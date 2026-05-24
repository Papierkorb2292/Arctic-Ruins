using Core.Factory;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    public class ShapeAsteroidStabilizerSimulationFactory: IFactory<ShapeAsteroidStabilizerSimulationState, ShapeAsteroidStabilizerSimulation>
    {
        public readonly IShapeAsteroidStabilizerConfiguration Configuration;

        public ShapeAsteroidStabilizerSimulationFactory(IShapeAsteroidStabilizerConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ShapeAsteroidStabilizerSimulation Produce(ShapeAsteroidStabilizerSimulationState state)
        {
            return new ShapeAsteroidStabilizerSimulation(state, Configuration);
        }
    }
}