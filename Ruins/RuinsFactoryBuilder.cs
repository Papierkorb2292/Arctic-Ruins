using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace ArcticRuins.Ruins
{
    internal class DataFragmentFactoryBuilder
        : IBuildingSimulationFactoryBuilder<RuinsSimulation, RuinsSimulationState, IRuinsConfiguration>
    {
        public IFactory<RuinsSimulationState, RuinsSimulation> BuildFactory(
            SimulationSystemsDependencies dependencies,
            out IRuinsConfiguration config)
        {
            config = new RuinsConfiguration();
            return new RuinsSimulationFactory();
        }
    }
}