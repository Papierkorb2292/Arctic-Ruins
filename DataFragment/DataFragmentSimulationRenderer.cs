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
        
        public DataFragmentSimulationRenderer(
            IMapModel map,
            IBuildingSoundManager soundManager,
            IShapeRegistry shapeRegistry) : base(map)
        {
        }

        public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            var baseRotation = options.AnimationSimulationTime_G * 22.5f;
            if (entity.Simulation.RewardUnlockSimulationTime < 0)
            {
                if(!entity.Simulation.State.UnlockedReward)
                    DrawDataCube(entity, baseRotation, options);
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
    }
}
