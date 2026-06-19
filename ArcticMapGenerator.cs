using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using ArcticRuins.ArcticPlatform;
using ArcticRuins.DataFragment;
using Core.Collections.Scoped;
using Core.Randomizing;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using Game.Core.Simulation;
using Game.Interaction.EntitiesPlacement;
using Game.Orchestration;
using Game.Placement.Connectors;
using Game.Placement.Data;
using Game.Placement.Processing;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using UnityEngine;

namespace ArcticRuins;

public static class ArcticMapGenerator
{
    private static int _postgameDataFragmentProbabilityPerMille = 1;
    private static readonly int[] LevelRadii = [
        0,
        2 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK,
        4 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK,
        6 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK,
        8 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK,
        10 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK,
        12 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK,
        14 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK,
        16 * CoordinateConstants.CHUNKS_PER_SUPER_CHUNK
    ];
    private static readonly int[] LevelRadiiSquare = LevelRadii.Select(radius => radius * radius).ToArray();
    
    private static readonly ConditionalWeakTable<GameSessionOrchestrator, BlueprintCache> BlueprintCaches = new();
    private static Hook _gameTickHook;

    private static readonly HashSet<GlobalChunkCoordinate> PendingSuperChunkGeneration = [];

    private static GameSessionOrchestrator globalOrchestrator;
    
    public static void Register()
    {
        _gameTickHook = DetourHelper.CreatePrefixHook<GameSessionOrchestrator, Ticks, IReadOnlyList<SimulationLOD>, bool>(
                (orchestrator, deltaTicks, simulationLODs, doParallelSimulationUpdate) => orchestrator.StartLogicUpdate(deltaTicks, simulationLODs, doParallelSimulationUpdate),
                (orchestrator, deltaTicks, simulationLODs, doParallelSimulationUpdate) =>
                {
                    // Generate a pending chunk. This has to happen delayed, because blueprints can't be placed while the simulation is updated
                    // It also reduces lag to do process them one at a time
                    using var pendingChunks = ScopedList.Get(PendingSuperChunkGeneration);
                    foreach (var chunk in pendingChunks)
                        TryGenerateChunk(orchestrator, chunk);
                    return (deltaTicks, simulationLODs, doParallelSimulationUpdate);
                }
            );
    }

    public static void Dispose()
    {
        _gameTickHook.Dispose();
    }

    public static void QueueChunkGeneration(in GlobalChunkCoordinate pos)
    {
        PendingSuperChunkGeneration.Add(pos);
    } 

    private static void TryGenerateChunk(GameSessionOrchestrator orchestrator, in GlobalChunkCoordinate pos)
    {
        PendingSuperChunkGeneration.Remove(pos);
        
        var blueprintCache = BlueprintCaches.GetValue(orchestrator,
            orchestrator2 => new BlueprintCache(orchestrator2));
        
        var seed = orchestrator.Mode.Seed;
        var rng = new ConsistentRandom($"{seed}/{pos.x}/{pos.y}");
        var hasDataFragment = ShouldGenerateLevelDataFragment(pos, orchestrator) ||
                              ShouldGeneratePostgameDataFragment(pos, orchestrator, rng);
        if (!hasDataFragment && rng.Next(0, 10000) != 0) return;
        try
        {
            var island = orchestrator.MapModel.CreateIsland(
                orchestrator.Mode.Islands.GetDefinition(ArcticPlatformIsland.ArcticPlatform1x1Id),
                new GlobalChunkTransform(pos, GridRotation.NoRotate),
                null
            );
            var islandDescriptor = island.Instance.ToDescriptor();
            ArcticRuinsMod.Instance.SaveData.UnremovablePlatforms.Add(pos);
                    
            var blueprint = blueprintCache.GetBlueprint("DoubleHalfCutter");
            if (blueprint is BuildingBlueprint buildingBlueprint)
                PlaceBuildingBlueprint(buildingBlueprint, orchestrator, pos, GridRotation.RotationsInClockwiseOrder[rng.Next(0, 4)]);

            if (hasDataFragment)
                PlaceDataFragment(islandDescriptor, orchestrator, rng);
        } catch(MapCannotCreateIslandException) { }
    }

    private static void PlaceBuildingBlueprint(BuildingBlueprint blueprint, GameSessionOrchestrator orchestrator,
        GlobalChunkCoordinate chunk, GridRotation rotation)
    {
        var map = orchestrator.MapModel;
        var player = orchestrator.SystemPlayer;
        var placementData = new ConcurrentPlacementData();
        var blueprintInput = new BlueprintPlacementInput<GlobalTileCoordinate>(rotation, false);
        var coordinate = chunk.ToOrigin_G() + new TileVector(
            CoordinateConstants.TILES_PER_CHUNK / 2 - (rotation == GridRotation.Rotate180 || rotation == GridRotation.RotateCW ? 1 : 0),
            CoordinateConstants.TILES_PER_CHUNK / 2 - (rotation == GridRotation.Rotate180 || rotation == GridRotation.RotateCCW ? 1 : 0),
            0);
        blueprintInput.TryUpdateStartPosition(coordinate);
        blueprintInput.TryUpdateEndPosition(coordinate);
        var processor = new BuildingBlueprintProcessor(blueprint, ArcticRuinsMod.Logger);
        processor.Process(placementData, new PlacementInputHolder(blueprintInput), map, map.LayoutModel, new PlacementErrors());
        using var addedBuildings = ScopedList.Get<BuildingPlacement>();
        placementData.GetAllBuildings(addedBuildings);

        using var placePayload = ScopedList.Get<PlaceBuildingPayload>();
        foreach (var addedBuilding in addedBuildings)
        {
            var island = map.GetIsland(addedBuilding.Descriptor.Transform.Position);
            var islandTileTransform = addedBuilding.Descriptor.Transform.ToIsland(island);
            placePayload.Add(new PlaceBuildingPayload(
                island.Id,
                addedBuilding.Descriptor.Definition,
                addedBuilding.Descriptor.Configuration,
                in islandTileTransform
            ));
        }

        var action = new ActionModifyBuildings(map, player, new ModifyBuildingsPayload(placePayload, []));
        if (!action.IsPossible(orchestrator.InteractionMode))
            throw new Exception("Failed to place blueprint for map generation");
        orchestrator.PlayerActions.ExecuteActionImmediately_INTERNAL(action, out _);
    }

