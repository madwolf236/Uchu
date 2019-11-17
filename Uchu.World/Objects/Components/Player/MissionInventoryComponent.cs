using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Uchu.Core;
using Uchu.Core.Client;
using Uchu.World.Client;

namespace Uchu.World
{
    public class MissionInventoryComponent : Component
    {
        private readonly object _lock = new object();

        public Mission[] GetCompletedMissions()
        {
            using var ctx = new UchuContext();
            return ctx.Missions.Where(
                m => m.Character.CharacterId == GameObject.ObjectId && m.State == (int) MissionState.Completed
            ).ToArray();
        }

        public Mission[] GetActiveMissions()
        {
            using var ctx = new UchuContext();
            return ctx.Missions.Where(
                m => m.Character.CharacterId == GameObject.ObjectId &&
                     m.State == (int) MissionState.Active ||
                     m.State == (int) MissionState.CompletedActive
            ).ToArray();
        }

        public Mission[] GetMissions()
        {
            using var ctx = new UchuContext();
            return ctx.Missions.Where(
                m => m.Character.CharacterId == GameObject.ObjectId
            ).ToArray();
        }

        public void MessageOfferMission(int missionId, GameObject missionGiver)
        {
            As<Player>().Message(new OfferMissionMessage
            {
                Associate = GameObject,
                MissionId = missionId,
                QuestGiver = missionGiver
            });
        }

        public void MessageMissionState(int missionId, MissionState state, bool sendingRewards = false)
        {
            using (var ctx = new UchuContext())
            {
                var character = ctx.Characters
                    .Include(c => c.Missions)
                    .Single(c => c.CharacterId == GameObject.ObjectId);

                var mission = character.Missions.Single(m => m.MissionId == missionId);

                mission.State = (int) state;

                ctx.SaveChanges();
            }

            if (state == MissionState.ReadyToComplete) state = MissionState.Active;

            As<Player>().Message(new NotifyMissionMessage
            {
                Associate = GameObject,
                MissionId = missionId,
                MissionState = state,
                SendingRewards = sendingRewards
            });
        }

        public void MessageMissionTypeState(MissionLockState state, string subType, string type)
        {
            As<Player>().Message(new SetMissionTypeStateMessage
            {
                Associate = GameObject,
                LockState = state,
                SubType = subType,
                Type = type
            });
        }

        public void MessageUpdateMissionTask(int missionId, int taskIndex, float[] updates)
        {
            As<Player>().Message(new NotifyMissionTaskMessage
            {
                Associate = GameObject,
                MissionId = missionId,
                TaskIndex = taskIndex,
                Updates = updates
            });
        }

        public async Task RespondToMissionAsync(int missionId, GameObject missionGiver, Lot rewardItem)
        {
            Logger.Information($"Responding {missionId}");

            //
            // The player has clicked on the accept or complete button.
            //

            await using var ctx = new UchuContext();
            await using var cdClient = new CdClientContext();
            
            //
            // Collect character data.
            //

            var character = await ctx.Characters
                .Include(c => c.Items)
                .Include(c => c.Missions)
                .ThenInclude(m => m.Tasks)
                .SingleAsync(c => c.CharacterId == GameObject.ObjectId);

            //
            // Get the mission the player is responding to.
            //

            var mission = await cdClient.MissionsTable.FirstAsync(m => m.Id == missionId);

            //
            // Get the character mission to update, if present.
            //

            var characterMission = character.Missions.Find(m => m.MissionId == missionId);

            //
            // Check if the player is accepting a mission or responding to one.
            //

            if (characterMission == default)
            {
                //
                // Player is accepting a new mission.
                //

                //
                // Get all the tasks of this mission setup the new mission.
                //

                var tasks = cdClient.MissionTasksTable.Where(t => t.Id == missionId);

                //
                // Setup new mission
                //

                character.Missions.Add(new Mission
                {
                    MissionId = missionId,
                    Tasks = tasks.Select(t => GetTask(character, t)).ToList()
                });

                await ctx.SaveChangesAsync();

                MessageMissionState(missionId, MissionState.Active);

                MessageMissionTypeState(MissionLockState.New, mission.Definedsubtype, mission.Definedtype);

                return;
            }

            //
            // Player is responding to an active mission.
            //

            if (!await MissionParser.AllTasksCompletedAsync(characterMission))
            {
                //
                // Mission is not complete.
                //

                MessageMissionState(missionId, MissionState.Active);

                MessageOfferMission(missionId, missionGiver);

                return;
            }

            //
            // Complete mission.
            //

            CompleteMission(missionId, rewardItem);
        }

