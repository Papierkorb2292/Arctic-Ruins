using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace ArcticRuins.DataFragment
{
    internal class DataFragmentFactoryBuilder
        : IBuildingSimulationFactoryBuilder<DataFragmentSimulation, DataFragmentSimulationState, IDataFragmentConfiguration>
    {
        public IFactory<DataFragmentSimulationState, DataFragmentSimulation> BuildFactory(
            SimulationSystemsDependencies dependencies,
            out IDataFragmentConfiguration config)
        {
            config = new DataFragmentConfiguration();
            return new DataFragmentSimulationFactory();
        }
    }
}