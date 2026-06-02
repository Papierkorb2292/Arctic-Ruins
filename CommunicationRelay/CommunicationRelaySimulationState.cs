using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.CommunicationRelay;

[SyncableIdentifier("CommunicationRelayState")]
public class CommunicationRelaySimulationState : ISimulationState
{
    public void Sync(ISerializationVisitor visitor)
    {
    }
}