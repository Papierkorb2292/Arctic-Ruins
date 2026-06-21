using Game.Core.Coordinates;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    internal class ShapeShapeAsteroidStabilizerDrawData(ILODMesh hammerMesh) : IShapeAsteroidStabilizerDrawData
    {
        public IBeltLaneRendererDefinition InputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(-0.5f, 0.0f, 0.0f),
            new LocalVector(0.0f, 0.0f, 0.0f));

        public IBeltLaneRendererDefinition ProcessingLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, 0.0f, 0.0f),
            new LocalVector(0.0f, 0.0f, -2.0f));

        public ILODMesh HammerMesh => hammerMesh;
    }
}