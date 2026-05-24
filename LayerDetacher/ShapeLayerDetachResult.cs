namespace ArcticRuins.LayerDetacher;

public class ShapeLayerDetachResult(ShapeCollapseResult lowerLayers, ShapeCollapseResult topLayer)
{
    public readonly ShapeCollapseResult LowerLayers = lowerLayers;
    public readonly ShapeCollapseResult TopLayer = topLayer;
}