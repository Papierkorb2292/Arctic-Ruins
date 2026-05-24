namespace ArcticRuins.LayerDetacher
{
    public interface ILayerDetacherDrawData : IBuildingCustomDrawData
    {
        IBeltLaneRendererDefinition InputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition LeftOutputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition RightOutputLaneRenderingDefinition { get; }
    }
}
