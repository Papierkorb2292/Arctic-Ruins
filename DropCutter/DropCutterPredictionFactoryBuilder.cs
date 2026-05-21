using Core.Factory;
using Game.Content.Features.Predictions.Processing;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack.Predictions;

namespace DiagonalCutter.DropCutter
{
    internal class DropCutterPredictionFactoryBuilder
        : IBuildingPredictionFactoryBuilder<Processing1In2OutPredictionSimulation>
    {
        public IFactory<Processing1In2OutPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
        {
            var op = new ShapeOperationCut(
                dependencies.Mode.MaxShapeLayers,
                dependencies.ShapeRegistry,
                dependencies.ShapeIdManager);
            return new Processing1In2OutPredictionSimulationFactory(op);
        }
    }
}
