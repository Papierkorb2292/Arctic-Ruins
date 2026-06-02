using JetBrains.Annotations;

namespace ArcticRuins.CommunicationRelay
{
    [UsedImplicitly]
    public class CommunicationRelaySimulationRenderer
        : StatelessBuildingSimulationRenderer<CommunicationRelaySimulation, ICommunicationRelayDrawData>
    {
        public CommunicationRelaySimulationRenderer(
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
