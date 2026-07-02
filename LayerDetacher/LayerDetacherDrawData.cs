using Game.Core.Coordinates;

namespace ArcticRuins.LayerDetacher
{
    internal class LayerDetacherDrawData(ILODMesh launcherMesh, bool isMirrored) : ILayerDetacherDrawData
    {
        public IBeltLaneRendererDefinition InputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(-0.5f, 0.0f, 0.0f),
            new LocalVector(0.0f, 0.0f, 0.0f));

        public IBeltLaneRendererDefinition LeftOutputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, FlipForMirrored(-1.0f), 0.0f),
            new LocalVector(0.5f, FlipForMirrored(-1.0f), 0.0f));
        
        public IBeltLaneRendererDefinition RightOutputLaneRenderingDefinition => new MyBeltLaneRenderingDefinition(
            new LocalVector(0.0f, 0.0f, 0.0f),
            new LocalVector(0.5f, 0.0f, 0.0f));

        public ILODMesh LauncherMesh => launcherMesh;
        public float FlipForMirrored(float value) => isMirrored ? -value : value;
    }
}