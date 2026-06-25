using System.Collections.Generic;

namespace ArcticRuins.Ruins;

public class RuinsModules : IBuildingModules
{
    // Ruins don't have any modules
    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IMapModel map, BuildingModel building)
    {
        return [];
    }

    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IBuildingDefinition definition)
    {
        return [];
    }
}