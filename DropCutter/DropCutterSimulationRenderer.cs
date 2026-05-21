using Game.Content.Features.Predictions.Processing;
using Game.Core.Coordinates;
using Game.Core.Simulation;
using JetBrains.Annotations;

namespace DiagonalCutter.DropCutter
{
    [UsedImplicitly]
    public class DropCutterSimulationRenderer
        : StatelessBuildingSimulationRenderer<DropCutterSimulation, IDropCutterDrawData>
    {
        public DropCutterSimulationRenderer(
            IMapModel map,
            IBuildingSoundManager soundManager,
            IShapeRegistry shapeRegistry) : base(map)
        {
        }

        public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            DropCutterSimulation simulation = entity.Simulation;

            DrawBeltItem(entity.Transform, options, simulation.InputLane, entity.DrawData.InputLaneRenderingDefinition);
            DrawBeltItem(entity.Transform, options, simulation.LeftOutputLane,
                entity.DrawData.LeftOutputLaneRenderingDefinition);
            DrawBeltItem(entity.Transform, options, simulation.RightOutputLane,
                entity.DrawData.RightOutputLaneRenderingDefinition);

            DrawProcessingLane(entity, options, simulation.LeftLane,
                entity.DrawData.LeftLaneRenderingDefinition);
            DrawProcessingLane(entity, options, simulation.RightLane,
                entity.DrawData.RightLaneRenderingDefinition);
        }

        private void DrawProcessingLane(in Entity entity, FrameDrawOptions options, DelayBeltLane lane,
            IBeltLaneRendererDefinition definition)
        {
            if (!lane.HasItem)
                return;
            var fallDuration = Ticks.FromMilliSeconds(200);
            var progressTicks = lane.Progress_T;
            var durationTicks = lane.Duration_T;
            var remainingTicks = durationTicks - progressTicks;
            LocalVector pos_L = remainingTicks < fallDuration
                ? definition.GetPosFromProgress(1 - Ticks.Ratio(remainingTicks, fallDuration))
                : definition.ItemStartPos_L;
            DrawBeltItem(entity.Transform, options, lane.Item, pos_L);
        }
    }
}
