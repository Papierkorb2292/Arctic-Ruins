using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace DiagonalCutter.ArcticCutter
{
    internal class ArcticCutterFactoryBuilder
        : IBuildingSimulationFactoryBuilder<ArcticCutterSimulation, FullCutterSimulationState,
            ICutterConfiguration>
    {
        public IFactory<FullCutterSimulationState, ArcticCutterSimulation> BuildFactory(
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

            return new ArcticCutterSimulationFactory(config, dependencies.ShapeRegistry, cut);
        }
    }
}