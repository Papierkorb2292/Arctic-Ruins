using Core.Factory;

namespace ArcticRuins.LayerDetacher;

public class LayerDetacherSimulationFactory(
    ILayerDetacherConfiguration configuration,
    IShapeRegistry shapeIdRegistry,
    ShapeOperationLayerDetach layerDetachOperation)
    : IFactory<LayerDetacherSimulationState, LayerDetacherSimulation>
{
    public LayerDetacherSimulation Produce(LayerDetacherSimulationState state)
    {
        return new LayerDetacherSimulation(state, configuration, shapeIdRegistry, layerDetachOperation);
    }
}