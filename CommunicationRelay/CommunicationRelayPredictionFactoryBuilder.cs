using Core.Factory;
using Game.Content.Features.Predictions.Processing;
using Game.Content.Trash.Prediction;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack.Predictions;

namespace ArcticRuins.CommunicationRelay
{
    internal class ShapeAsteroidStabilizerPredictionFactoryBuilder
        : IBuildingPredictionFactoryBuilder<TrashPredictionSimulation>
    {
        public IFactory<TrashPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
        {
            return new LambdaFactory<TrashPredictionSimulation>(() => new TrashPredictionSimulation(1));
        }
    }
}
