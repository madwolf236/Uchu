using RakDotNet.IO;
using Uchu.World.Parsers;

namespace Uchu.World
{
    public class ModuleAssemblyComponent : ReplicaComponent
    {
        public override ComponentId Id => ComponentId.ModuleAssemblyComponent;

        public override void FromLevelObject(LevelObject levelObject)
        {
        }

        public override void Construct(BitWriter writer)
        {
            writer.WriteBit(false);
        }

        public override void Serialize(BitWriter writer)
        {
        }
    }
}