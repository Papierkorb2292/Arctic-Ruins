using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace ArcticRuins.LayerDetacher;

internal class LayerDetacherFactoryBuilder
    : IBuildingSimulationFactoryBuilder<LayerDetacherSimulation, LayerDetacherSimulationState,
        ILayerDetacherConfiguration>
{
    public IFactory<LayerDetacherSimulationState, LayerDetacherSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out ILayerDetacherConfiguration config)
    {
        config = new LayerDetacherConfiguration(
            BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
            BuffableBeltDelay.DiscreteDuration.TwoPointFiveSeconds,
            new ResearchSpeedId("CutterSpeed"));

        var cut = new ShapeOperationLayerDetach(
            dependencies.Mode.MaxShapeLayers,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);

        return new LayerDetacherSimulationFactory(config, dependencies.ShapeRegistry, cut);
    }
}