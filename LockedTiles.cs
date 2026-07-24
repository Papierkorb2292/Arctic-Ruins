using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
    private static Hook _actionModifyBuildingsIsPossibleHook;
    private static Hook _actionModifyBuildingsExecuteInternalHook;
    private static Hook _actionModifyIslandIsPossibleHook;
    private static Hook _actionModifyIslandExecuteInternalHook;
    private static Hook _preparePlacementDataHook;
    private static readonly ConditionalWeakTable<IModularEntityPlacer, object> _patchedPlacers = new();
    private static readonly ConditionalWeakTable<ActionModifyBuildings, object> _filteredBuildingModify = new();
    private static readonly ConditionalWeakTable<ActionModifyIsland, object> _filteredIslandModify = new();
    

    public static void Register()
    {
        _drawPendingBuildingSelectionHook = DetourHelper.CreatePrefixHook<HUDBuildingMassSelection, FrameDrawOptions, IReadOnlyCollection<BuildingModel>, HUDMassSelectionSelectionType>(
            (selection, options, buildings, type) => selection.Draw_PendingSelection(options, buildings, type),
            (_, options, buildings, type) =>
            {
                if (buildings is HashSet<BuildingModel> set)
                    set.RemoveWhere(building => LockedBuildings.Any(condition => condition(building.Transform.Position, StaticGameCoreAccessor.G.LocalPlayer.CurrentMap, InteractionType.Select)));
                return (options, buildings, type);
            }
        );
        _drawPendingIslandSelectionHook = DetourHelper.CreatePrefixHook<HUDIslandMassSelection, FrameDrawOptions, IReadOnlyCollection<IslandModel>, HUDMassSelectionSelectionType>(
            (selection, options, islands, type) => selection.Draw_PendingSelection(options, islands, type),
            (_, options, islands, type) =>
            {
                if (islands is HashSet<IslandModel> set)
                    set.RemoveWhere(island => LockedIslands.Any(condition => condition(island.Position, StaticGameCoreAccessor.G.LocalPlayer.CurrentMap, InteractionType.Select)));
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
        _actionModifyBuildingsIsPossibleHook = DetourHelper.CreatePrefixHook<ActionModifyBuildings, IInteractionMode, bool>(
                (action, mode) => action.IsPossible(mode),
                (action, mode) =>
                {
                    FilterLockedBuildingModifyAction(action);
                    return mode;
                });
        _actionModifyBuildingsExecuteInternalHook = new Hook(
            typeof(ActionModifyBuildings).GetMethod(nameof(ActionModifyBuildings.ExecuteInternal),
                BindingFlags.NonPublic | BindingFlags.Instance)!,
            (BuildingsExecuteInternalWrapper)((original, action, interactionMode, out reverseAction) =>
            {
                FilterLockedBuildingModifyAction(action);
                original(action, interactionMode, out reverseAction);
            })
        );
        _actionModifyIslandIsPossibleHook = DetourHelper.CreatePrefixHook<ActionModifyIsland, IInteractionMode, bool>(
                (action, mode) => action.IsPossible(mode),
                (action, mode) =>
                {
                    FilterLockedIslandModifyAction(action);
                    return mode;
                });
        _actionModifyIslandExecuteInternalHook = new Hook(
            typeof(ActionModifyIsland).GetMethod(nameof(ActionModifyIsland.ExecuteInternal),
                BindingFlags.NonPublic | BindingFlags.Instance)!,
            (IslandExecuteInternalWrapper)((original, action, interactionMode, out reverseAction) =>
            {
                FilterLockedIslandModifyAction(action);
                original(action, interactionMode, out reverseAction);
            })
        );
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
        _actionModifyBuildingsIsPossibleHook.Dispose();
        _actionModifyBuildingsExecuteInternalHook.Dispose();
        _actionModifyIslandIsPossibleHook.Dispose();
        _actionModifyIslandExecuteInternalHook.Dispose();
    }

    private static void FilterLockedSelections(GameSessionOrchestrator orchestrator)
    {
        var buildingSelection = orchestrator.PlayerInteractionOrchestrator.PlayerInteractionState.BuildingSelection;
        var islandSelection = orchestrator.PlayerInteractionOrchestrator.PlayerInteractionState.IslandSelection;
        var map = orchestrator.MapModel;
        buildingSelection.OnAdded.Register(buildings =>
        {
            buildingSelection.Remove(buildings.Where(building => LockedBuildings.Any(condition => condition(building.Tile_G, map, InteractionType.Select))));
        });
        islandSelection.OnAdded.Register(islands =>
        {
            islandSelection.Remove(islands.Where(island => LockedIslands.Any(condition => condition(island.Position, map, InteractionType.Select))));
        });
    }

    private static void FilterLockedBuildingModifyAction(ActionModifyBuildings action)
    {
        if (!_filteredBuildingModify.TryAdd(action, action)) return;
        // Doesn't have to filter placed buildings, because those are already covered by FilterLockedPlacementProcessor
        var filteredDelete = action.Data.Delete.Where(building =>
        {
            var pos = action.Map.GetBuilding(building.BuildingId).Tile_G;
            return !LockedBuildings.Any(condition => condition(pos, action.Map, InteractionType.Remove));
        }).ToList();
        if(filteredDelete.Count != action.Data.Delete.Count)
            action.Data.Set(payload => payload.Delete, filteredDelete);
    }
    
    private static void FilterLockedIslandModifyAction(ActionModifyIsland action)
    {
        if (!_filteredIslandModify.TryAdd(action, action)) return;
        // Doesn't have to filter placed islands, because those are already covered by FilterLockedPlacementProcessor
        var filteredDelete = action.Data.Delete.Where(island =>
        {
            var pos = action.Map.GetIsland(island.IslandId).Position;
            return !LockedIslands.Any(condition => condition(pos, action.Map, InteractionType.Remove));
        }).ToList();
        if(filteredDelete.Count != action.Data.Delete.Count)
            action.Data.Set(payload => payload.Delete, filteredDelete);
    }

    public delegate bool LockedBuildingCondition(GlobalTileCoordinate pos, IMapModel map, InteractionType type);
    public delegate bool LockedIslandCondition(GlobalChunkCoordinate pos, IMapModel map, InteractionType type);

    public enum InteractionType
    {
        Add, Select, Remove
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
                         LockedBuildings.Any(condition => condition(building.Descriptor.Transform.Position, realMap, InteractionType.Add))))
            {
                placementData.InvalidateBuildingAt(building.Descriptor.Transform.Position);
            }

            using var islandsFilter = ScopedList.Get<IslandPlacement>();
            placementData.GetAllIslands(islandsFilter);
            foreach (var island in islandsFilter.Where(island =>
                         island.PlacementAllowability.WillBePlaced() &&
                         LockedIslands.Any(condition => condition(island.Descriptor.Transform.Position, realMap, InteractionType.Add))))
            {
                placementData.InvalidateIslandAt(island.Descriptor.Transform.Position);   
            }
        }
    }

    private delegate void BuildingsExecuteInternal(ActionModifyBuildings action, IInteractionMode interactionMode, out IPlayerAction reverseAction);
    private delegate void BuildingsExecuteInternalWrapper(BuildingsExecuteInternal original, ActionModifyBuildings action, IInteractionMode interactionMode, out IPlayerAction reverseAction);
    private delegate void IslandExecuteInternal(ActionModifyIsland action, IInteractionMode interactionMode, out IPlayerAction reverseAction);
    private delegate void IslandExecuteInternalWrapper(IslandExecuteInternal original, ActionModifyIsland action, IInteractionMode interactionMode, out IPlayerAction reverseAction);
}