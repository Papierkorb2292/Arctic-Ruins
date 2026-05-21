namespace DiagonalCutter.DropCutter
{
    public class DropCutterSimulation : FullCutterSimulation
    {
        public DropCutterSimulation(FullCutterSimulationState state, ICutterConfiguration cutterConfiguration, IShapeRegistry shapeRegistry, ShapeOperationCut cutOp) : base(state, cutterConfiguration, shapeRegistry, cutOp)
        {
        }
    }
}