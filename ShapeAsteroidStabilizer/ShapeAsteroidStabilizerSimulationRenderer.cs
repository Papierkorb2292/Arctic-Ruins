using Game.Core.Coordinates;
using Game.Core.Simulation;
using JetBrains.Annotations;
using ShapezShifter.Kit;
using UnityEngine;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    [UsedImplicitly]
    public class ShapeAsteroidStabilizerSimulationRenderer
        : StatelessBuildingSimulationRenderer<ShapeAsteroidStabilizerSimulation, IShapeAsteroidStabilizerDrawData>
    {
        private static float DefaultHammerAngle = 45f;
        
        public ShapeAsteroidStabilizerSimulationRenderer(
            IMapModel map,
            IBuildingSoundManager soundManager,
            IShapeRegistry shapeRegistry) : base(map)
        {
        }

        public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            var simulation = entity.Simulation;
            var transform = entity.Transform;
            DrawBeltItem(in transform, options, simulation.InputLane, entity.DrawData.InputLaneRenderingDefinition);
            if(!simulation.ProcessingLane.HasItem || simulation.ProcessingLane.Item is not ShapeItem item)
            {
                DrawHammer(in entity, options, DefaultHammerAngle);
                return;
            }

            var progress = simulation.ProcessingLane.Progress;
            var hammerAngle = progress switch
            {
                <= 0.1f => Mathf.Lerp(DefaultHammerAngle, 90, Mathf.InverseLerp(0, 0.1f, progress)),
                <= 0.5f => 90,
                _ => Mathf.Lerp(90, DefaultHammerAngle, Mathf.InverseLerp(0.5f, 1, progress))
            };
            DrawHammer(in entity, options, hammerAngle);

            var isShapeAccepted = simulation.AllowedHashes.Contains(item.Definition.Hash);

            if (isShapeAccepted)
            {
                // Animate shape dropping down into the platform
                var shapeBaseTranslation = entity.DrawData.ProcessingLaneRenderingDefinition.GetPosFromProgress(
                    Mathf.InverseLerp(0.09f, 0.45f, progress));
                DrawBeltItem(in transform, options, item, shapeBaseTranslation);
            }
            else
            {
                // Dissolve shape
                var beltItems = options.Renderers.BeltItems;
                var localTranslation = entity.DrawData.ProcessingLaneRenderingDefinition.ItemStartPos_L +
                                       new LocalVector(0.0f, 0.0f, beltItems.BeltShapeHeight);
                var translation = localTranslation * transform;
                var opacity = Mathf.Lerp(1, 0, Mathf.InverseLerp(0.08f, 0.3f, progress));
                DrawWithOpacity(item, FastMatrix.Translate(in translation), options, opacity);
            }
        }

        private static void DrawWithOpacity(ShapeItem shapeItem, Matrix4x4 transform, FrameDrawOptions options, float opacity)
        {
            var shapeRenderer = options.Renderers.BeltItems.ShapeRenderer;
            var alphaBlock = MaterialPropertyHelpers.CreateAlphaBlock(opacity);
            options.Renderers.RegularNonInstanced.DrawMesh(shapeRenderer.GetDrawData(shapeItem, options.LOD.ShapeLOD).Mesh, shapeRenderer.ShapeMaterialDissolve, transform, RenderCategory.BuildingsDynamic, alphaBlock);
        }

        private static void DrawHammer(in Entity entity, FrameDrawOptions options, float angleDegrees)
        {
            int buildingMaterialLod = options.LOD.BuildingMaterialLOD;
            if(!entity.DrawData.HammerMesh.TryGet(buildingMaterialLod, out var hammerMesh)) return;
            var transform = entity.Transform;
            var relativePos = new LocalVector(1, 0, 0.4f);
            options.Renderers.Buildings.Add(hammerMesh,
                options.Theme.BaseResources.BuildingMaterial[buildingMaterialLod],
                Matrix4x4.TRS(
                    relativePos * transform,
                    FastMatrix.RotateY(transform.Rotation) * FastMatrix.RotateZAngle(Angle.FromDegrees(angleDegrees)), 
                    Vector3.one));
        }
    }
}
