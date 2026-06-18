using System;
using System.Collections.Generic;
using System.Linq;
using Core.Collections.Scoped;
using Core.Localization;
using Game.Core.Coordinates;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Textures;
using Unity.Mathematics;

namespace ArcticRuins.ArcticPlatform;

public static class ArcticPlatformIsland
{
    public static IslandDefinitionId ArcticPlatform1x1Id { get; private set; } 
    
    public static void Register()
    {
        ArcticPlatform1x1Id = AddFoundation(1, 1);
    }
    
    private static IslandDefinitionId AddFoundation(int width, int height)
    {
        string suffix = $"{width}x{height}";
        IslandDefinitionGroupId groupId = new($"ArcticPlatform_{suffix}");
        IslandDefinitionId definitionId = new($"ArcticPlatform_{suffix}");

        string titleId = $"ArcticPlatform{suffix}.title";
        string descriptionId = "island-layout.Layout_GenericPlatform.description";


        string iconPath = ArcticRuinsMod.Instance.Resources.SubPath("DiagonalCutter_Icon.png");

        IIslandGroupBuilder islandGroupBuilder = IslandGroup.Create(groupId)
           .WithTitle(titleId.T())
           .WithDescription(descriptionId.T())
           .WithIcon(FileTextureLoader.LoadTextureAsSprite(iconPath, out _))
           .AsNonTransportableIsland()
           .WithPreferredPlacement(DefaultPreferredPlacementMode.Area);

        var layout = FoundationLayout(width, height);

        IIslandBuilder islandBuilder = Island.Create(definitionId)
           .WithLayout(layout)
           .WithBoundingCollider()
           .WithConnectorData(FoundationConnectors(layout))
           .WithInteraction(flippable: false, canHoldBuildings: true)
           .WithCustomChunkCost(new ChunkLimitCurrency(0)) // Doesn't cost anything because these are gonna placed all over the map automatically
           .WithRenderingOptions(ChunkDrawingOptions(), drawPlayingField: true);

        AtomicIslands.Extend()
           .AllScenarios()
           .WithIsland(islandBuilder, islandGroupBuilder)
           .UnlockedAtMilestone(Helper.FirstMilestoneSelector)
           .WithDefaultPlacement()
           .InToolbar(Helper.NoToolbarEntryLocation)
           .WithoutSimulation()
           .WithoutModules()
           .Build();
        
        return definitionId;
    }

    private static IChunkDrawingContextProvider ChunkDrawingOptions()
    {
        return new HomogeneousChunkDrawing(ChunkPlatformDrawingContext.DrawAll());
    }

    // TODO: Create fluent API for this
    private static ChunkLayoutLookup<ChunkVector, IslandChunkData> FoundationLayout(int width, int height)
    {
        return new ChunkLayoutLookup<ChunkVector, IslandChunkData>(Chunks(width, height));
    }

    private static IEnumerable<KeyValuePair<ChunkVector, IslandChunkData>> Chunks(int width, int height)
    {
        var allChunks = ChunksData(width, height).ToArray();

        foreach (var kv in allChunks)
        {
            yield return new KeyValuePair<ChunkVector, IslandChunkData>(
                kv.Key,
                IslandLayoutFactory.CreateIslandChunkData(
                    kv.Key,
                    kv.Value,
                    allChunks.Select(x => x.Key).ToArray(),
                    true,
                    false,
                    out _));
        }
    }

    private static IEnumerable<KeyValuePair<ChunkVector, ChunkDirection[]>> ChunksData(int width, int height)
    {
        var start = new int2((width - 1) / -2, (height - 1) / -2);
        var end = new int2(width / 2, height / 2);

        using var chunks = ScopedHashSet<ChunkVector>.Get();

        for (int x = start.x; x <= end.x; x++)
        {
            for (int y = start.y; y <= end.y; y++)
            {
                chunks.Add(new ChunkVector(x, y, 0));
            }
        }

        foreach (ChunkVector chunk in chunks)
        {
            using var notchDirections = ScopedList<ChunkDirection>.Get();
            ComputeExternalNotches(chunk, chunks, notchDirections);
            yield return new KeyValuePair<ChunkVector, ChunkDirection[]>(chunk, notchDirections.ToArray());
        }
    }

    private static void ComputeExternalNotches(
        ChunkVector chunk,
        ISet<ChunkVector> chunks,
        ICollection<ChunkDirection> notchDirections)
    {
        foreach (GridRotation rotation in GridRotation.RotationsInClockwiseOrder)
        {
            var dir = rotation.ToChunkDirection();
            ChunkVector neighbor = chunk + dir;
            if (!chunks.Contains(neighbor))
            {
                notchDirections.Add(dir);
            }
        }
    }

    private static IIslandConnectorData FoundationConnectors(ChunkLayoutLookup<ChunkVector, IslandChunkData> chunkLayout)
    {
        return new IslandConnectorData(
            Array.Empty<EntityIO<LocalChunkPivot, IIslandConnector>>(),
            chunkLayout.ChunkPositions);
    }
}