        public void CompleteMission(int missionId, Lot rewardItem = default)
        {
            lock (_lock)
            {
                Logger.Information($"Completing mission {missionId}");

                using var ctx = new UchuContext();
                using var cdClient = new CdClientContext();
            
                //
                // Get mission information.
                //
                
                var mission = cdClient.MissionsTable.FirstOrDefault(m => m.Id == missionId);
    
                if (mission == default) return;
                
                //
                // Get character information.
                //
                
                var character = ctx.Characters
                    .Include(c => c.Items)
                    .Include(c => c.Missions)
                    .ThenInclude(m => m.Tasks)
                    .Single(c => c.CharacterId == GameObject.ObjectId);
    
                //
                // If this mission is not already accepted, accept it and move on to complete it.
                //
                
                if (!character.Missions.Exists(m => m.MissionId == missionId))
                {
                    var tasks = cdClient.MissionTasksTable.Where(t => t.Id == missionId);
    
                    character.Missions.Add(new Mission
                    {
                        MissionId = missionId,
                        State = (int) MissionState.Active,
                        Tasks = tasks.Select(t => GetTask(character, t)).ToList()
                    });
                }
    
                //
                // Save changes to be able to update its state.
                //
                
                ctx.SaveChanges();
                
                MessageMissionState(missionId, MissionState.Unavailable, true);
                
                //
                // Get character mission to complete.
                //
                
                var characterMission = character.Missions.Find(m => m.MissionId == missionId);
                
                if (characterMission.State == (int) MissionState.Completed) return;
                
                var repeat = characterMission.CompletionCount != 0;
                characterMission.CompletionCount++;
                characterMission.LastCompletion = DateTimeOffset.Now.ToUnixTimeSeconds();

                //
                // Update player based on rewards.
                //
    
                if (mission.IsMission ?? true)
                {
                    // Mission
                    
                    As<Player>().Currency += mission.Rewardcurrency ?? 0;
    
                    As<Player>().UniverseScore += mission.LegoScore ?? 0;
                }
                else
                {
                    //
                    // Achievement
                    //
                    // These rewards have the be silent, as the client adds them itself.
                    //
    
                    character.Currency += mission.Rewardcurrency ?? 0;
                    character.UniverseScore += mission.LegoScore ?? 0;

                    //
                    // The client adds currency rewards as an offset, in my testing. Therefore we
                    // have to account for this offset.
                    //
                    
                    As<Player>().HiddenCurrency += mission.Rewardcurrency ?? 0;

                    ctx.SaveChanges();
                }
    
                var stats = GameObject.GetComponent<Stats>();
                
                stats.MaxHealth += (uint) (mission.Rewardmaxhealth ?? 0);
                stats.MaxImagination += (uint) (mission.Rewardmaximagination ?? 0);

                //
                // Get item rewards.
                //
    
                var inventory = GameObject.GetComponent<InventoryManagerComponent>();
                
                var rewards = new (Lot, int)[]
                {
                    ((repeat ? mission.Rewarditem1repeatable : mission.Rewarditem1) ?? 0, 
                        (repeat ? mission.Rewarditem1repeatcount : mission.Rewarditem1count) ?? 1),
                    
                    ((repeat ? mission.Rewarditem2repeatable : mission.Rewarditem2) ?? 0, 
                        (repeat ? mission.Rewarditem2repeatcount : mission.Rewarditem2count) ?? 1),
                    
                    ((repeat ? mission.Rewarditem3repeatable : mission.Rewarditem3) ?? 0, 
                        (repeat ? mission.Rewarditem3repeatcount : mission.Rewarditem3count) ?? 1),
                    
                    ((repeat ? mission.Rewarditem4repeatable : mission.Rewarditem4) ?? 0, 
                        (repeat ? mission.Rewarditem4repeatcount : mission.Rewarditem4count) ?? 1),
                };
    
                if (rewardItem == -1)
                {
                    foreach (var (lot, count) in rewards)
                    {
                        if (lot == default || count == default) continue;
                        
                        Task.Run(async () => await inventory.AddItemAsync(lot, (uint) count));
                    }
                }
                else
                {
                    var (lot, count) = rewards[rewardItem];
                    
                    if (lot != default && count != default)
                        Task.Run(async () => await inventory.AddItemAsync(lot, (uint) count));
                }
    
                //
                // Inform the client it's now complete.
                //
                
                MessageMissionState(missionId, MissionState.Completed);

                characterMission.State = (int) MissionState.Completed;
                
                ctx.SaveChanges();
            }
        }

