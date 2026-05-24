using Core.Factory;
using Game.Content.Features.Predictions.Processing;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack.Predictions;

namespace ArcticRuins.LayerDetacher
{
    internal class LayerDetacherPredictionFactoryBuilder
        : IBuildingPredictionFactoryBuilder<Processing1In2OutPredictionSimulation>
    {
        public IFactory<Processing1In2OutPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
        {
            var op = new ShapeOperationLayerDetach(
                dependencies.Mode.MaxShapeLayers,
                dependencies.ShapeRegistry,
                dependencies.ShapeIdManager);
            return new Processing1In2OutPredictionSimulationFactory(op);
        }
    }
}
