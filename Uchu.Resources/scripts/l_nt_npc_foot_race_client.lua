--------------------------------------------------------------
-- Foot Race script that 
-- updated Ray... 12/13/10
--------------------------------------------------------------
require('L_ACT_GENERIC_ACTIVITY_MGR')
require('L_ACT_BASE_FOOT_RACE_CLIENT')

local gVars = { pathName = "NT_Foot_Race_Path_01",
                goalLot = 8575,           
                --obsticleLot = {8572, 8576, 8578, 8579, 6214,}, --6215,
                numOfObsticles = 2,
                finalGoalObsticleLot = 6216,
                sStopTooltipText = Localize("FOOT_RACE_STOP_QUESTION"),  
                sStartTooltipText = Localize("FOOT_RACE_START_QUESTION"), 
                sFirstGoalText = Localize("FOOT_RACE_FIRST_GOAL"),
                sFinalGoalText = Localize("FOOT_RACE_FINAL_GOAL"),
                startTime = 10,     -- amount of time the player should start the race with
                addTime = 10,       -- amount of time to add when the player hits a goal
                completedSetFlagNum = nil,      -- sets the specified flag when the race is completed
                completedFireEventGroup = nil,} -- -ie- "PandaGroup" - calls FireEventServerSide message named Foot_Race_Completed to the group with the playerID as param1

-- these variabls are for setting up the goal posts only
local tGoalpostVars = { goalpostLot = 3890,
                        goalpostOffset = 5.2,   -- how far right and left of the object to spawn in the goal posts.
                        nextEffectID = 503,
                        nextEffectType = "create",}

----------------------------------------------------------------
-- leave the functions below alone
----------------------------------------------------------------

----------------------------------------------------------------
-- Called when the script starts up; setup activity and randomseed
----------------------------------------------------------------
function onStartup(self)
    setLocalVars(self, gVars, tGoalpostVars)
    
    baseStartup(self)
end

----------------------------------------------------------------
-- called when the script is shut down; clears out the UI
----------------------------------------------------------------
function onShutdown(self)
    baseShutdown(self)
end

----------------------------------------------
-- sent when the local player interacts with the object
----------------------------------------------
function onClientUse(self, msg)    
    baseClientUse(self, msg)
end 

----------------------------------------------
-- sent when the local player terminates an interacts with the object
----------------------------------------------
function onTerminateInteraction(self, msg)  
    baseTerminateInteraction(self, msg)  
end

----------------------------------------------
-- sent when the player responds to the message box
----------------------------------------------
function onMessageBoxRespond(self, msg)
    baseMessageBoxRespond(self, msg)
end

----------------------------------------------
-- fire event sent from another client object
----------------------------------------------
function onFireEvent(self, msg)
    baseFireEvent(self, msg)
end

----------------------------------------------
-- called when an activity timer is updated
----------------------------------------------
function onActivityTimerUpdate(self, msg)
    baseActivityTimerUpdate(self, msg)
end

----------------------------------------------
-- called when an activity timer is finished
----------------------------------------------
function onActivityTimerDone(self, msg)
    baseActivityTimerDone(self, msg)
end
