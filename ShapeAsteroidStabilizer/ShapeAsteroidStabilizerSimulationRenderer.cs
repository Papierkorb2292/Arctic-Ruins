using Game.Core.Coordinates;
using Game.Core.Simulation;
using JetBrains.Annotations;
using UnityEngine;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    [UsedImplicitly]
    public class ShapeAsteroidStabilizerSimulationRenderer
        : StatelessBuildingSimulationRenderer<ShapeAsteroidStabilizerSimulation, IShapeAsteroidStabilizerDrawData>
    {
        public ShapeAsteroidStabilizerSimulationRenderer(
            IMapModel map,
            IBuildingSoundManager soundManager,
            IShapeRegistry shapeRegistry) : base(map)
        {
        }

        public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            var simulation = entity.Simulation;
            DrawBeltItem(entity.Transform, options, simulation.InputLane, entity.DrawData.InputLaneRenderingDefinition);
            if(!simulation.ProcessingLane.HasItem || simulation.ProcessingLane.Item is not ShapeItem item) return;
            var beltItems = options.Renderers.BeltItems;
            var translation = (entity.DrawData.ProcessingLaneRenderingDefinition.ItemStartPos_L + new LocalVector(0.0f, 0.0f, beltItems.BeltShapeHeight)) * entity.Transform;
            var opacity = simulation.ProcessingLane.Progress < 0.75f ? 1 : (1 - simulation.ProcessingLane.Progress) * 4;
            DrawWithOpacity(item, FastMatrix.Translate(in translation), options, opacity); //TODO(opt): Make shape fall down instead of fading it out
        }

        private static void DrawWithOpacity(ShapeItem shapeItem, Matrix4x4 transform, FrameDrawOptions options, float opacity)
        {
            var shapeRenderer = options.Renderers.BeltItems.ShapeRenderer;
            var alphaBlock = MaterialPropertyHelpers.CreateAlphaBlock(opacity);
            options.Renderers.RegularNonInstanced.DrawMesh(shapeRenderer.GetDrawData(shapeItem, options.LOD.ShapeLOD).Mesh, shapeRenderer.ShapeMaterialDissolve, transform, RenderCategory.BuildingsDynamic, alphaBlock);
        }
    }
}
