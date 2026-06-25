using Core.Factory;

namespace ArcticRuins.Ruins
{
    public class RuinsSimulationFactory: IFactory<RuinsSimulationState, RuinsSimulation>
    {
        public RuinsSimulation Produce(RuinsSimulationState state)
        {
            return new RuinsSimulation(state);
        }
    }
}