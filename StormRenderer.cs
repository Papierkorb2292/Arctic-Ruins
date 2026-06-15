using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using Core.Collections.Scoped;
using Game.Core.Coordinates;
using Game.Placement.Data;
using Game.Placement.Processing;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using UnityEngine;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;
using Vector2 = System.Numerics.Vector2;

namespace ArcticRuins;

public class StormRenderer
{
    private static readonly IMeshReference Mesh = CreateStormMesh();
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
    private static readonly int ScaleId = Shader.PropertyToID("_Scale");
    private static readonly int TimeScaleId = Shader.PropertyToID("_TimeScale");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");
    private static readonly int HeightsId = Shader.PropertyToID("_Heights");
    private static readonly MaterialPropertyBlock HeightsBlock = new();
    private static Vector4 _lastHeightsParam = Vector4.zero;
    private const int StormChunkSize = 16;
    private const int StormTileSize = StormChunkSize * CoordinateConstants.TILES_PER_CHUNK;
    private const int StormChunksPerSuperChunk = CoordinateConstants.CHUNKS_PER_SUPER_CHUNK / StormChunkSize;
    private static Hook _cameraControllerUpdateHook;
    private static Hook _drawPendingBuildingSelectionHook;
    private static Hook _drawPendingIslandSelectionHook;
    private static Hook _preparePlacementDataHook;

    private readonly StormLayer[] _detailLayers;
    private readonly StormLayer[] _mapLayers;
    private readonly AsteroidProgressSystem _progressSystem;
    private readonly GameResourcesMap _gameResourcesMap;
    private readonly DelaunayHelper _delaunay;
    private readonly Dictionary<GlobalChunkCoordinate, float> _heights = new();
    private readonly Dictionary<GlobalChunkCoordinate, float> _targetHeights = new();
    private readonly HashSet<Point> _completedCircles = [];
    private readonly Dictionary<GlobalChunkCoordinate, float> _lastRevealedRatio = new();

    private StormRenderer(GameSessionOrchestrator orchestrator)
    {
        var stormChunks = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/StormChunksMat.mat") };
        var stormNoise = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/StormNoiseMat.mat") };
        var stormBackground = new MaterialReference { _Material = ArcticRuinsMod.Instance.AssetBundle.LoadAsset<Material>("Assets/AssetBundle/StormBackgroundMat.mat") };
        stormBackground._Material.color = new Color(0.9f, 0.95f, 1, 1);
        _detailLayers =
        [
            CreateLayer(stormBackground, 0, 1, 1, 0),
            CreateLayer(stormNoise, 0.1f * Mathf.PI, 0.7f, 1, 1),
            CreateLayer(stormChunks, 0.15f * Mathf.PI, 1, 1, 2),
            CreateLayer(stormChunks, -0.1f * Mathf.PI, 1.7f, 0.8f, 3),
            CreateLayer(stormNoise, -0.05f * Mathf.PI, 1, 1, 4),
            CreateLayer(stormChunks, 0.05f * Mathf.PI, 2f, 0.5f, 5),
            CreateLayer(stormChunks, -0.02f * Mathf.PI, 1.5f, 0.7f, 6),
        ];
        _mapLayers =
        [
            CreateLayer(stormBackground, 0, 1, 1, 0),
            CreateLayer(stormNoise, 0, 0.1f, 10, 1),
        ];
        _progressSystem = (AsteroidProgressSystem)orchestrator.SimulationSystems.First(system => system is AsteroidProgressSystem);
        _gameResourcesMap = orchestrator.ResourcesMap;
        _delaunay = new DelaunayHelper(() => _gameResourcesMap.SuperChunks
            .SelectMany(sc => sc.AllResources)
            .OfType<ShapeMapResourceSource>()
            .Select(patch => patch.CenterOfMass_GC)
            .Select(chunkCoord => new Vector2(chunkCoord.x, chunkCoord.y)));
        _progressSystem.OnAsteroidProgressUpdate.Register((coord, data) => RevealPatch(coord, data, _targetHeights));
        InitializeStormHeight();
        FilterLockedSelections(orchestrator);
    }

