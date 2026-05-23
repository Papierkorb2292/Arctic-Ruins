using Game.Core.Belts.BeltPath;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.ReceiverFromHub;

[SyncableIdentifier("ArticRuinsBeltPortReceiverFromHubState")]
public class BeltPortReceiverFromHubSimulationState : ISimulationState
{
    private const short MaxVortexItems = 50; //TODO: Shorten the time it takes to arrive at the receiver
    public readonly BeltLaneState OutputLaneState = new();
    public readonly FastBeltPathLaneState VortexLaneState = new(MaxVortexItems);

    public void Sync(ISerializationVisitor visitor)
    {
        if (visitor.Version < GameVersion.AddOptionalEntityConfiguration)
            throw new OutdatedBlobSkipRemainingBytesException();
        OutputLaneState.Sync(visitor);
        VortexLaneState.Sync(visitor);
    }
}