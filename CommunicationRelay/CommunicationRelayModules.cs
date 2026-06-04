using System;
using System.Collections.Generic;

namespace ArcticRuins.CommunicationRelay;

public class CommunicationRelayModules : IBuildingModules
{
    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IMapModel map, BuildingModel building)
    {
        TileDirection? selectedDirection = null;
        Action sideConfigurationUpdater = null;
        Action<string> shapeSelector = null;
        yield return new HUDSidePanelModuleVortexSideConfiguration.Data(direction =>
        {
            selectedDirection = direction;
            var selectedShape = ArcticRuinsMod.Instance.SaveData.GetShapeForVortexSide(direction);
            shapeSelector?.Invoke(selectedShape);
        }, action =>
        {
            sideConfigurationUpdater = action;
        });
        yield return new HUDSidePanelModuleVortexShapeConfiguration.Data(shape =>
        {
            ArcticRuinsMod.Instance.SaveData.SetShapeForVortexSide(selectedDirection!.Value, shape);
            sideConfigurationUpdater();
        }, action =>
        {
            action(ArcticRuinsMod.Instance.SaveData.GetShapeForVortexSide(selectedDirection!.Value));
            shapeSelector = action;
        });
    }

    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IBuildingDefinition definition) => [];
}