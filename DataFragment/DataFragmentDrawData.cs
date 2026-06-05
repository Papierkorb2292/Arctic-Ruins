using Game.Core.Coordinates;

namespace ArcticRuins.DataFragment
{
    internal class DataFragmentDrawData(ILODMesh dataCubeMesh) : IDataFragmentDrawData
    {
        public ILODMesh DataCubeMesh => dataCubeMesh;
    }
}