namespace ArcticRuins.ArcticCutter
{
    public interface IArcticCutterDrawData : IBuildingCustomDrawData
    {
        IBeltLaneRendererDefinition InputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition LeftOutputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition RightOutputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition LeftLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition RightLaneRenderingDefinition { get; }
    }
}
