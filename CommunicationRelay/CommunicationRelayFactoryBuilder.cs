using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace ArcticRuins.CommunicationRelay
{
    internal class CommunicationRelayFactoryBuilder
        : IBuildingSimulationFactoryBuilder<CommunicationRelaySimulation, CommunicationRelaySimulationState, ICommunicationRelayConfiguration>
    {
        public IFactory<CommunicationRelaySimulationState, CommunicationRelaySimulation> BuildFactory(
            SimulationSystemsDependencies dependencies,
            out ICommunicationRelayConfiguration config)
        {
            config = new CommunicationRelayConfiguration();
            return new CommunicationRelaySimulationFactory();
        }
    }
}