    public static void Register()
    {
        _cameraControllerUpdateHook = new Hook(
            DetourHelper.GetRuntimeMethod((Expression<Action<CameraController, float>>)((controller, deltaTime) => controller.Update_ApplyAngleAndZoom(deltaTime))),
            (Action<Action<CameraController, float>, CameraController, float>)((original, cameraController, deltaTime) =>
            {
                ArcticRuinsMod.Instance.StormRenderer?.ZoomCameraOutsideStorm(cameraController);
                original(cameraController, deltaTime);
            }));
        _drawPendingBuildingSelectionHook = DetourHelper.CreatePrefixHook<HUDBuildingMassSelection, FrameDrawOptions, IReadOnlyCollection<BuildingModel>, HUDMassSelectionSelectionType>(
            (selection, options, buildings, type) => selection.Draw_PendingSelection(options, buildings, type),
            (_, options, buildings, type) =>
            {
                var stormRenderer = ArcticRuinsMod.Instance.StormRenderer;
                if (stormRenderer != null && buildings is HashSet<BuildingModel> set)
                    set.RemoveWhere(island => stormRenderer.IsChunkLocked(island.Transform.Position.ToChunkCoordinate()));
                return (options, buildings, type);
            }
        );
        _drawPendingIslandSelectionHook = DetourHelper.CreatePrefixHook<HUDIslandMassSelection, FrameDrawOptions, IReadOnlyCollection<IslandModel>, HUDMassSelectionSelectionType>(
            (selection, options, islands, type) => selection.Draw_PendingSelection(options, islands, type),
            (_, options, islands, type) =>
            {
                var stormRenderer = ArcticRuinsMod.Instance.StormRenderer;
                if (stormRenderer != null && islands is HashSet<IslandModel> set)
                    set.RemoveWhere(island => stormRenderer.IsChunkLocked(island.Position));
                return (options, islands, type);
            }
        );
        _preparePlacementDataHook = DetourHelper.CreatePostfixHook<EntityPlacementRunner, IEntityPlacer>(
            (runner, placer) => runner.PreparePlacementData(placer),
            (runner, _) =>
            {
                var stormRenderer = ArcticRuinsMod.Instance.StormRenderer;
                if (stormRenderer == null)
                    return;
                // Replace placement data to filter out all locked chunks
                runner.CurrentPlacementData = new FilterLockedPlacementData(stormRenderer, runner.CurrentPlacementData);
            });
    }

    public static void Dispose()
    {
        _cameraControllerUpdateHook.Dispose();
        _drawPendingBuildingSelectionHook.Dispose();
        _drawPendingIslandSelectionHook.Dispose();
        _preparePlacementDataHook.Dispose();
    }

    public static StormRenderer HookRenderer(GameSessionOrchestrator orchestrator)
    {
        if (!ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(orchestrator.Mode.Scenario))
            return null;
        var stormRenderer = new StormRenderer(orchestrator);
        orchestrator.Draw.Hooks.OnDrawSuperChunk += stormRenderer.Draw;
        orchestrator.Draw.Hooks.OnDrawMap += (options, _, _) =>
        {
            stormRenderer.ZoomCameraOutsideStorm(orchestrator.PlayerInteractionOrchestrator.CameraController);
            stormRenderer.UpdateAnimatedHeights(options.DeltaTime);
        };
        return stormRenderer;
    }

    private void Draw(FrameDrawOptionsNoLOD options, MapSuperChunk superChunk)
    {
        var layers = options.InOverviewMode ? _mapLayers : _detailLayers;
        for (int x = 0; x < StormChunksPerSuperChunk; x++)
        {
            for (int y = 0; y < StormChunksPerSuperChunk; y++)
            {
                var cornerCoord = superChunk.Origin_GC + new ChunkVector(x * StormChunkSize, y * StormChunkSize, 0);  
                var chunkCoord = cornerCoord + new ChunkVector(StormChunkSize / 2, StormChunkSize / 2, 1);
                WorldCoordinate pos = chunkCoord.ToOrigin_W() + 50 * WorldVector.Up;
                
                // The height component of each vertex is selected by computing the dot product with the 4d uv (is that still called uv?)
                var heights = new Vector4(
                    _heights.GetValueOrDefault(cornerCoord + new ChunkVector(StormChunkSize, StormChunkSize, 0), 0),
                    _heights.GetValueOrDefault(cornerCoord + new ChunkVector(StormChunkSize, 0, 0), 0),
                    _heights.GetValueOrDefault(cornerCoord + new ChunkVector(0, 0, 0), 0),
                    _heights.GetValueOrDefault(cornerCoord + new ChunkVector(0, StormChunkSize, 0), 0)
                );
                if (Mathf.Approximately(heights.ManhattanDistToOrigin(), 4))
                {
                    continue; // All corners at lowest height
                }
                if (!Mathf.Approximately((heights - _lastHeightsParam).ManhattanDistToOrigin(), 0))
                {
                    _lastHeightsParam = heights;
                    HeightsBlock.SetVector(HeightsId, heights);
                }

                for (int i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    options.Renderers.UINonInstanced.DrawMesh(layer.Mesh, layer.Material, FastMatrix.TranslateScale(pos + layer.Offset, StormTileSize), RenderCategory.Misc, HeightsBlock);
                }
            }
        }
    }

