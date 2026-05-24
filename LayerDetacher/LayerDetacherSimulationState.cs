using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.LayerDetacher;

[SyncableIdentifier("LayerDetacherState")]
public class LayerDetacherSimulationState : ISimulationState
{
    public readonly BeltLaneState InputLaneState = new();
    public readonly BeltLaneState LeftOutputLaneState = new();
    public readonly BeltLaneState RightOutputLaneState = new();
    public readonly BeltLaneState LeftProcessingLaneState = new();
    public readonly BeltLaneState RightProcessingLaneState = new();
    public ShapeCollapseResult LeftCollapseResult;
    public ShapeCollapseResult RightCollapseResult;
    public ShapeItem LastProcessedShape;
    
    public void Sync(ISerializationVisitor visitor)
    {
        InputLaneState.Sync(visitor);
        LeftProcessingLaneState.Sync(visitor);
        RightProcessingLaneState.Sync(visitor);
        LeftOutputLaneState.Sync(visitor);
        RightOutputLaneState.Sync(visitor);

        var collapseResultSerializer = visitor.GetSerializer<ShapeCollapseResult>();

        collapseResultSerializer.Sync(ref LeftCollapseResult);
        collapseResultSerializer.Sync(ref RightCollapseResult);

        if (visitor.Writing)
        {
            if (LastProcessedShape == null)
            {
                visitor.WriteBool_1(false);
            } 
            else
            {
                visitor.WriteBool_1(true);
                visitor.Serialize(LastProcessedShape);   
            }
        }
        else
        {
            LastProcessedShape = visitor.ReadBool_1() ? visitor.Deserialize<ShapeItem>() : null;
        }
    }
}