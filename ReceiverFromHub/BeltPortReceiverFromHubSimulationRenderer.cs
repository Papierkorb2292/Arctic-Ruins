using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Core.Collections.Scoped;
using Core.Randomizing;
using Game.Buildings.BeltPort.Rendering.HubAnimation;
using Game.Core.Coordinates;
using Game.Core.Rendering.Culling;
using Game.Core.Simulation;
using ShapezShifter.SharpDetour;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace ArcticRuins.ReceiverFromHub;
public class BeltPortReceiverFromHubSimulationRenderer(
  IMapModel map,
  BeltPortSenderEntityMetaBuildingDefinition.DrawData drawData,
  StopperRenderer stopperRenderer,
  ISimulationSpeedReader simulationSpeed,
  IBuildingSoundManager soundManager)
  :
    StatefulTileSimulationRenderer<ConnectableBuildingSimulation, BeltPortReceiverFromHubSimulation,
      BeltPortReceiverFromHubSimulationRenderer.Data>(soundManager)
{
  private static ProfilerMarker _drawMarker = new("BeltPortReceiverFromHubSimulationRenderer.Draw");
  private static ProfilerMarker _processItemsMarker = new("BeltPortReceiverFromHubSimulationRenderer.ProcessItems");
  private FrameDrawOptions _cachedDrawOptions;
  private readonly ConsistentRandom _random = new(50);
  private readonly Dictionary<GlobalChunkCoordinate, VortexData> _animationsByVortexCoordinate = new();
  private readonly MetaHubInputAnimationParameters _animationParameters = Globals.Resources.HubInputAnimationParameters;

  public override bool ShouldDraw(FrameDrawOptionsNoLOD options) => !options.InOverviewMode;

  public override void Draw(FrameDrawOptionsNoLOD options, MapCullResult cullResult)
  {
    using(_drawMarker.Auto())
    {
      options.CloneIntoLOD(ref _cachedDrawOptions);
      base.Draw(options, cullResult);
      double simulationTimeG = simulationSpeed.SimulationTime_G;
      foreach(var (globalChunkCoordinate, vortexData) in _animationsByVortexCoordinate)
      {
        var chunkBounds = options.Theme.MapBoundsProvider.GetChunkBounds(globalChunkCoordinate);
        if(GeometryUtility.TestPlanesAABB(options.CameraPlanes, chunkBounds))
        {
          _cachedDrawOptions.LOD.Compute(math.distancesq(chunkBounds.center, options.CameraPosition_W), options.LODComputation, out var _);
          if (Application.isEditor && _animationParameters.InvalidateCache)
          {
            foreach (GlobalTileCoordinate key in vortexData.AnimationsByTile.Keys)
              vortexData.AnimationsByTile[key].ResetCache_SLOW(_animationParameters, vortexData.VortexCenter);
          }
          foreach (HubSlotAnimationData slotAnimationData in vortexData.AnimationsByTile.Values)
          {
            if (!slotAnimationData.SimulationIsPresent)
              SyncAnimationWithSimulation(slotAnimationData, _animationParameters, null);
            HubItemAnimationDrawer.DrawAnimations(slotAnimationData, _animationParameters, _cachedDrawOptions, simulationTimeG);
          }
        }
      }
      _animationParameters.InvalidateCache = false;
    }
  }

  public override Bounds ComputeBounds(ConnectableBuildingSimulation localizedSimulation)
  {
    var island = map.GetIsland(localizedSimulation.Transform.Position);
    return GlobalChunkBounds.From(island.LayoutQuery.ChunkLookup.GetChunkInformation().Where(chunk => chunk.IsBuildable).Select(chunk => chunk.Chunk_L.ToGlobal(island.Transform))).ToWorldBounds();
  }

  public override Data CreateDataForSimulation(
    ConnectableBuildingSimulation localizedSimulation,
    BeltPortReceiverFromHubSimulation simulation)
  {
    try
    {
      var transform = localizedSimulation.Transform.Rotate(GridRotation.Rotate180);
      WorldCoordinate center = ComputeBounds(localizedSimulation).center;
      var globalChunkCoordinate = center.ToGlobalChunkCoordinate();
      if (!_animationsByVortexCoordinate.TryGetValue(globalChunkCoordinate, out var vortexData))
      {
        vortexData = new VortexData(center);
        _animationsByVortexCoordinate.Add(globalChunkCoordinate, vortexData);
      }

      if (!vortexData.AnimationsByTile.TryGetValue(transform.Position, out var animationData))
      {
        animationData =
          new HubSlotAnimationData(_animationParameters, center, in transform, new BeltPortSenderEntityMetaBuildingDefinition.DrawData.AnimationCurvesGroup
          {
            // JumpLanes array may not be populated at this point(?) and I don't know what the numbers mean, so use hardcoded values idk
            HeightCurve = new AnimationCurve(new Keyframe(0, 0.34f)),
            RotationCurve = new AnimationCurve(new Keyframe(0, -1.5f))
          });
        animationData.Definition.Set(
          data => data.B1,
          animationData.Definition.B1 + (WorldVector)transform.Rotation.ToTileDirection() * 0.25f);
        animationData.Definition.Set(
          data => data.BezierStart,
          animationData.Definition.BezierStart + (WorldVector)transform.Rotation.ToTileDirection() * 0.25f);
        vortexData.AnimationsByTile.Add(transform.Position, animationData);
      }

      animationData.SimulationIsPresent = true;
      animationData.LastItemDeliveryCount = simulation.ItemDeliveryCount;
      return new Data(animationData);
    }
    catch (Exception e)
    {
      ArcticRuinsMod.Logger.Error?.LogException(e);
      throw;
    }
  }

  public override void CleanupDataForSimulation(
    ConnectableBuildingSimulation localizedSimulation,
    BeltPortReceiverFromHubSimulation simulation,
    Data data)
  {
    var occupiedTile = localizedSimulation.GetOccupiedTile(0);
    if(!_animationsByVortexCoordinate.TryGetValue(occupiedTile.ToChunkCoordinate(), out var vortexData) || !vortexData.AnimationsByTile.TryGetValue(occupiedTile, out var slotAnimationData))
      throw new MissingPrimaryKeyException();
    vortexData.AnimationsByTile.Remove(occupiedTile); // There's no reason to keep floating items around when the receiver is gone
    //TODO(opt): Make items fall down when receiver is removed
  }

  public override void OnDrawDynamic(
    in Entity entity,
    FrameDrawOptions options)
  {
    var transform = entity.LocalizedSimulation.Transform;
    SyncAnimationWithSimulation(entity.Data.AnimationData, _animationParameters, entity);
    DrawJumpItems(entity.Simulation, in transform, options, drawData.JumpLaneCurves[2], out var stopperRotation);
    stopperRenderer.DrawStoppers(in transform, options, stopperRotation);
  }
  
  private void DrawJumpItems(
    BeltPortReceiverFromHubSimulation simulation,
    in GlobalTileTransform receiverTransform,
    FrameDrawOptions options,
    BeltPortSenderEntityMetaBuildingDefinition.DrawData.AnimationCurvesGroup curves,
    out Angle stopperRotation)
  {
    var angle = stopperRenderer.GetStopperRotation(0.0f);
    if (simulation.OutputLane.HasItem)
    {
      var laneEndL = new LocalVector(2.5f, 0f, 0f);
      var progress = Mathf.Lerp(0.65f, 1, simulation.OutputLane.Progress);
      var transform = receiverTransform;
      transform -= TileVector.ByDirection(receiverTransform.Rotation.ToTileDirection()) * 2;
      var localVector = BeltPortRenderUtils.DrawItemWithCurves(in transform, options, simulation.OutputLane.Item,
        progress, BeltPortTransferSimulationRenderer.LaneBegin_L, laneEndL, curves);
      angle = Angle.FromDegrees(math.max(
        stopperRenderer.GetStopperRotation((float)(1.0 - ((double)laneEndL.x - (double)localVector.x))).Degrees,
        angle.Degrees));
    }

    stopperRotation = angle;
  }
  private void SyncAnimationWithSimulation(
    HubSlotAnimationData animations,
    MetaHubInputAnimationParameters animParams,
    Entity entity)
  {
    using (_processItemsMarker.Auto())
    {
      if (!animations.SimulationIsPresent)
        return;
      RemoveExpiredAnimations(animations, entity);
      AddPendingAnimations(animations, animParams, entity);
      UpdateAnimationTime(animations, entity);
    }
  }

  private static void RemoveExpiredAnimations(HubSlotAnimationData animations, Entity entity)
  {
    if(animations.Items.Count > entity.Simulation.VortexLane.ItemCount)
      animations.Items.RemoveRange(entity.Simulation.VortexLane.ItemCount, animations.Items.Count - entity.Simulation.VortexLane.ItemCount);
  }
  private void AddPendingAnimations(
    HubSlotAnimationData animations,
    MetaHubInputAnimationParameters animParams,
    Entity entity)
  {
    var simulation = entity.Simulation;
    var itemDeliveryCount1 = entity.Data.AnimationData.LastItemDeliveryCount;
    var itemDeliveryCount2 = simulation.ItemDeliveryCount;
    var vortexLane = simulation.VortexLane;
    if (itemDeliveryCount1 == itemDeliveryCount2 || vortexLane.ItemCount == 0)
      return;
    entity.Data.AnimationData.LastItemDeliveryCount = itemDeliveryCount2;
    using var collection = ScopedList<HubItemAnimation>.Get();
    // Add all shapes on the lane that haven't been added yet, including shapes that were already there when the world was loaded
    var num1 = math.min(itemDeliveryCount2 - itemDeliveryCount1, vortexLane.ItemCount);
    for (int index = 0; index < num1; ++index)
    {
      var itemOnBelt = vortexLane.Items[index];
      var seed = _random.NextFloatRange(-1000f, 1000f);
      var forwardAnimation = HubItemAnimation.From(itemOnBelt.Item, seed, animParams, 0);
      // Set added time to when the item will arrive at the receiver and use negative timescale to make the item go in reverse
      var backwardAnimation = new HubItemAnimation(forwardAnimation.Item, forwardAnimation.Seed, -1 * forwardAnimation.TimeScale, forwardAnimation.PosOffset, 0);
      collection.Add(backwardAnimation);
    }
    animations.Items.InsertRange(0, collection);
  }

  private void UpdateAnimationTime(
    HubSlotAnimationData animations,
    Entity entity)
  {
    // Predict the arrival time of each item and use it as `addedTime`. Continuously updating the animations prevents any desync with the belt that could otherwise occur. 
    var simulation = entity.Simulation;
    var vortexLane = simulation.VortexLane;
    var simulationTimeG = simulationSpeed.SimulationTime_G;
    var num1 = math.min(animations.Items.Count, vortexLane.ItemCount);
    var accumSteps = vortexLane.FirstItemDistance_S;
    for (int index = 0; index < num1; ++index)
    {
      var animation = animations.Items[index];
      var remainingSteps = vortexLane.Length_S - accumSteps;
      var remainingTime = Steps.Ratio(remainingSteps, vortexLane.StepsPerTick_ * Ticks.OneSecond);
      animation.AddedTime = simulationTimeG + remainingTime; 
      accumSteps += vortexLane.Items[index].NextItemDistance_S;
    }
  }

  public class Data(HubSlotAnimationData animationData) : StateData
  {
    public readonly HubSlotAnimationData AnimationData = animationData;
  }

  private class VortexData(WorldCoordinate vortexCenter)
  {
    public readonly WorldCoordinate VortexCenter = vortexCenter;
    public readonly Dictionary<GlobalTileCoordinate, HubSlotAnimationData> AnimationsByTile = new();
  }
}