using Core.Factory;

namespace ArcticRuins.DataFragment
{
    public class DataFragmentSimulationFactory(ResearchProgression progression): IFactory<DataFragmentSimulationState, DataFragmentSimulation>
    {
        public DataFragmentSimulation Produce(DataFragmentSimulationState state)
        {
            return new DataFragmentSimulation(state, progression);
        }
    }
}