using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.DataFragment;

[SyncableIdentifier("DataFragmentState")]
public class DataFragmentSimulationState : ISimulationState
{
    public void Sync(ISerializationVisitor visitor)
    {
    }
}