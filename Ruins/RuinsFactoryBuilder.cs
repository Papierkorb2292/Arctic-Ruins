using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace ArcticRuins.Ruins
{
    internal class DataFragmentFactoryBuilder
        : IBuildingSimulationFactoryBuilder<RuinsSimulation, RuinsSimulationState, IDataFragmentConfiguration>
    {
        public IFactory<RuinsSimulationState, RuinsSimulation> BuildFactory(
            SimulationSystemsDependencies dependencies,
            out IDataFragmentConfiguration config)
        {
            config = new DataFragmentConfiguration();
            return new RuinsSimulationFactory();
        }
    }
}