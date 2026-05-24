using Game.Core.Coordinates;

namespace ArcticRuins.ShapeAsteroidStabilizer;

public class StabilizerOnResourcePatchPlacementRequirement : IBuildingPlacementRequirement
{
    private readonly OnlyOnShapeResourcePatchPlacementRequirement _vanillaRequirement = new();
    
    public bool Check(IMapModel map, BuildingDescriptor building, IslandDescriptor island)
    {
        var islandLayoutQuery = new IslandLayoutQuery(island, map.MaxBuildingLayer);
        var tile_I = building.Transform.Position.ToIslandCoordinate(in island.Transform.Position);
        return islandLayoutQuery.IsValidAndBuildableTile_I(in tile_I) && _vanillaRequirement.IsValidExtractorPlacement(map, island, building.Transform.Position, building.Transform.Rotation);
    }
}