    private static bool ShouldGenerateLevelDataFragment(GlobalChunkCoordinate chunk, GameSessionOrchestrator orchestrator)
    {
        if (ArcticRuinsMod.Instance.SaveData.DataFragmentChunks != null)
            return ArcticRuinsMod.Instance.SaveData.DataFragmentChunks.Contains(chunk);
        
        var dataFragmentChunks = new HashSet<GlobalChunkCoordinate>();
        var research = orchestrator.Research.Layout;

        var rewardCounts = MilestoneReverser.GetLevelRewardCount(research);
        var rng = new ConsistentRandom($"{orchestrator.Mode.Seed}");
        for (int i = 1; i < rewardCounts.Count; i++)
        {
            var count = rewardCounts[i] + i; //
                                             // Generate some extra data fragments for each level, so player's don't have to unlock the entire circle
            var distMin = LevelRadii[i];
            var distMax = LevelRadii[i + 1];
            for (int j = 0; j < count; j++)
            {
                GlobalChunkCoordinate position;
                do
                {
                    var directionDeg = rng.Next(0, 360 * 4) / 4f;
                    var distance = rng.Next(distMin, distMax);
                    var posXFloat = Mathf.Cos(directionDeg * Mathf.Deg2Rad) * distance;
                    var posYFloat = Mathf.Sin(directionDeg * Mathf.Deg2Rad) * distance;
                    position = new GlobalChunkCoordinate(
                        Mathf.RoundToInt(posXFloat),
                        Mathf.RoundToInt(posYFloat),
                        0);
                } while(!dataFragmentChunks.Add(position));
            }
        }
        
        ArcticRuinsMod.Instance.SaveData.DataFragmentChunks = dataFragmentChunks;
        return dataFragmentChunks.Contains(chunk);
    }

    private static bool ShouldGeneratePostgameDataFragment(GlobalChunkCoordinate chunk, GameSessionOrchestrator orchestrator, ConsistentRandom chunkRng)
    {
        // Postgame data fragments only generate beyond the last level
        return !IsChunkInCircle(chunk.x, chunk.y, LevelRadii[MilestoneReverser.GetLevelRewardCount(orchestrator.Research.Layout).Count]) &&
               chunkRng.TestPerMille(_postgameDataFragmentProbabilityPerMille);
    }

    private static void PlaceDataFragment(IslandDescriptor island, GameSessionOrchestrator orchestrator,
        ConsistentRandom chunkRng)
    {
        var query = new IslandLayoutQuery(island, orchestrator.Mode.MaxBuildingLayer);
        var availableTiles = 0;
        for (short y = 0; y < CoordinateConstants.TilesPerIslandLayer; y++)
        {
            for (short x = 0; x < CoordinateConstants.TilesPerIslandLayer; x++)
            {
                if(query.IsValidAndBuildableTile_I(new IslandTileCoordinate(x, y, 0)) && !orchestrator.Map.TryGetBuilding(island.Transform.Position.ToOrigin_G() + new TileVector(x, y, 0), out _, out _))
                    availableTiles++;
            }
        }
        var fragmentLocation = chunkRng.Next(0, availableTiles);
        for (short y = 0; y < CoordinateConstants.TilesPerIslandLayer; y++)
        {
            for (short x = 0; x < CoordinateConstants.TilesPerIslandLayer; x++)
            {
                var position = island.Transform.Position.ToOrigin_G() + new TileVector(x, y, 0);
                if (!query.IsValidAndBuildableTile_I(new IslandTileCoordinate(x, y, 0)) ||
                    orchestrator.Map.TryGetBuilding(position, out _, out _)) continue;
                var buildingTransform = new GlobalTileTransform(position, GridRotation.NoRotate);
                if (fragmentLocation == 0)
                {
                    orchestrator.MapModel.CreateBuilding(
                        orchestrator.Mode.Buildings.GetDefinition(DataFragmentBuilding.DefinitionId),
                        in buildingTransform,
                        null);
                    return;
                }
                
                fragmentLocation--;
            }
        }
    }
    
    public static bool IsChunkInCircle(int x, int y, int radius)
    {
        // Use corner closest to the origin
        var cornerX = x < 0 ? x + 1 : x;
        var cornerY = y < 0 ? y + 1 : y;
        return cornerX*cornerX + cornerY*cornerY <= radius*radius;
    }

    private class BlueprintCache(GameSessionOrchestrator orchestrator)
    {
        private readonly Dictionary<string, IBlueprint> _loadedBlueprints = new();
        
        public IBlueprint GetBlueprint(string name)
        {
            if (_loadedBlueprints.TryGetValue(name, out var blueprint))
                return blueprint;
            blueprint = LoadBlueprint(name);
            _loadedBlueprints[name] = blueprint;
            return blueprint;
        }

        private IBlueprint LoadBlueprint(string name)
        {
            var path = ArcticRuinsMod.Instance.Resources.SubPath($"Blueprints/{name}.txt");
            return orchestrator.BlueprintSerializer.Deserialize(File.ReadAllText(path));
        }
    }
}