using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ArcticRuins.LayerDetacher;

public class ShapeOperationLayerDetach(
    int maxShapeLayers,
    IShapeRegistry shapeRegistry,
    IShapeIdManager shapeIdManager)
    : ShapeOperation<ShapeDefinition, ShapeLayerDetachResult>(shapeRegistry, shapeIdManager), IItemOperation1In2Out
{
    public override ShapeLayerDetachResult ExecuteInternal(ShapeDefinition shape)
    {
        var unfolded = ShapeLogic.Unfold(shape.Layers);
        var highestLayer = unfolded.References.Select(reference => reference.LayerIndex).Max();
        var lowerLayers = unfolded.References.Where(reference => reference.LayerIndex < highestLayer).ToList();
        var topLayer = unfolded.References.Where(reference => reference.LayerIndex == highestLayer).ToList();

        var lowerLayersResult = ShapeLogic.Collapse(
            lowerLayers,
            shape.PartCount,
            maxShapeLayers,
            ShapeIdManager,
            unfolded.FusedReferences);
        var topLayerResult = ShapeLogic.Collapse(
            topLayer,
            shape.PartCount,
            maxShapeLayers,
            ShapeIdManager,
            unfolded.FusedReferences);

        return new ShapeLayerDetachResult(lowerLayersResult, topLayerResult);
    }

    public bool TryExecute(IItem input, [UnscopedRef] out IItem output1, [UnscopedRef] out IItem output2)
    {
        if (input is not ShapeItem shapeItem)
        {
            output1 = null;
            output2 = null;
            return false;
        }
        var shapeDetachResult = Execute(shapeItem.Definition);
        output1 = shapeDetachResult.LowerLayers != null ? ShapeRegistry.GetItem(shapeDetachResult.LowerLayers.Shape) : (IItem)null;
        output2 = shapeDetachResult.TopLayer != null ? ShapeRegistry.GetItem(shapeDetachResult.TopLayer.Shape) : (IItem)null;
        return true;
    }
}
