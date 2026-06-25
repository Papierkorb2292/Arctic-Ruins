using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.Ruins;

[SyncableIdentifier("RuinsState")]
public class RuinsSimulationState : ISimulationState
{
    public void Sync(ISerializationVisitor visitor)
    {
    }
}