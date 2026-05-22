using Core.Factory;

namespace ArcticRuins.ArcticCutter
{
    public class ArcticCutterSimulationFactory: IFactory<FullCutterSimulationState, ArcticCutterSimulation>
    {
        public readonly ICutterConfiguration CutterConfiguration;
        public IShapeRegistry ShapeIdRegistry;
        public ShapeOperationCut CutOperation;

        public ArcticCutterSimulationFactory(
            ICutterConfiguration dropCutterConfiguration,
            IShapeRegistry shapeIdRegistry,
            ShapeOperationCut cutOperation)
        {
            this.CutterConfiguration = dropCutterConfiguration;
            this.ShapeIdRegistry = shapeIdRegistry;
            this.CutOperation = cutOperation;
        }

        public ArcticCutterSimulation Produce(FullCutterSimulationState state)
        {
            return new ArcticCutterSimulation(state, CutterConfiguration, ShapeIdRegistry, CutOperation);
        }
    }
}