        public async Task UpdateObjectTaskAsync(MissionTaskType type, Lot lot, GameObject gameObject = default)
        {
            await using var ctx = new UchuContext();
            await using var cdClient = new CdClientContext();
            
            //
            // Collect character data.
            //

            var character = await ctx.Characters
                .Include(c => c.Items)
                .Include(c => c.Missions)
                .ThenInclude(m => m.Tasks)
                .SingleAsync(c => c.CharacterId == GameObject.ObjectId);

            //
            // Check if this object has anything to do with any of the active missions.
            //

            foreach (var mission in character.Missions)
            {
                //
                // Only active missions should have tasks that can be completed, the rest can be skipped.
                //

                var missionState = (MissionState) mission.State;
                if (missionState != MissionState.Active && missionState != MissionState.CompletedActive) continue;

                //
                // Get all the tasks this mission operates on.
                //

                var tasks = cdClient.MissionTasksTable.Where(
                    t => t.Id == mission.MissionId
                ).ToArray();

                //
                // Get the task, if any, that includes any requirements related to this object.
                //

                var task = tasks.FirstOrDefault(missionTask =>
                    MissionParser.GetTargets(missionTask).Contains(lot) &&
                    mission.Tasks.Exists(a => a.TaskId == missionTask.Uid)
                );

                //
                // If not, move on to the next mission.
                //

                if (task == default) continue;

                //
                // Get the task on the character mission which will be updated.
                //

                var characterTask = mission.Tasks.Find(t => t.TaskId == task.Uid);

                // Get task id.
                if (task.Id == default) return;

                var taskId = task.Id.Value;

                switch (type)
                {
                    case MissionTaskType.Collect:
                        if (gameObject == default)
                        {
                            Logger.Error($"{type} is only valid when {nameof(gameObject)} != null");
                            return;
                        }

                        var component = gameObject.GetComponent<CollectibleComponent>();

                        // The collectibleId bitshifted by the zoneId, as that is how the client expects it later
                        var shiftedId = (float) component.CollectibleId + (gameObject.Zone.ZoneInfo.ZoneId << 8);
                        
                        if (!characterTask.Values.Contains(shiftedId) && task.TargetValue > characterTask.Values.Count)
                        {
                            Logger.Information($"{GameObject} collected {component.CollectibleId}");
                            characterTask.Values.Add(shiftedId);
                        }

                        Logger.Information($"Has collected {characterTask.Values.Count}/{task.TargetValue}");

                        // Send update to client
                        MessageUpdateMissionTask(
                            taskId, tasks.IndexOf(task),
                            new[]
                            {
                                shiftedId
                            }
                        );

                        break;
                    case MissionTaskType.KillEnemy:
                        if (task.TargetValue > characterTask.Values.Count)
                        {
                            characterTask.Values.Add(lot);
                        }

                        break;
                    case MissionTaskType.QuickBuild:
                    case MissionTaskType.NexusTowerBrickDonation:
                    case MissionTaskType.None:
                    case MissionTaskType.Discover:
                    case MissionTaskType.GoToNpc:
                    case MissionTaskType.MinigameAchievement:
                    case MissionTaskType.UseEmote:
                    case MissionTaskType.UseConsumable:
                    case MissionTaskType.UseSkill:
                    case MissionTaskType.ObtainItem:
                    case MissionTaskType.Interact:
                    case MissionTaskType.Flag:
                    case MissionTaskType.Script:
                    case MissionTaskType.MissionComplete:
                    case MissionTaskType.TamePet:
                    case MissionTaskType.Racing:
                        // Start this task value array
                        if (!characterTask.Values.Contains(lot))
                            characterTask.Values.Add(lot);

                        // Send update to client
                        MessageUpdateMissionTask(
                            taskId, tasks.IndexOf(task),
                            new[] {(float) characterTask.Values.Count}
                        );

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }

                await ctx.SaveChangesAsync();

                //
                // Check if this mission is complete.
                //

                if (!await MissionParser.AllTasksCompletedAsync(mission)) continue;

                MessageMissionState(mission.MissionId, MissionState.ReadyToComplete);
            }

            //
            // Collect tasks which fits the requirements of this action.
            //

            var otherTasks = new List<MissionTasks>();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var missionTask in cdClient.MissionTasksTable)
                if (MissionParser.GetTargets(missionTask).Contains(lot))
                    otherTasks.Add(missionTask);

