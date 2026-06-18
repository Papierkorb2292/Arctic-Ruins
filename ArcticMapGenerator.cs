using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using ArcticRuins.ArcticPlatform;
using Core.Collections.Scoped;
using Core.Randomizing;
using Game.Core.Coordinates;
using Game.Core.Simulation;
using Game.Interaction.EntitiesPlacement;
using Game.Orchestration;
using Game.Placement.Connectors;
using Game.Placement.Data;
using Game.Placement.Processing;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;

namespace ArcticRuins;

public static class ArcticMapGenerator
{
    private static int _islandRuinChunkProbabilityPerMille = 5;
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
    
    private static readonly Dictionary<int, int> LatticePointsInCircleCache = new();

    private static readonly ConditionalWeakTable<GameSessionOrchestrator, BlueprintCache> BlueprintCaches = new();
    private static Hook _gameInitializedHook;

    private static readonly HashSet<GlobalChunkCoordinate> PendingSuperChunkGeneration = [];

    private static GameSessionOrchestrator globalOrchestrator;
    
    public static void Register()
    {
        _gameInitializedHook = DetourHelper.CreateStaticPostfixHook<IGameSessionManagers>(
                managers => StaticGameCoreAccessor.AssignGameSessionManagers(managers),
                managers =>
                {
                    if (managers == null) return;
                    // Session is initialized now, generate all the pending chunks
                    using var pendingChunks = ScopedList.Get(PendingSuperChunkGeneration);
                    foreach (var chunk in pendingChunks)
                        TryGenerateChunk(globalOrchestrator, chunk);
                }
            );
    }

    public static void Dispose()
    {
        _gameInitializedHook.Dispose();
    }

    public static void TryGenerateChunk(GameSessionOrchestrator orchestrator, in GlobalChunkCoordinate pos)
    {
        globalOrchestrator = orchestrator;
        
        if (StaticGameCoreAccessor.G == null)
        {
            // Not finished initializing yet
            PendingSuperChunkGeneration.Add(pos);
            return;
        }
        
        PendingSuperChunkGeneration.Remove(pos);
        
        var blueprintCache = BlueprintCaches.GetValue(globalOrchestrator,
            orchestrator2 => new BlueprintCache(orchestrator2));
        
        var seed = globalOrchestrator.Mode.Seed;
        var rng = new ConsistentRandom($"{seed}/{pos.x}/{pos.y}");
        if (!rng.TestPerMille(_islandRuinChunkProbabilityPerMille)) return;
        try
        {
            globalOrchestrator.MapModel.CreateIsland(
                globalOrchestrator.Mode.Islands.GetDefinition(ArcticPlatformIsland.ArcticPlatform1x1Id),
                new GlobalChunkTransform(pos, GridRotation.NoRotate),
                null
            );
            ArcticRuinsMod.Instance.SaveData.UnremovablePlatforms.Add(pos);
                    
            var blueprint = blueprintCache.GetBlueprint("DoubleHalfCutter");
            if (blueprint is BuildingBlueprint buildingBlueprint)
                PlaceBuildingBlueprint(buildingBlueprint, globalOrchestrator, pos);
        } catch(MapCannotCreateIslandException) { }
    }

    private static void PlaceBuildingBlueprint(BuildingBlueprint blueprint, GameSessionOrchestrator orchestrator,
        GlobalChunkCoordinate chunk)
    {
        var map = orchestrator.MapModel;
        var player = orchestrator.SystemPlayer;
        var placementData = new ConcurrentPlacementData();
        var blueprintInput = new BlueprintPlacementInput<GlobalTileCoordinate>(GridRotation.NoRotate, false);
        var coordinate = chunk.ToOrigin_G() + new TileVector(CoordinateConstants.TILES_PER_CHUNK / 2, CoordinateConstants.TILES_PER_CHUNK / 2, 0);
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
    
    /*public static (float probability, int level) GetDataFragmentProbabilityInChunk(int x, int y, ResearchProgression researchLayout)
    {
        var level = GetLevelForChunk(x, y);
        if (level == -1)
            return (_postgameDataFragmentProbability, -1);
        if (level <= 1)
            return (0f, level); // For these levels everything is placed statically
        var generatorData = ArcticRuinsMod.Instance.SaveData.GetLevelGeneratorData(level);
        var accumulatedRewardsCount = MilestoneReverser.GetLevelRewardCount(researchLayout);
        if (level >= accumulatedRewardsCount.Count)
            return (_postgameDataFragmentProbability, -1);
        var totalDataFragmentsForLevel = accumulatedRewardsCount[level] - accumulatedRewardsCount[level - 1];
        totalDataFragmentsForLevel += level; // Player shouldn't have to unlock the entire circle, so add a couple more
        var generatedDataFragments = generatorData.GeneratedDataFragments;
        var totalChunks = GetChunksInCircleSection(LevelRadii[level - 1], LevelRadii[level]);
        var generatedChunks = generatorData.GeneratedChunks;
        var probability = (totalDataFragmentsForLevel - generatedDataFragments) / (float)(totalChunks - generatedChunks);
        return (probability, level);
    }*/
    
    public static int GetLevelForChunk(int x, int y)
    {
        // Use corner closest to the origin
        var cornerX = x < 0 ? x + 1 : x;
        var cornerY = y < 0 ? y + 1 : y;
        var distanceSqr = cornerX * cornerX + cornerY * cornerY;
        return LevelRadiiSquare.FindIndex(radiusSqr => distanceSqr >= radiusSqr);
    }

    public static bool IsChunkInCircle(int x, int y, int radius)
    {
        // Use corner closest to the origin
        var cornerX = x < 0 ? x + 1 : x;
        var cornerY = y < 0 ? y + 1 : y;
        return cornerX*cornerX + cornerY*cornerY <= radius*radius;
    }

    public static int GetChunksInCircleSection(int innerRadius, int outerRadius)
    {
        return GetChunksInCircle(outerRadius) - GetChunksInCircle(innerRadius);
    }

    public static int GetChunksInCircle(int radius)
    {
        var chunkCount = GetLatticePointsInCircle(radius);

        // Add chunks along the axis that share a lattice point
        chunkCount += radius * 4 + 3;
        
        return chunkCount;
    }
    
    // https://oeis.org/A000328
    public static int GetLatticePointsInCircle(int radius)
    {
        if (LatticePointsInCircleCache.TryGetValue(radius, out var result))
            return result;
        
        result = 1 + Enumerable.Range(1, radius)
            .Select(k => (int)Math.Sqrt(k * ((radius << 1) - k)))
            .Sum() << 2;
        LatticePointsInCircleCache[radius] = result;
        return result;
    }
    
    private delegate MapSuperChunk GetOrCreateSuperChunkMethod(GameResourcesMap map, in SuperChunkCoordinate tile_SC);
    private delegate MapSuperChunk GetOrCreateSuperChunkWrapper(GetOrCreateSuperChunkMethod original, GameResourcesMap map, in SuperChunkCoordinate tile_SC);

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