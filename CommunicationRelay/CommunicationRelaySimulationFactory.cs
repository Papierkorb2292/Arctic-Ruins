using Core.Factory;

namespace ArcticRuins.CommunicationRelay
{
    public class CommunicationRelaySimulationFactory: IFactory<CommunicationRelaySimulationState, CommunicationRelaySimulation>
    {
        public CommunicationRelaySimulation Produce(CommunicationRelaySimulationState state)
        {
            return new CommunicationRelaySimulation(state);
        }
    }
}