using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Uchu.Core;
using Uchu.World.Scripting.Managed;

namespace Uchu.World.Handlers.Commands
{
    public class OperatorCommandHandler : HandlerGroup
    {
        [CommandHandler(Signature = "stop", Help = "Stop the server", GameMasterLevel = GameMasterLevel.Operator)]
        public async Task<string> Stop(string[] arguments, Player player)
        {
            await using var ctx = new UchuContext();

            var world = await ctx.Specifications.FirstOrDefaultAsync(
                s => s.ServerType == ServerType.World && s.Id != Server.Id
            );

            foreach (var zonePlayer in player.Zone.Players)
            {
                if (world == default)
                {
                    zonePlayer.Message(new DisconnectNotifyPacket
                    {
                        DisconnectId = DisconnectId.ServerShutdown
                    });
                    
                    continue;
                }
                
                zonePlayer.SendChatMessage($"This zone is closing, going to {world.ZoneId}!");
                
                await zonePlayer.SendToWorldAsync(world);
            }

            var delay = 1000;

            if (arguments.Length > 0)
            {
                int.TryParse(arguments[0], out delay);
            }

            await Task.Delay(delay);

            Environment.Exit(0);

            return "Stopped server";
        }

        [CommandHandler(Signature = "save", Help = "Save a serialization", GameMasterLevel = GameMasterLevel.Operator)]
        public async Task<string> SaveSerialize(string[] arguments, Player player)
        {
            var current = player.Zone.GameObjects[0];

            foreach (var gameObject in player.Zone.GameObjects.Where(g => g != player && g != default))
            {
                if (gameObject.Transform == default) continue;

                if (gameObject.GetComponent<SpawnerComponent>() != default) continue;

                if (Vector3.Distance(current.Transform.Position, player.Transform.Position) >
                    Vector3.Distance(gameObject.Transform.Position, player.Transform.Position))
                    current = gameObject;
            }

            var path = Path.Combine(Server.MasterPath, "./packets/");

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            Zone.SaveSerialization(current, new[] {player}, Path.Combine(path, $"./{current.ObjectId}_s.bin"));
            Zone.SaveCreation(current, new[] {player}, Path.Combine(path, $"./{current.ObjectId}_c.bin"));

            return "Saved packets";
        }

        [CommandHandler(Signature = "python", Help = "Run python", GameMasterLevel = GameMasterLevel.Admin)]
        public async Task<string> Python(string[] arguments, Player player)
        {
            if (arguments.Length <= 1) return $"python <id> <python code to run>";

            var param = arguments.ToList();

            var name = param[0];

            param.RemoveAt(0);

            var source = string.Join(" ", param).Replace(@"\n", "\n").Replace(@"\t", "\t");

            await player.Zone.ScriptManager.SetManagedScript(name, source);

            return "Attempting to run python script...";
        }

        [CommandHandler(Signature = "python-load", Help = "Load a python file", GameMasterLevel = GameMasterLevel.Admin)]
        public async Task<string> PythonLoad(string[] arguments, Player player)
        {
            if (arguments.Length == 0) return "python-load <file>";

            await player.Zone.ScriptManager.SetManagedScript(arguments[0]);
            
            return "Attempting to run python pack...";
        }

        [CommandHandler(Signature = "python-unload", Help = "Unload a python script", GameMasterLevel = GameMasterLevel.Admin)]
        public async Task<string> PythonUnload(string[] arguments, Player player)
        {
            if (arguments.Length == 0) return "python-unload <id>";

            await player.Zone.ScriptManager.SetManagedScript(arguments[0]);
            
            return $"Unloaded: {arguments[0]}";
        }

        [CommandHandler(Signature = "python-list", Help = "List all python scripts", GameMasterLevel = GameMasterLevel.Admin)]
        public string PythonList(string[] arguments, Player python)
        {
            var builder = new StringBuilder();

            builder.Append("Loaded scripts:");

            foreach (var scriptPack in python.Zone.ScriptManager.ScriptPacks.OfType<PythonScriptPack>())
            {
                builder.Append($"\n{scriptPack.Name}");
            }

            return builder.ToString();
        }
    }
}