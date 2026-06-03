using System.Collections.Generic;

namespace ArcticRuins.CommunicationRelay;

public class CommunicationRelayModules : IBuildingModules
{
    
    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IMapModel map, BuildingModel building)
    {
        yield return new HUDSidePanelModuleVortexSideConfiguration.Data();
        yield return new HUDSidePanelModuleVortexShapeConfiguration.Data();
    }

    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IBuildingDefinition definition) => [];
}