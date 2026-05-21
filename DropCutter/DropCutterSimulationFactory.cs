using Core.Factory;

namespace DiagonalCutter.DropCutter
{
    public class DropCutterSimulationFactory: IFactory<FullCutterSimulationState, DropCutterSimulation>
    {
        public readonly ICutterConfiguration CutterConfiguration;
        public IShapeRegistry ShapeIdRegistry;
        public ShapeOperationCut CutOperation;

        public DropCutterSimulationFactory(
            ICutterConfiguration dropCutterConfiguration,
            IShapeRegistry shapeIdRegistry,
            ShapeOperationCut cutOperation)
        {
            this.CutterConfiguration = dropCutterConfiguration;
            this.ShapeIdRegistry = shapeIdRegistry;
            this.CutOperation = cutOperation;
        }

        public DropCutterSimulation Produce(FullCutterSimulationState state)
        {
            return new DropCutterSimulation(state, CutterConfiguration, ShapeIdRegistry, CutOperation);
        }
    }
}