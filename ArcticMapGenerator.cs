using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ArcticRuins.ArcticPlatform;
using ArcticRuins.DataFragment;
using Core.Collections.Scoped;
using Core.Randomizing;
using Game.Core.Coordinates;
using Game.Core.Map.Generation;
using Game.Core.Map.Simulation;
using Game.Core.Simulation;
using Game.Interaction.EntitiesPlacement;
using Game.Orchestration;
using Game.Placement.Data;
using Game.Placement.Processing;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using Unity.Mathematics;
using UnityEngine;

namespace ArcticRuins;

public static class ArcticMapGenerator
{
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

    private static readonly RandomBlueprint[] Blueprints =
    [
        new("Misc1", 4, new IslandTileCoordinate(9, 10, 0)),
        new("LayerDetacher", 4, new IslandTileCoordinate(8, 9, 0)),
        new("Crossing", 2, new IslandTileCoordinate(9, 10, 0)),
        new("DiagonalSwapper", 3, new IslandTileCoordinate(9, 10, 0)),
        new("Painter", 3, new IslandTileCoordinate(9, 9, 0)),
        new("DoubleHalfCutter", 1, new IslandTileCoordinate(10, 10, 0))
    ];

    private static readonly int BlueprintsTotalWeight = Blueprints.Select(blueprint => blueprint.Weight).Sum(); 
    
    private static readonly ConditionalWeakTable<GameSessionOrchestrator, BlueprintCache> BlueprintCaches = new();
    private static readonly ConditionalWeakTable<IMapGenerator, GameSessionOrchestrator> GeneratorOrchestrators = new();
    private static Hook _gameTickHook;
    private static Hook _mainMapHook;
    private static Hook _tryGenerateShapePatchHook;

    private static readonly HashSet<GlobalChunkCoordinate> PendingSuperChunkGeneration = [];

    public static void Register()
    {
        _gameTickHook = DetourHelper.CreatePrefixHook<GameSessionOrchestrator, Ticks, IReadOnlyList<SimulationLOD>, bool>(
                (orchestrator, deltaTicks, simulationLODs, doParallelSimulationUpdate) => orchestrator.StartLogicUpdate(deltaTicks, simulationLODs, doParallelSimulationUpdate),
                (orchestrator, deltaTicks, simulationLODs, doParallelSimulationUpdate) =>
                {
                    // Generate pending chunks. This has to happen delayed, because blueprints can't be placed while the simulation is updated
                    using var pendingChunks = ScopedList.Get(PendingSuperChunkGeneration);
                    foreach (var chunk in pendingChunks)
                        TryGenerateChunk(orchestrator, chunk);
                    return (deltaTicks, simulationLODs, doParallelSimulationUpdate);
                }
            );
        _mainMapHook = DetourHelper.CreatePostfixHook<GameSessionOrchestrator, Ticks, GameMigrators>(
            (orchestrator, initialSimulationTime, gameMigrators) =>
                orchestrator.Init_3_3_MainMap(initialSimulationTime, gameMigrators),
            (orchestrator, _, _) =>
            {
                GeneratorOrchestrators.Add(orchestrator.ResourcesMap.Generator, orchestrator);
            });
        _tryGenerateShapePatchHook = new Hook(
            typeof(DefaultMapGenerator).GetMethod(nameof(DefaultMapGenerator.TryGenerateShapePatch), BindingFlags.NonPublic | BindingFlags.Instance)!,
            (TryGenerateShapePatchWrapper)((orig, generator, data, shape, size, out result) =>
            {
                // Replace shapes with the custom generated shapes. This needs to happen after TryGenerateShapePatch because it uses the chunk coordinate
                if (!orig(generator, data, shape, size, out result))
                    return false;
                if (!GeneratorOrchestrators.TryGetValue(generator, out var orchestrator) ||
                    !ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(orchestrator.Mode.Scenario) ||
                    result is not ShapeResourceClusterData shapeCluster)
                {
                    return true;   
                }

                var distanceToOriginSuperChunk = (int)math.length((int2)data.ChunkPos_SC);
                var distanceToOriginChunk = (int)Math.Sqrt(shapeCluster.Center_GC.x*shapeCluster.Center_GC.x + shapeCluster.Center_GC.y*shapeCluster.Center_GC.y);
                
                var newShape = GenerateClusterShape(orchestrator, generator.ShapeGenerator, data.Rng, distanceToOriginSuperChunk, distanceToOriginChunk);

                result = new ShapeResourceClusterData(
                    shapeCluster.ShapeResources.Select(shapeData =>
                        new ShapeResourceSourceData(shapeData.Offset_LC, newShape)),
                    shapeCluster.Center_GC,
                    shapeCluster.ShapeRegistry
                );
                
                return true;
            }));
    }

