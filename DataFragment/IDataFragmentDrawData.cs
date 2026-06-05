namespace ArcticRuins.DataFragment
{
    public interface IDataFragmentDrawData : IBuildingCustomDrawData
    {
        public ILODMesh DataCubeMesh { get; }
    }
}
