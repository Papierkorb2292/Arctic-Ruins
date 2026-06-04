using Game.Core.Belts.BeltPath;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.ReceiverFromHub;

[SyncableIdentifier("ArcticRuinsBeltPortReceiverFromHubState")]
public class BeltPortReceiverFromHubSimulationState : ISimulationState
{
    private const short MaxVortexItems = 15;
    public readonly BeltLaneState OutputLaneState = new();
    public readonly FastBeltPathLaneState VortexLaneState = new(MaxVortexItems);
    public ShapeItem BufferedItem;

    public void Sync(ISerializationVisitor visitor)
    {
        if (visitor.Version < GameVersion.AddOptionalEntityConfiguration)
            throw new OutdatedBlobSkipRemainingBytesException();
        OutputLaneState.Sync(visitor);
        VortexLaneState.Sync(visitor);
        if (visitor.Writing)
            visitor.Serialize(BufferedItem);
        else
            BufferedItem = visitor.Deserialize<ShapeItem>();
    }
}