            foreach (var task in otherTasks)
            {
                var mission = cdClient.MissionsTable.First(m => m.Id == task.Id);

                //
                // Check if mission is an achievement and has a task of the correct type.
                //

                if (mission.OfferobjectID != -1 ||
                    mission.TargetobjectID != -1 ||
                    (mission.IsMission ?? true) ||
                    task.TaskType != (int) type)
                    continue;

                //
                // Get all tasks for the mission connected to this task.
                //

                var tasks = cdClient.MissionTasksTable.Where(m => m.Id == mission.Id).ToArray();

                //
                // Get the mission on the character. If present.
                //

                var characterMission = character.Missions.Find(m => m.MissionId == mission.Id);

                //
                // Check if the player could passably start this achievement.
                //

                if (characterMission == default)
                {
                    //
                    // Check if player has the Prerequisites to start this achievement.
                    //

                    var hasPrerequisites = MissionParser.CheckPrerequiredMissions(
                        mission.PrereqMissionID,
                        GetCompletedMissions()
                    );

                    if (!hasPrerequisites) continue;

                    //
                    // Player can start achievement.
                    //

                    // Get Mission Id of new achievement.
                    if (mission.Id == default) continue;
                    var missionId = mission.Id.Value;

                    //
                    // Setup new achievement.
                    //

                    characterMission = new Mission
                    {
                        MissionId = missionId,
                        State = (int) MissionState.Active,
                        Tasks = tasks.Select(t =>
                        {
                            Debug.Assert(t.Uid != null, "t.Uid != null");
                            return new MissionTask
                            {
                                TaskId = (int) t.Uid,
                                Values = new List<float>()
                            };
                        }).ToList()
                    };

                    //
                    // Add achievement to the database.
                    //

                    character.Missions.Add(characterMission);

                    await ctx.SaveChangesAsync();
                }

                //
                // Check if the mission is active.
                //

                var state = (MissionState) characterMission.State;
                
                if (state != MissionState.Active && state != MissionState.CompletedActive) continue;

                //
                // Get the task to be updated.
                //

                var characterTask = characterMission.Tasks.Find(t => t.TaskId == task.Uid);

                // Start this task value array
                if (!characterTask.Values.Contains(lot)) characterTask.Values.Add(lot);

                await ctx.SaveChangesAsync();

                //
                // Notify the client of the new achievement
                //

                MessageUpdateMissionTask(
                    characterMission.MissionId,
                    tasks.IndexOf(task),
                    new[] {(float) characterTask.Values.Count}
                );

                //
                // Check if achievement is complete.
                //

                if (await MissionParser.AllTasksCompletedAsync(characterMission))
                {
                    CompleteMission(characterMission.MissionId);
                }
            }
        }

        private static MissionTask GetTask(Character character, MissionTasks task)
        {
            var values = new List<float>();

            var targets = MissionParser.GetTargets(task);

            values.AddRange(targets
                .Where(lot => character.Items.Exists(i => i.LOT == lot))
                .Select(lot => (float) (int) lot));

            Debug.Assert(task.Uid != null, "t.Uid != null");
            return new MissionTask
            {
                TaskId = task.Uid.Value,
                Values = values
            };
        }
    }
}