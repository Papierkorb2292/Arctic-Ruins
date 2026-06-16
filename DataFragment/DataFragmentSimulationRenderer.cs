using Game.Core.Coordinates;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

namespace ArcticRuins.DataFragment
{
    [UsedImplicitly]
    public class DataFragmentSimulationRenderer
        : StatelessBuildingSimulationRenderer<DataFragmentSimulation, IDataFragmentDrawData>
    {
        // A corner of the cube points upwards
        private static readonly Quaternion BaseCubeRotation = Quaternion.FromToRotation(Vector3.one.normalized, Vector3.up); 
        private static readonly IMaterialReference IconMaterial = new MaterialReference()
        {
            _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("DataFragmentIconMat.mat")
        };
        
        public DataFragmentSimulationRenderer(
            IMapModel map,
            IBuildingSoundManager soundManager,
            IShapeRegistry shapeRegistry) : base(map)
        {
        }

        public override bool ShouldDraw(FrameDrawOptionsNoLOD options) => true;
        public override bool ShouldDraw(LODRenderConfig lod) => true;

        public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            var baseRotation = options.AnimationSimulationTime_G * 22.5f;
            if (entity.Simulation.RewardUnlockSimulationTime < 0)
            {
                if (!entity.Simulation.State.UnlockedReward)
                {
                    DrawDataCube(entity, baseRotation, options);
                    DrawResearchBillboard(entity, options);
                }
                return;
            }
            
            var elapsedTime = options.SimulationTime_G - entity.Simulation.RewardUnlockSimulationTime;
            switch (elapsedTime)
            {
                case > 1:
                    return;
                case > 0.5:
                    var delta = elapsedTime - 0.5f;
                    DrawDataCube(entity, baseRotation + (float)(delta * 720), options, (float)(1 - 2*delta));
                    break;
                default:
                    DrawDataCube(entity, baseRotation, options, 1 + Mathf.Sin((float)elapsedTime * Mathf.PI * 2) * 0.2f);
                    if(elapsedTime < 0.25f)
                        DrawResearchBillboard(entity, options, (float)(1 - elapsedTime * 4));
                    break;
            }
        }
        
        private static void DrawDataCube(in Entity entity, float rotationDegrees, FrameDrawOptions options, float scale = 1)
        {
            if (!entity.DrawData.DataCubeMesh.TryGet(options.LOD.BuildingLOD, out var dataCubeMesh)) return;
            var material = options.Theme.BaseResources.BuildingMaterial[options.LOD.BuildingMaterialLOD];
            var matrix = Matrix4x4.TRS(
                entity.Transform.Position.ToCenter_W() + new WorldVector(0, 0,
                    0.7f + Mathf.Sin(options.AnimationSimulationTime_G / 4f * Mathf.PI) * 0.1f),
                Quaternion.AngleAxis(rotationDegrees, Vector3.up) * BaseCubeRotation,
                new Vector3(0.14f, 0.14f, 0.14f) * scale
            );
            options.Renderers.Buildings.Add(dataCubeMesh, material, matrix);
        }

        private static void DrawResearchBillboard(in Entity entity, FrameDrawOptions options, float scale = 1)
        {
            var chunk = entity.Transform.Position.ToChunkCoordinate();
            var stormRenderer = ArcticRuinsMod.Instance.StormRenderer;
            if (stormRenderer != null && stormRenderer.IsChunkLocked(chunk))
                return;
            var position = chunk.ToCenter_W() + new WorldVector(0, 0, 10f);
            var camDist = Mathf.Min((options.Viewport.MainCamera.transform.position - (Vector3)position).magnitude, 4000f);
            var s = Vector3.one * (camDist * 0.1f * (float) (1.0 + HUDTheme.PulseAnimation() * 0.30000001192092896)) * scale;
            options.Renderers.Buildings.Add(GeometryHelpers.BillboardMesh, IconMaterial, Matrix4x4.TRS(
                position + new WorldVector(0, 0, camDist / 4f),
                options.Viewport.MainCamera.transform.rotation,
                s
            ));
        }
    }
}
