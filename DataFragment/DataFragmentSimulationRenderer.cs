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
            DrawDataCube(entity, options);
        }
        
        private static void DrawDataCube(in Entity entity, FrameDrawOptions options)
        {
            if (!entity.DrawData.DataCubeMesh.TryGet(options.LOD.BuildingLOD, out var dataCubeMesh)) return;
            var material = options.Theme.BaseResources.BuildingMaterial[options.LOD.BuildingMaterialLOD];
            options.Renderers.Buildings.Add(dataCubeMesh, material, Matrix4x4.TRS(entity.Transform.Position.ToCenter_W() + new WorldVector(0, 0, 0.5f), Quaternion.AngleAxis(options.AnimationSimulationTime_G * 20, Vector3.up) * BaseCubeRotation, new Vector3(0.14f, 0.14f, 0.14f) ));
        }
    }
}
