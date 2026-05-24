using Game.Core.Coordinates;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    internal class ShapeShapeAsteroidStabilizerDrawData : IShapeAsteroidStabilizerDrawData
    {
        public IBeltLaneRendererDefinition InputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(-0.5f, 0.0f, 0.0f),
            new LocalVector(0.0f, 0.0f, 0.0f));

        public IBeltLaneRendererDefinition ProcessingLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, 0.0f, 0.0f),
            new LocalVector(0.0f, 0.0f, 0.0f));
    }
}