    public static void Dispose()
    {
        _gameTickHook.Dispose();
        _mainMapHook.Dispose();
        _tryGenerateShapePatchHook.Dispose();
    }

    public static void QueueChunkGeneration(in GlobalChunkCoordinate pos)
    {
        PendingSuperChunkGeneration.Add(pos);
    } 

    private static void TryGenerateChunk(GameSessionOrchestrator orchestrator, in GlobalChunkCoordinate pos)
    {
        var blueprintCache = BlueprintCaches.GetValue(orchestrator,
            orchestrator2 => new BlueprintCache(orchestrator2));
        
        PendingSuperChunkGeneration.Remove(pos);
        if (pos == GlobalChunkCoordinate.Origin)
        {
            PlaceHubBuildings(orchestrator, blueprintCache);
        }
        if (pos.x is 0 or -1 && pos.y is 0 or -1)
            return; // Don't generate anything else at the center of the map, where there already are islands
        
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

            var blueprintNumber = rng.Next(0, BlueprintsTotalWeight);
            var blueprintIndex = 0;
            while(blueprintNumber >= Blueprints[blueprintIndex].Weight)
            {
                blueprintNumber -= Blueprints[blueprintIndex].Weight;
                blueprintIndex++;
            }
            var randomBlueprint =  Blueprints[blueprintIndex];
            
            var blueprint = blueprintCache.GetBlueprint(randomBlueprint.Name);
            if (blueprint is BuildingBlueprint buildingBlueprint)
                PlaceBuildingBlueprint(buildingBlueprint, orchestrator, pos, randomBlueprint.Coord, GridRotation.RotationsInClockwiseOrder[rng.Next(0, 4)]);

            if (hasDataFragment)
                PlaceDataFragment(islandDescriptor, orchestrator, rng);
        } catch(MapCannotCreateIslandException) { }
    }

    private static void PlaceBuildingBlueprint(BuildingBlueprint blueprint, GameSessionOrchestrator orchestrator,
        GlobalChunkCoordinate chunk, IslandTileCoordinate relativePos, GridRotation rotation)
    {
        var map = orchestrator.MapModel;
        var player = orchestrator.SystemPlayer;
        // Use flat here, because other PlacementData implementations would add multi tile buildings multiple times and that's no good
        var placementData = new FlatPlacementData(ArcticRuinsMod.Logger);
        var blueprintInput = new BlueprintPlacementInput<GlobalTileCoordinate>(rotation, false);
        var coordinate = relativePos.RotateAroundCenter(rotation).ToGlobal(chunk);
        blueprintInput.TryUpdateStartPosition(coordinate);
        blueprintInput.TryUpdateEndPosition(coordinate);
        var processor = new BuildingBlueprintProcessor(blueprint, ArcticRuinsMod.Logger);
        processor.Process(placementData, new PlacementInputHolder(blueprintInput), map, map.LayoutModel, new PlacementErrors());
        using var addedBuildings = ScopedList.Get<BuildingPlacement>();
        placementData.GetAllBuildings(addedBuildings);

        // Put buildings in modify action
        using var placePayload = ScopedList.Get<PlaceBuildingPayload>();
        foreach (var addedBuilding in addedBuildings)
        {
            ArcticRuinsMod.Logger.Info!.Log(addedBuilding.Descriptor.ToString());
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

    public static bool ShouldGenerateLevelDataFragment(GlobalChunkCoordinate chunk, GameSessionOrchestrator orchestrator)
    {
        // Use cached map if it was generated before
        if (ArcticRuinsMod.Instance.SaveData.DataFragmentChunkLevels.Count != 0)
            return ArcticRuinsMod.Instance.SaveData.DataFragmentChunkLevels.ContainsKey(chunk);
        
        var dataFragmentChunks = new Dictionary<GlobalChunkCoordinate, int>();
        dataFragmentChunks[new GlobalChunkCoordinate(-2, 0, 0)] = 1; // Add the stabilizer data fragment from PlaceHubBuildings
        var research = orchestrator.Research.Layout;

        var rewardCounts = MilestoneReverser.GetLevelRewardCount(research);
        var rng = new ConsistentRandom($"{orchestrator.Mode.Seed}");
        for (int i = 1; i < rewardCounts.Count; i++)
        {
            var count = rewardCounts[i] + i; // Generate some extra data fragments for each level, so player's don't have to unlock the entire circle
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
                } while(dataFragmentChunks.ContainsKey(position));

                dataFragmentChunks[position] = i;
            }
        }
        
        ArcticRuinsMod.Instance.SaveData.DataFragmentChunkLevels = dataFragmentChunks;
        return dataFragmentChunks.ContainsKey(chunk);
    }

    private static bool ShouldGeneratePostgameDataFragment(GlobalChunkCoordinate chunk, GameSessionOrchestrator orchestrator, ConsistentRandom chunkRng)
    {
        // Postgame data fragments only generate beyond the last level
        return !IsChunkInCircle(chunk.x, chunk.y, LevelRadii[MilestoneReverser.GetLevelRewardCount(orchestrator.Research.Layout).Count]) &&
               chunkRng.Next(0, 50000) == 0;
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

    private static void PlaceHubBuildings(GameSessionOrchestrator orchestrator, BlueprintCache blueprints)
    {
        // Place data fragment for stabilizer reward next to first asteroid miner island
        var buildingTransform = new GlobalTileTransform(new GlobalTileCoordinate(-36, 6, 0), GridRotation.NoRotate);
        orchestrator.MapModel.CreateBuilding(
            orchestrator.Mode.Buildings.GetDefinition(DataFragmentBuilding.DefinitionId),
            in buildingTransform,
            null);
        
        
        PlaceBuildingBlueprint((BuildingBlueprint)blueprints.GetBlueprint("Hub"), orchestrator, new GlobalChunkCoordinate(-1, 0, 0), new IslandTileCoordinate(3, 8, 0), GridRotation.NoRotate);
    }
    
    // Generate a shape for a cluster at the given distance. This shape will only use the levels that the player
    // is expected to have unlocked at this distance from the vortex
    private static ShapeId GenerateClusterShape(GameSessionOrchestrator orchestrator, MapShapeGenerator generator, ConsistentRandom rng, int distanceToOriginSuperChunk, int distanceToOriginChunk)
    {
        var types = generator.GenerationCache[Math.Min(distanceToOriginSuperChunk, 50)];
        if (types.Length == 0)
            return ShapeId.Invalid;
        var type = rng.Choice(types);
        var level = GetLevelForPatch(distanceToOriginChunk, orchestrator.Research.Layout.Levels.Count);
        return GenerateClusterShape(orchestrator, generator, rng, type, level);
    }
    
    private static ShapeId GenerateClusterShape(GameSessionOrchestrator orchestrator, MapShapeGenerator generator, ConsistentRandom rng, MapShapeGenerationType type, int level) 
    {
        int partCount = orchestrator.Mode.ShapesConfiguration.PartCount;
        int halfShape = partCount / 2;
        int basePartCount = type switch
        {
            MapShapeGenerationType.UncoloredHalfShape => 1,
            MapShapeGenerationType.UncoloredAlmostFullShape => 1,
            MapShapeGenerationType.UncoloredFullShape => halfShape,
            MapShapeGenerationType.UncoloredFullShapePure => partCount,
            MapShapeGenerationType.PrimaryColorHalfShape => 1,
            MapShapeGenerationType.PrimaryColorAlmostFullShape => 1,
            MapShapeGenerationType.PrimaryColorFullShape => halfShape,
            MapShapeGenerationType.PrimaryColorFullShapePure => partCount,
            MapShapeGenerationType.SecondaryColorHalfShape => 1,
            MapShapeGenerationType.SecondaryColorAlmostFullShape => 1,
            MapShapeGenerationType.SecondaryColorFullShape => halfShape,
            MapShapeGenerationType.SecondaryColorFullShapePure => partCount,
            MapShapeGenerationType.TertiaryColorHalfShape => 1,
            MapShapeGenerationType.TertiaryColorAlmostFullShape => 1,
            MapShapeGenerationType.TertiaryColorFullShape => halfShape,
            MapShapeGenerationType.TertiaryColorFullShapePure => partCount,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        int additionalPartCount = type switch
        {
            MapShapeGenerationType.UncoloredHalfShape => 1,
            MapShapeGenerationType.UncoloredAlmostFullShape => halfShape,
            MapShapeGenerationType.UncoloredFullShape => halfShape,
            MapShapeGenerationType.UncoloredFullShapePure => 0,
            MapShapeGenerationType.PrimaryColorHalfShape => 1,
            MapShapeGenerationType.PrimaryColorAlmostFullShape => halfShape,
            MapShapeGenerationType.PrimaryColorFullShape => halfShape,
            MapShapeGenerationType.PrimaryColorFullShapePure => 0,
            MapShapeGenerationType.SecondaryColorHalfShape => 1,
            MapShapeGenerationType.SecondaryColorAlmostFullShape => halfShape,
            MapShapeGenerationType.SecondaryColorFullShape => halfShape,
            MapShapeGenerationType.SecondaryColorFullShapePure => 0,
            MapShapeGenerationType.TertiaryColorHalfShape => 1,
            MapShapeGenerationType.TertiaryColorAlmostFullShape => halfShape,
            MapShapeGenerationType.TertiaryColorFullShape => halfShape,
            MapShapeGenerationType.TertiaryColorFullShapePure => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        using var parts = ScopedList<ShapePart>.Get();
        var baseShape = ColorShapeRandomly(PickRandomShape(rng, level, orchestrator), rng, generator, type);
        for (int index = 0; index < basePartCount; ++index)
        {
            parts.Add(baseShape);
        }
        for (int index = 0; index < additionalPartCount; ++index)
        {
            var additionalShape = ColorShapeRandomly(PickRandomShape(rng, level, orchestrator), rng, generator, type);
            parts.Add(additionalShape);
        }
        for (int count = parts.Count; count < partCount; ++count)
            parts.Add(ShapePart.Empty);
        rng.Shuffle(parts);
        return generator.ShapeIdManager.Resolve(ShapeDefinition.ComputeHash([
            new ShapeLayer(parts.ToArray())
        ]));
    }

    private static ShapePart PickRandomShape(ConsistentRandom rng, int level, GameSessionOrchestrator orchestrator)
    {
        var shapeSourceLevel = Math.Max(rng.Next(0, level), rng.Next(0, level)); // Bias towards the higher levels
        var costs = orchestrator.Research.Layout.Levels[shapeSourceLevel].Costs;
        var shapeHash = ((IShapeResearchCost)rng.Choice(costs)).ShapeHash;
        var shape = orchestrator.ShapeRegistry.GetDefinition(orchestrator.ShapeIdManager.Resolve(shapeHash));

        ShapePart result;
        do
        {
            result = rng.Choice(rng.Choice(shape.Layers).Parts);
        } while (result.IsEmpty);
        return result;
    }

    private static ShapePart ColorShapeRandomly(ShapePart shape, ConsistentRandom rng, MapShapeGenerator generator,
        MapShapeGenerationType type)
    {
        if (shape.IsEmpty || !shape.Shape.AllowColor || !rng.TestPercentage(generator.MapGenerationParameters.ShapePatchShapeColorfulnessPercent))
            return shape; // Keep original color (doesn't have to be uncolored)

        var newColor = type switch
        {
            MapShapeGenerationType.UncoloredHalfShape => generator.ColorScheme.DefaultShapeColor,
            MapShapeGenerationType.UncoloredAlmostFullShape => generator.ColorScheme.DefaultShapeColor,
            MapShapeGenerationType.UncoloredFullShape => generator.ColorScheme.DefaultShapeColor,
            MapShapeGenerationType.UncoloredFullShapePure => generator.ColorScheme.DefaultShapeColor,
            MapShapeGenerationType.PrimaryColorHalfShape => rng.Choice(generator.ColorScheme.PrimaryColors),
            MapShapeGenerationType.PrimaryColorAlmostFullShape => rng.Choice(generator.ColorScheme.PrimaryColors),
            MapShapeGenerationType.PrimaryColorFullShape => rng.Choice(generator.ColorScheme.PrimaryColors),
            MapShapeGenerationType.PrimaryColorFullShapePure => rng.Choice(generator.ColorScheme.PrimaryColors),
            MapShapeGenerationType.SecondaryColorHalfShape => rng.Choice(generator.ColorScheme.SecondaryColors),
            MapShapeGenerationType.SecondaryColorAlmostFullShape => rng.Choice(generator.ColorScheme.SecondaryColors),
            MapShapeGenerationType.SecondaryColorFullShape => rng.Choice(generator.ColorScheme.SecondaryColors),
            MapShapeGenerationType.SecondaryColorFullShapePure => rng.Choice(generator.ColorScheme.SecondaryColors),
            MapShapeGenerationType.TertiaryColorHalfShape => rng.Choice(generator.ColorScheme.TertiaryColors),
            MapShapeGenerationType.TertiaryColorAlmostFullShape => rng.Choice(generator.ColorScheme.TertiaryColors),
            MapShapeGenerationType.TertiaryColorFullShape => rng.Choice(generator.ColorScheme.TertiaryColors),
            MapShapeGenerationType.TertiaryColorFullShapePure => rng.Choice(generator.ColorScheme.TertiaryColors),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        return new ShapePart(shape.Shape, newColor);
    }
    
    private static bool IsChunkInCircle(int x, int y, int radius)
    {
        // Use corner closest to the origin
        var cornerX = x < 0 ? x + 1 : x;
        var cornerY = y < 0 ? y + 1 : y;
        return cornerX*cornerX + cornerY*cornerY <= radius*radius;
    }

    private static int GetLevelForPatch(int distanceToOrigin, int levelCount)
    {
        for (int i = 1; i < levelCount; i++)
        {
            if(distanceToOrigin < LevelRadii[i])
                return i - 1;
        }
        return levelCount;
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

    private delegate bool TryGenerateShapePatch(DefaultMapGenerator self, MapGenerationSuperChunkPayload data, ShapeId shape, int patchSize, out IResourceSourceData result);

    private delegate bool TryGenerateShapePatchWrapper(TryGenerateShapePatch original, DefaultMapGenerator self,
        MapGenerationSuperChunkPayload data, ShapeId shape, int patchSize, out IResourceSourceData result);

    private class RandomBlueprint(string name, int weight, IslandTileCoordinate coord)
    {
        // The file name of the blueprint in "Resources/Blueprints" without the .txt file extension
        public string Name => name;
        // The weight with which this blueprint should be randomly chosen
        public int Weight => weight;
        // The relative position of the blueprint on the island with the default rotation. This it the coordinate
        // that the cursor is on when placing the blueprint
        public IslandTileCoordinate Coord => coord;
    }
}