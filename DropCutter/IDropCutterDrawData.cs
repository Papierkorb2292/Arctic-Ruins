namespace DiagonalCutter.DropCutter
{
    public interface IDropCutterDrawData : IBuildingCustomDrawData
    {
        IBeltLaneRendererDefinition InputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition LeftOutputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition RightOutputLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition LeftLaneRenderingDefinition { get; }
        IBeltLaneRendererDefinition RightLaneRenderingDefinition { get; }
    }
}