    private void InitializeStormHeight()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        foreach (var chunk in _gameResourcesMap.SuperChunks)
        {
            foreach (var patch in chunk.AllResources)
            {
                if(ArcticRuinsMod.Instance.SaveData.Asteroids.TryGetValue(patch.Origin_GC, out var data))
                    RevealPatch(patch.Origin_GC, data, _heights);
            }
        }
        // Technically zero isn't actually part of the graph, but this should work anyway
        var startCircles = _delaunay.GetCirclesAroundPoint(Vector2.Zero);
        var startPolygon = _delaunay.DelaunayPolygonAroundPoint(Vector2.Zero); 
        for (int i = 0; i < startCircles.Count; i++)
            AddCompletedCircle(startCircles[i], Vector2.Zero, startPolygon[i], startPolygon[(i + 1) % startPolygon.Count], _heights);
        ArcticRuinsMod.Logger.Info!.LogFormat("InitializeStormHeight took {0}", stopwatch.Elapsed);
    }

    private void RevealPatch(GlobalChunkCoordinate patchOrigin, SaveData.AsteroidData data, Dictionary<GlobalChunkCoordinate, float> heightsMap)
    {
        if (data.SuppliedShapes == 0)  return;
        if (data.IsComplete())
        {
            var patch = _gameResourcesMap.GetResourceAt_GC(patchOrigin);
            var centerOfMass = new Vector2(patch.CenterOfMass_GC.x, patch.CenterOfMass_GC.y);
            var circles = _delaunay.GetCirclesAroundPoint(centerOfMass);
            var polygon = _delaunay.DelaunayPolygonAroundPoint(centerOfMass);
            for (int i = 0; i < circles.Count; i++)
            {
                AddCompletedCircle(circles[i], centerOfMass, polygon[i], polygon[(i + 1) % polygon.Count], heightsMap);
            }
        }
        else
        {
            var ratio = (float)data.SuppliedShapes / data.TotalRequirement;
            var prevRatio = _lastRevealedRatio.GetValueOrDefault(patchOrigin, 0);
            if (ratio - prevRatio < 0.05f)
                return;
            var patch = _gameResourcesMap.GetResourceAt_GC(patchOrigin);
            var centerOfMass = new Vector2(patch.CenterOfMass_GC.x, patch.CenterOfMass_GC.y);
            var circles = _delaunay.GetCirclesAroundPoint(centerOfMass);
            var polygon = _delaunay.DelaunayPolygonAroundPoint(centerOfMass);
            for (int i = 0; i < circles.Count; i++)
            {
                var circle = circles[i];
                var centerPoint = new Point(Mathf.RoundToInt(circle.Center.X), Mathf.RoundToInt(circle.Center.Y));
                if (_completedCircles.Contains(centerPoint))
                    continue;
                // Don't add it to completed circles yet, since it's still being scaled up
                RevealCircle(new DelaunayHelper.Circle(
                    Vector2.Lerp(centerOfMass, circle.Center, ratio),
                        circle.RadiusSqr * ratio * ratio,
                        circle.Radius * ratio
                    ), centerOfMass, Vector2.Lerp(centerOfMass, polygon[i], ratio),
                    Vector2.Lerp(centerOfMass, polygon[(i + 1) % polygon.Count], ratio),
                    heightsMap);
            }

            _lastRevealedRatio[patchOrigin] = ratio;
        }
    }

    private void AddCompletedCircle(DelaunayHelper.Circle circle, Vector2 a, Vector2 b, Vector2 c, Dictionary<GlobalChunkCoordinate, float> heightsMap)
    {
        var centerPoint = new Point(Mathf.RoundToInt(circle.Center.X), Mathf.RoundToInt(circle.Center.Y));
        if (_completedCircles.Add(centerPoint))
            RevealCircle(circle, a, b, c, heightsMap);
    }

    private void RevealCircle(DelaunayHelper.Circle circle, Vector2 a, Vector2 b, Vector2 c, Dictionary<GlobalChunkCoordinate, float> heightsMap)
    {
        var connectingEdge1Half = Vector2.Lerp(a, b, 0.5f);
        var connectingEdge2Half = Vector2.Lerp(a, c, 0.5f);
        
        void TryRevealCorner(GlobalChunkCoordinate coord)
        {
            var fadeStart = -1 * StormChunkSize;
            var fadeEnd = 3 * StormChunkSize;
            
            var vector = new Vector2(coord.x, coord.y);
            // The storm should not be rendered for chunks that are at the edge of the circle but close to the source patch
            if (DelaunayHelper.GetDistanceSqrToLineSegment(vector, a, connectingEdge1Half) < fadeStart * fadeStart ||
                DelaunayHelper.GetDistanceSqrToLineSegment(vector, a, connectingEdge2Half) < fadeStart * fadeStart)
            {
                /*_heights[coord] = -0.5f;
                return;*/
            }
            
            
            var distanceToCenter = (circle.Center - vector).Length();
            var distanceToEdge = distanceToCenter - circle.Radius;
            var heightInterpolation = Mathf.Lerp(-0.5f, 0, Mathf.InverseLerp(fadeStart, fadeEnd, distanceToEdge));
            heightsMap[coord] = Mathf.Min(heightsMap.GetValueOrDefault(coord, 0), heightInterpolation);
        }
        
        var radiusCeil = Mathf.Ceil(circle.Radius / StormChunkSize) * StormChunkSize;
        var centerXFloor = Mathf.Floor(circle.Center.X /  StormChunkSize) * StormChunkSize;
        var centerYFloor = Mathf.Floor(circle.Center.Y /  StormChunkSize) * StormChunkSize;
        var lowerCorner = new GlobalChunkCoordinate((int)centerXFloor, (int)centerYFloor, 0);
        for (int x = 0; x < radiusCeil; x++)
        {
            for (int y = 0; y < radiusCeil; y++)
            {
                TryRevealCorner(lowerCorner + new ChunkVector(-x * StormChunkSize, -y * StormChunkSize, 0));
                TryRevealCorner(lowerCorner + new ChunkVector((x + 1) * StormChunkSize, -y * StormChunkSize, 0));
                TryRevealCorner(lowerCorner + new ChunkVector((x + 1) * StormChunkSize, (y + 1) * StormChunkSize, 0));
                TryRevealCorner(lowerCorner + new ChunkVector(-x * StormChunkSize, (y + 1) * StormChunkSize, 0));
            }
        }
    }

    private void UpdateAnimatedHeights(float deltaTime)
    {
        using var finishedAnimations = ScopedList.Get<GlobalChunkCoordinate>();
        foreach (var (coord, target) in _targetHeights)
        {
            var current = _heights.GetValueOrDefault(coord, 0);
            if (current > target)
            {
                var next = Mathf.Max(target, current - 0.04f * deltaTime);
                _heights[coord] = next;
            }
            if(current <= target)
                finishedAnimations.Add(coord);
        }
        foreach(var coord in finishedAnimations)
            _targetHeights.Remove(coord);
    }
    
    private void ZoomCameraOutsideStorm(CameraController cameraController)
    {
        if(!IsCameraInsideStorm(cameraController.Parent.position, cameraController.Viewport.TargetZoom, cameraController.TargetAngle, cameraController.Parent.localRotation)) return;

        if (!IsCameraInsideStorm(cameraController.Parent.position, cameraController.Viewport.Zoom,
                cameraController.Viewport.Angle, cameraController.Parent.localRotation))
        {
            // Just use the previous zoom and angle, so the camera doesn't stutter when getting close towards the storm
            cameraController.TargetAngle = cameraController.Viewport.Angle;
            cameraController.Viewport.TargetZoom = cameraController.Viewport.Zoom;
            return;
        }
        
        do
        {
            cameraController.Viewport.TargetZoom *= 1.1f;
        } while (IsCameraInsideStorm(cameraController.Parent.position, cameraController.Viewport.TargetZoom, cameraController.TargetAngle, cameraController.Parent.localRotation));
    }

    private bool IsCameraInsideStorm(Vector3 position, float zoom, float angleDeg, Quaternion rotation)
    {
        var targetPosition = position;
        var angle = Mathf.Deg2Rad * Mathf.Max(angleDeg, CameraController.ComputeZoomAdjustedMinAngle(zoom));
        var localOffset = new Vector3(0, Mathf.Sin(angle) * zoom, -Mathf.Cos(angle) * zoom);
        targetPosition += rotation * localOffset;
        var chunk = ((WorldCoordinate)targetPosition).ToGlobalChunkCoordinate();
        
        var chunkXFloor = (chunk.x / StormChunkSize) * StormChunkSize;
        var chunkYFloor = (chunk.y / StormChunkSize) * StormChunkSize;
        var maxStormHeight = Mathf.Max(
            _heights.GetValueOrDefault(new GlobalChunkCoordinate(chunkXFloor, chunkYFloor, 0), 0),
            _heights.GetValueOrDefault(new GlobalChunkCoordinate(chunkXFloor + StormChunkSize, chunkYFloor, 0), 0),
            _heights.GetValueOrDefault(
                new GlobalChunkCoordinate(chunkXFloor + StormChunkSize, chunkYFloor + StormChunkSize, 0), 0),
            _heights.GetValueOrDefault(new GlobalChunkCoordinate(chunkXFloor, chunkYFloor + StormChunkSize, 0), 0));
        var stormHeightInterpolation = Mathf.InverseLerp(-1, 0, maxStormHeight);
        var minimumCamHeight = Mathf.Lerp(-1, 0.5f, stormHeightInterpolation * stormHeightInterpolation) * StormTileSize; // Add 50% to the max storm height to account for the +50 in draw, for the chunk height in draw being 1, and for the layer offset
        
        return targetPosition.y < minimumCamHeight;
    }

    private static StormLayer CreateLayer(IMaterialReference material, float angleRad, float scaleMultiplier, float timeScaleMultiplier, int index)
    {
        var copy = material.Copy();
        var copyMat = copy.GetMaterialInternal();
        if (copyMat.HasVector(OffsetId))
        {
            copyMat.SetVector(OffsetId, new UnityEngine.Vector2(Random.Range(-20000f, 20000f), Random.Range(-20000f, 200000f)));
        }
        if (copyMat.HasFloat(ScaleId))
        {
            copyMat.SetFloat(ScaleId, copyMat.GetFloat(ScaleId) *  scaleMultiplier);
        }
        if (copyMat.HasFloat(TimeScaleId))
        {
            copyMat.SetFloat(TimeScaleId, copyMat.GetFloat(TimeScaleId) * timeScaleMultiplier);
        }
        if (copyMat.HasFloat(RotationId))
        {
            copyMat.SetFloat(RotationId, angleRad);
        }
        
        return new StormLayer(Mesh, copy, new WorldVector(0, 0, 4 * index));
    }

    private static IMeshReference CreateStormMesh()
    {
        // A plane with larger bounds, because shader changes the height
        // Also add a vortex in the center to make interpolation smoother
        var mesh = new Mesh
        {
            name = "StormRenderer.CreateStormMesh",
            vertices =
            [
                new Vector3(0.5f, 0.0f, -0.5f),
                new Vector3(0.5f, 0.0f, 0.5f),
                new Vector3(-0.5f, 0.0f, 0.5f),
                new Vector3(-0.5f, 0.0f, -0.5f),
                new Vector3(0, 0.0f, 0),
            ],
            triangles =
            [
                4, 1, 0,
                4, 0, 3,
                4, 3, 2,
                4, 2, 1
            ],
            normals = [Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up]
        };
        var bounds = mesh.bounds;
        var extents = bounds.extents;
        extents.y = 64;
        bounds.extents = extents;
        mesh.bounds = bounds;
        // Set uv that is used to select the height of the storm
        mesh.SetUVs(0, new Vector4[]
        {
            new(1, 0, 0, 0),
            new(0, 1, 0, 0),
            new(0, 0, 1, 0),
            new(0, 0, 0, 1),
            new(0.25f, 0.25f, 0.25f, 0.25f)
        });
        return new TemporaryMeshReference(mesh);
    }

    private bool IsChunkLocked(GlobalChunkCoordinate coord)
    {
        return true; //TODO
    }

    private void FilterLockedSelections(GameSessionOrchestrator orchestrator)
    {
        var buildingSelection = orchestrator.PlayerInteractionOrchestrator.PlayerInteractionState.BuildingSelection;
        var islandSelection = orchestrator.PlayerInteractionOrchestrator.PlayerInteractionState.IslandSelection;
        buildingSelection.OnAdded.Register(buildings =>
        {
            buildingSelection.Remove(buildings.Where(building => IsChunkLocked(building.Transform.Position.ToChunkCoordinate())));
        });
        islandSelection.OnAdded.Register(islands =>
        {
            islandSelection.Remove(islands.Where(island => IsChunkLocked(island.Position)));
        });
    }
    
    private class StormLayer(IMeshReference mesh, IMaterialReference material, WorldVector offset)
    {
        public IMeshReference Mesh => mesh;
        public IMaterialReference Material => material;
        public WorldVector Offset => offset;
    }

    private class FilterLockedPlacementData(StormRenderer stormRenderer, IPlacementData parent) : IPlacementData
    {
        public void GetAllBuildings(ICollection<BuildingPlacement> outBuildings)
        {
            using var buildingsFilter = ScopedList.Get<BuildingPlacement>();
            parent.GetAllBuildings(buildingsFilter);
            foreach (var building in buildingsFilter.Where(building => building.PlacementAllowability.WillBePlaced() && stormRenderer.IsChunkLocked(building.Descriptor.Transform.Position.ToChunkCoordinate())))
                parent.InvalidateBuildingAt(building.Descriptor.Transform.Position);
            parent.GetAllBuildings(outBuildings);
        }

        public void GetAllIslands(ICollection<IslandPlacement> outIslands)
        {
            using var islandsFilter = ScopedList.Get<IslandPlacement>();
            parent.GetAllIslands(islandsFilter);
            foreach (var island in islandsFilter.Where(island => island.PlacementAllowability.WillBePlaced() && stormRenderer.IsChunkLocked(island.Descriptor.Transform.Position)))
                parent.InvalidateIslandAt(island.Descriptor.Transform.Position);
            parent.GetAllIslands(outIslands);
        }

        public void AddBuildingPlacement(BuildingPlacement buildingPlacement) => parent.AddBuildingPlacement(buildingPlacement);
        public void RemoveBuildingPlacement(BuildingPlacement buildingPlacement) => parent.RemoveBuildingPlacement(buildingPlacement);
        public void ReplaceBuildingPlacement(BuildingPlacement replacement) => parent.ReplaceBuildingPlacement(replacement);
        public void InvalidateBuildingAt(GlobalTileCoordinate position) => parent.InvalidateBuildingAt(position);
        public bool HasBuildingPlacementAt(GlobalTileCoordinate position) => parent.HasBuildingPlacementAt(position);
        public bool TryGetBuildingPlacementsAt(GlobalTileCoordinate position, ICollection<BuildingPlacement> buildings) => parent.TryGetBuildingPlacementsAt(position, buildings);
        public bool HasIslandPlacementAt(GlobalChunkCoordinate position) => parent.HasIslandPlacementAt(position);
        public bool TryGetIslandPlacementsAt(GlobalChunkCoordinate position, ICollection<IslandPlacement> islands) => parent.TryGetIslandPlacementsAt(position, islands);
        public void AddIslandPlacement(IslandPlacement islandPlacement) => parent.AddIslandPlacement(islandPlacement);
        public void RemoveIslandPlacement(IslandPlacement islandPlacement) => parent.RemoveIslandPlacement(islandPlacement);
        public void ReplaceIslandPlacement(IslandPlacement replacement) => parent.ReplaceIslandPlacement(replacement);
        public void InvalidateIslandAt(GlobalChunkCoordinate position) => parent.InvalidateIslandAt(position);
        public void Clear() => parent.Clear();
        public ICollection<GlobalTileCoordinate> ExtraBuildingsToRemovePositions => parent.ExtraBuildingsToRemovePositions;
        public ICollection<GlobalChunkCoordinate> ExtraIslandsToRemovePositions => parent.ExtraIslandsToRemovePositions;
        public bool CanAffordBlueprintCost { get => parent.CanAffordBlueprintCost; set => parent.CanAffordBlueprintCost = value; }
        public bool CanFitChunkLimit { get => parent.CanFitChunkLimit; set => parent.CanFitChunkLimit = value; }
        public IPlacementAdditionalData AdditionalData => parent.AdditionalData;
        public int MaxBuildingIndex => parent.MaxBuildingIndex;
        public int MaxIslandIndex => parent.MaxIslandIndex;
        public bool CostBlueprintPoints { get => parent.CostBlueprintPoints; set => parent.CostBlueprintPoints = value; }
        public BlueprintCurrency BlueprintCost { get => parent.BlueprintCost; set => parent.BlueprintCost = value; }
        public ChunkLimitCurrency ChunkCost { get => parent.ChunkCost; set => parent.ChunkCost = value; }
        public int IslandsCount { get => parent.IslandsCount; }
        public int BuildingsCount { get => parent.BuildingsCount; }
    }
}