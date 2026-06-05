using Core.Factory;

namespace ArcticRuins.DataFragment
{
    public class DataFragmentSimulationFactory: IFactory<DataFragmentSimulationState, DataFragmentSimulation>
    {
        public DataFragmentSimulation Produce(DataFragmentSimulationState state)
        {
            return new DataFragmentSimulation(state);
        }
    }
}