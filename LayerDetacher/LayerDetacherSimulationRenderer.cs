using System;
using System.IO;
using Game.Core.Coordinates;
using Game.Core.Simulation;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

namespace ArcticRuins.LayerDetacher;

[UsedImplicitly]
public class LayerDetacherSimulationRenderer
    : StatelessBuildingSimulationRenderer<LayerDetacherSimulation, ILayerDetacherDrawData>
{
    private readonly IShapeRegistry _shapeRegistry;
    private static float _rotationOvershootDegrees = 13;
    
    public LayerDetacherSimulationRenderer(
        IMapModel map,
        IBuildingSoundManager soundManager,
        IShapeRegistry shapeRegistry) : base(map)
    {
        _shapeRegistry = shapeRegistry;
    }

    public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
    {
        LayerDetacherSimulation simulation = entity.Simulation;

        DrawBeltItem(entity.Transform, options, simulation.InputLane, entity.DrawData.InputLaneRenderingDefinition);
        DrawLeftOutputLane(entity,  options, simulation.LeftOutputLane);
        DrawBeltItem(entity.Transform, options, simulation.RightOutputLane,
            entity.DrawData.RightOutputLaneRenderingDefinition);

        var lowerLayerCount = 0;
        if (simulation.RightProcessingLane.Item is ShapeItem shape)
        {
            lowerLayerCount = shape.Definition.Layers.Length;
        }
        
        DrawProcessingLane(entity, options, simulation.LeftProcessingLane, false, lowerLayerCount);
        DrawProcessingLane(entity, options, simulation.RightProcessingLane, true, lowerLayerCount);
    }

    private void DrawLeftOutputLane(in Entity entity, FrameDrawOptions options, BeltLane lane)
    {
        if (!lane.HasItem)
            return;
        var overshootDuration = Ticks.FromMilliSeconds(200);
        var progressTicks = lane.Progress_S / (LaneConstants.ItemSpacing / lane.Duration_T);
        if (progressTicks > overshootDuration)
            progressTicks = overshootDuration;
        var overshootProgress = Ticks.Ratio(progressTicks, overshootDuration);
        var angle = Mathf.Sin(overshootProgress * Mathf.PI) * -_rotationOvershootDegrees;
        var beltItems = options.Renderers.BeltItems;
        var pos_L = entity.DrawData.LeftOutputLaneRenderingDefinition.GetPosFromProgress(lane.Progress) + new LocalVector(0.0f, 0.0f, beltItems.BeltShapeHeight)
            + /*0.33f*/ 0.5f * 0.5f * new LocalVector(0, Mathf.Cos(Mathf.Deg2Rad * angle) - 1, -Mathf.Sin(Mathf.Deg2Rad * angle));
        var translation = (pos_L ) * entity.Transform;
        options.Renderers.Shapes.Add(beltItems.GetDrawData(lane.Item, options.LOD.ShapeLOD), CalculateFlyingShapeMatrix(translation, entity.Transform.Rotation, angle, 1));
    }

    private void DrawProcessingLane(in Entity entity, FrameDrawOptions options, DelayBeltLane lane, bool isLowerLayers, int lowerLayerCount)
    {
        if (!lane.HasItem)
            return;
        var fallDuration = Ticks.FromMilliSeconds(600);
        var progressTicks = lane.Progress_T;
        var durationTicks = lane.Duration_T;
        var remainingTicks = durationTicks - progressTicks;
        if(remainingTicks > fallDuration)
        {
            if (isLowerLayers || lowerLayerCount == 0)
            {
                DrawBeltItem(entity.Transform, options, entity.Simulation.State.LastProcessedShape, new LocalVector(0));
            }
            return;
        }

        var totalProgress = 1 - Ticks.Ratio(remainingTicks, fallDuration);

        // Up until 0.25f, the top layer is drawn as part of the lower layers (unless it's the only layer), because otherwise the gap between the shape contents would suddenly change 
        if (totalProgress < 0.25f && !isLowerLayers && lowerLayerCount != 0) return;
        
        var progress = AdjustLowerLayersProgress(totalProgress, isLowerLayers);
        var beltItems = options.Renderers.BeltItems;
        var pivotRotation = CalculateRotationAroundPivot(progress);
        var pivotRotationVert = new LocalVector(0, -pivotRotation.z, pivotRotation.y);
        var translation = pivotRotationVert * beltItems.BeltShapeHeight + 0.5f * (pivotRotation + LocalVector.North);
        float scale = 1;
        MeshMaterialCombination meshMaterial;
        if (!isLowerLayers)
        {
            var topLayerGrowthProgress = progress <= 0.25f ? 0 : (progress - 0.25f) / 0.75f; 
            translation += pivotRotationVert * Mathf.Lerp(beltItems.ShapeRenderer.GetShapeLayerHeight(lowerLayerCount), -2 * beltItems.BeltShapeHeight, topLayerGrowthProgress);
            translation.z += 2 * (topLayerGrowthProgress * -topLayerGrowthProgress + topLayerGrowthProgress); // Add a quadratic function with roots 0 and 1 to the height
            scale = Mathf.Lerp(beltItems.ShapeRenderer.GetShapeLayerScale(lowerLayerCount), 1, topLayerGrowthProgress);
            // Without baseplate
            meshMaterial = beltItems.ShapeRenderer.GetDrawData(((ShapeItem)lane.Item).Definition, options.LOD.ShapeLOD);
            translation.z += beltItems.ShapeRenderer.SupportMeshHeight;
        }
        else
        {
            meshMaterial = beltItems.GetDrawData(totalProgress < 0.25f ? entity.Simulation.State.LastProcessedShape : lane.Item, options.LOD.ShapeLOD);
        }

        var matrixTransform = CalculateFlyingShapeMatrix(translation * entity.Transform, entity.Transform.Rotation, CalculateRotationAroundSelfDeg(progress), scale);
        
        options.Renderers.Shapes.Add(meshMaterial, matrixTransform);
        if (!isLowerLayers && progress > 0.25f)
        {
            // Render a second inverted shape, because shapes don't have a bottom
            options.Renderers.Shapes.Add( beltItems.ShapeRenderer.GetDrawData(FlipShape(((ShapeItem)lane.Item).Definition), options.LOD.ShapeLOD), CalculateBottomDuplicateMatrix(matrixTransform, options));
        }
    }

    private static float AdjustLowerLayersProgress(float progress, bool isLowerLayers)
    {
        // Lower layers slowly rotate back into place
        return isLowerLayers && progress > 0.25f ? (1 - progress) / 3 : progress;
    }

    private static LocalVector CalculateRotationAroundPivot(float progress)
    {
        var angle = progress switch
        {
            <= 0.25f => Mathf.Lerp(0, 0.25f * Mathf.PI, progress / 0.25f),
            _ => Mathf.Lerp(0.25f * Mathf.PI, Mathf.PI, (progress - 0.25f) / 0.75f),
        };
        return new LocalVector(0, Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float CalculateRotationAroundSelfDeg(float progress)
    {
        return progress switch
        {
            <= 0.25f => (progress / 0.25f) * -45,
            _ => Mathf.Lerp(-45, -360, (progress - 0.25f) / 0.75f),
        };
    }

    private static Matrix4x4 CalculateFlyingShapeMatrix(WorldCoordinate translation, GridRotation buildingRot, float angle, float scale)
    {
        var quaternion = buildingRot.Value switch
        {
            GridRotation.Serializable.RotateCW => FastMatrix.RotateZAngle(Angle.FromDegrees(angle)),
            GridRotation.Serializable.RotateCCW => FastMatrix.RotateZAngle(Angle.FromDegrees(-angle)),
            GridRotation.Serializable.Rotate180 => FastMatrix.RotateXAngle(angle),
            _ => FastMatrix.RotateXAngle(-angle),
        };
        var rotation = Matrix4x4.Rotate(quaternion);
        return new Matrix4x4
        {
            m00 = rotation.m00 * scale, m01 = rotation.m01, m02 = rotation.m02 * scale, m03 = translation.x,
            m10 = rotation.m10 * scale, m11 = rotation.m11, m12 = rotation.m12 * scale, m13 = translation.z,
            m20 = rotation.m20 * scale, m21 = rotation.m21, m22 = rotation.m22 * scale, m23 = -translation.y,
            m30 = 0.0f,                 m31 = 0.0f,         m32 = 0.0f,                 m33 = 1f
        };
    }

    private static Matrix4x4 CalculateBottomDuplicateMatrix(Matrix4x4 matrix, FrameDrawOptions options)
    {
        var offset = matrix.GetColumn(1) * options.Renderers.BeltItems.ShapeRenderer.GetShapeLayerHeight(1);
        
        // Invert the y-axis and invert the u-axis (so culling works, but also means we need to generate a new shape with flipped content)
        return new Matrix4x4
        {
            m00 = matrix.m00, m01 = matrix.m01 * -1, m02 = matrix.m02 * -1, m03 = matrix.m03 + offset.x,
            m10 = matrix.m10, m11 = matrix.m11 * -1, m12 = matrix.m12 * -1, m13 = matrix.m13 + offset.y,
            m20 = matrix.m20, m21 = matrix.m21 * -1, m22 = matrix.m22 * -1, m23 = matrix.m23 + offset.z,
            m30 = 0.0f,       m31 = 0.0f,            m32 = 0.0f,            m33 = 1f
        };
    }

    private ShapeDefinition FlipShape(ShapeDefinition shape)
    {
        var flippedHash = string.Create(8, shape.Hash, (span, hash) =>
        {
            span[0] = hash[2];
            span[1] = hash[3];
            span[2] = hash[0];
            span[3] = hash[1];
            span[4] = hash[6];
            span[5] = hash[7];
            span[6] = hash[4];
            span[7] = hash[5];
        });
        var shapeRegistry = (ShapeRegistry)_shapeRegistry;
        var shapeId = shapeRegistry.ShapeIdManager.Resolve(flippedHash);
        return shapeRegistry.GetDefinition(shapeId);
    }
}