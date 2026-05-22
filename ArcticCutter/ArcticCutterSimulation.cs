namespace ArcticRuins.ArcticCutter
{
    public class ArcticCutterSimulation : FullCutterSimulation
    {
        public ArcticCutterSimulation(FullCutterSimulationState state, ICutterConfiguration cutterConfiguration, IShapeRegistry shapeRegistry, ShapeOperationCut cutOp) : base(state, cutterConfiguration, shapeRegistry, cutOp)
        {
        }
    }
}