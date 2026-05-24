namespace ArcticRuins.ShapeAsteroidStabilizer
{
    public interface IShapeAsteroidStabilizerDrawData : IBuildingCustomDrawData
    {
        IBeltLaneRendererDefinition InputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition ProcessingLaneRenderingDefinition { get; }
    }
}
