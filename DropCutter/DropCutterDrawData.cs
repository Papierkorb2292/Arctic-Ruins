using Game.Core.Coordinates;

namespace DiagonalCutter.DropCutter
{
    internal class DropCutterDrawData : IDropCutterDrawData
    {
        public IBeltLaneRendererDefinition InputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(-0.5f, 0.0f, 1.0f),
            new LocalVector(0.0f, 0.0f, 1.0f));

        public IBeltLaneRendererDefinition LeftOutputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, 0.0f, 0.0f),
            new LocalVector(0.5f, 0.0f, 0.0f));
        
        public IBeltLaneRendererDefinition RightOutputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, 0.0f, 0.0f),
            new LocalVector(0.0f, -0.5f, 0.0f));
        
        public IBeltLaneRendererDefinition LeftLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, 0.0f, 1.0f),
            new LocalVector(0.0f, 0.0f, 0.0f));
        
        public IBeltLaneRendererDefinition RightLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, 0.0f, 1.0f),
            new LocalVector(0.0f, 0.0f, 0.0f));
    }
}