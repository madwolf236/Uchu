using System.Threading.Tasks;

namespace Uchu.World.Behaviors
{
    public class ChargeUpBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.ChargeUp;
        
        public BehaviorBase Action { get; set; }
        
        public float MaxDuration { get; set; }
        
        public override async Task BuildAsync()
        {
            Action = await GetBehavior("action");

            MaxDuration = await GetParameter<float>("max_duration");
        }
        
        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branchContext)
        {
            await base.ExecuteAsync(context, branchContext);
            
            var handle = context.Reader.Read<uint>();
            
            ((Player) context.Associate)?.SendChatMessage($"ChargeUp: {handle}");

            RegisterHandle(handle, context, branchContext);
        }

        public override async Task SyncAsync(ExecutionContext context, ExecutionBranchContext branchContext)
        {
            await base.ExecuteAsync(context, branchContext);
            
            await Action.ExecuteAsync(context, branchContext);
        }
    }
}