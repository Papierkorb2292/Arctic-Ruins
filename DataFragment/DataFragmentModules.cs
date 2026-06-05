using System.Collections.Generic;
using Core.Localization;
using UnityEngine;

namespace ArcticRuins.DataFragment;

public class DataFragmentModules : IBuildingModules
{
    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IMapModel map, BuildingModel building)
    {
        var layout = StaticGameCoreAccessor.G.Research.Layout;
        // TODO: Select research
        var rewards = layout._Levels[0].Rewards;
        var tech = rewards[Random.Range(0, rewards.Count)];
        yield return new HUDSidePanelModuleDataFragmentInfo.Data(tech);
        yield return new HUDSidePanelModuleGenericButton.Data("ui.arctic-ruins.data-fragment.unlock".T(), () =>
        {
            // TODO: Unlock research       
        });
    }

    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IBuildingDefinition definition) => [];
}