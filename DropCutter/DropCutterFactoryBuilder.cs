using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace DiagonalCutter.DropCutter
{
    internal class DropCutterFactoryBuilder
        : IBuildingSimulationFactoryBuilder<DropCutterSimulation, FullCutterSimulationState,
            ICutterConfiguration>
    {
        public IFactory<FullCutterSimulationState, DropCutterSimulation> BuildFactory(
            SimulationSystemsDependencies dependencies,
            out ICutterConfiguration config)
        {
            var researchSpeed = new ResearchSpeedId("CutterSpeed");
            var beltSpeed = new BuffableBeltSpeed
            {
                BaseSpeed = BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
                ResearchId = researchSpeed
            };
            var processingDelay = new BuffableBeltDelay
            {
                BaseDuration = BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
                Research = researchSpeed
            };
            beltSpeed.OnAfterDeserialize();
            processingDelay.OnAfterDeserialize();
            config = new FullCutterMetaBuildingDefinition.Configuration
            {
                BeltSpeed = beltSpeed,
                ProcessingDelay = processingDelay
            };

            var cut = new ShapeOperationCut(
                dependencies.Mode.MaxShapeLayers,
                dependencies.ShapeRegistry,
                dependencies.ShapeIdManager);

            return new DropCutterSimulationFactory(config, dependencies.ShapeRegistry, cut);
        }
    }
}