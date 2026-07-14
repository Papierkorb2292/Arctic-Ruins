using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Core.Collections.Scoped;
using Game.Core.Coordinates;
using Game.Interaction.EntitiesPlacement;
using Game.Placement.Data;
using Game.Placement.Processing;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;

namespace ArcticRuins;

public class LockedTiles
{
    public static List<LockedBuildingCondition> LockedBuildings = [];
    public static List<LockedIslandCondition> LockedIslands = [];
    
    private static Hook _drawPendingBuildingSelectionHook;
    private static Hook _drawPendingIslandSelectionHook;
    private static Hook _preparePlacementDataHook;
    private static readonly ConditionalWeakTable<IModularEntityPlacer, object> _patchedPlacers = new();

    public static void Register()
    {
        _drawPendingBuildingSelectionHook = DetourHelper.CreatePrefixHook<HUDBuildingMassSelection, FrameDrawOptions, IReadOnlyCollection<BuildingModel>, HUDMassSelectionSelectionType>(
            (selection, options, buildings, type) => selection.Draw_PendingSelection(options, buildings, type),
            (_, options, buildings, type) =>
            {
                if (buildings is HashSet<BuildingModel> set)
                    set.RemoveWhere(building => LockedBuildings.Any(condition => condition(building.Transform.Position, InteractionType.Select)));
                return (options, buildings, type);
            }
        );
        _drawPendingIslandSelectionHook = DetourHelper.CreatePrefixHook<HUDIslandMassSelection, FrameDrawOptions, IReadOnlyCollection<IslandModel>, HUDMassSelectionSelectionType>(
            (selection, options, islands, type) => selection.Draw_PendingSelection(options, islands, type),
            (_, options, islands, type) =>
            {
                if (islands is HashSet<IslandModel> set)
                    set.RemoveWhere(island => LockedIslands.Any(condition => condition(island.Position, InteractionType.Select)));
                return (options, islands, type);
            }
        );
        _preparePlacementDataHook = DetourHelper.CreatePostfixHook<EntityPlacementRunner, IEntityPlacer>(
            (runner, placer) => runner.PreparePlacementData(placer),
            (runner, _) =>
            {
                // Add processor that invalidates everything that's locked
                if (runner.CurrentPlacer is IModularEntityPlacer placer && !_patchedPlacers.TryGetValue(placer, out var _))
                {
                    _patchedPlacers.AddOrUpdate(placer, placer);
                    ((ICollection<IPlacementProcessor>)placer.PlacementProcessors).Add(new FilterLockedPlacementProcessor());
                }
            });
    }

    public static void HookSessionOrchestrator(GameSessionOrchestrator orchestrator)
    {
        FilterLockedSelections(orchestrator);
    }    

    public static void Dispose()
    {
        _drawPendingBuildingSelectionHook.Dispose();
        _drawPendingIslandSelectionHook.Dispose();
        _preparePlacementDataHook.Dispose();
    }

    private static void FilterLockedSelections(GameSessionOrchestrator orchestrator)
    {
        var buildingSelection = orchestrator.PlayerInteractionOrchestrator.PlayerInteractionState.BuildingSelection;
        var islandSelection = orchestrator.PlayerInteractionOrchestrator.PlayerInteractionState.IslandSelection;
        buildingSelection.OnAdded.Register(buildings =>
        {
            buildingSelection.Remove(buildings.Where(building => LockedBuildings.Any(condition => condition(building.Tile_G, InteractionType.Select))));
        });
        islandSelection.OnAdded.Register(islands =>
        {
            islandSelection.Remove(islands.Where(island => LockedIslands.Any(condition => condition(island.Position, InteractionType.Select))));
        });
    }

    public delegate bool LockedBuildingCondition(GlobalTileCoordinate pos, InteractionType type);
    public delegate bool LockedIslandCondition(GlobalChunkCoordinate pos, InteractionType type);

    public enum InteractionType
    {
        Add, Select
    }
    
    private class FilterLockedPlacementProcessor : IPlacementProcessor
    {
        public void Process(IPlacementData placementData, PlacementInputHolder placementInput, IMapModel realMap,
            IReadOnlyMapLayoutModel virtualMap, IPlacementErrors placementErrors)
        {
            using var buildingsFilter = ScopedList.Get<BuildingPlacement>();
            placementData.GetAllBuildings(buildingsFilter);
            foreach (var building in buildingsFilter.Where(building =>
                         building.PlacementAllowability.WillBePlaced() &&
                         LockedBuildings.Any(condition => condition(building.Descriptor.Transform.Position, InteractionType.Add))))
            {
                placementData.InvalidateBuildingAt(building.Descriptor.Transform.Position);
            }

            using var islandsFilter = ScopedList.Get<IslandPlacement>();
            placementData.GetAllIslands(islandsFilter);
            foreach (var island in islandsFilter.Where(island =>
                         island.PlacementAllowability.WillBePlaced() &&
                         LockedIslands.Any(condition => condition(island.Descriptor.Transform.Position, InteractionType.Add))))
            {
                placementData.InvalidateIslandAt(island.Descriptor.Transform.Position);   
            }
        }
    }
}