using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    internal class ShapeAsteroidStabilizerFactoryBuilder
        : IBuildingSimulationFactoryBuilder<ShapeAsteroidStabilizerSimulation, ShapeAsteroidStabilizerSimulationState,
            IShapeAsteroidStabilizerConfiguration>
    {
        public IFactory<ShapeAsteroidStabilizerSimulationState, ShapeAsteroidStabilizerSimulation> BuildFactory(
            SimulationSystemsDependencies dependencies,
            out IShapeAsteroidStabilizerConfiguration config)
        {
            config = new ShapeShapeAsteroidStabilizerConfiguration(
                BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
                BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
                new ResearchSpeedId("BeltSpeed"));
            return new ShapeAsteroidStabilizerSimulationFactory(config);
        }
    }
}