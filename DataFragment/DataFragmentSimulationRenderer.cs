using JetBrains.Annotations;

namespace ArcticRuins.DataFragment
{
    [UsedImplicitly]
    public class DataFragmentSimulationRenderer
        : StatelessBuildingSimulationRenderer<DataFragmentSimulation, IDataFragmentDrawData>
    {
        public DataFragmentSimulationRenderer(
            IMapModel map,
            IBuildingSoundManager soundManager,
            IShapeRegistry shapeRegistry) : base(map)
        {
        }

        public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
        }
    }
}
