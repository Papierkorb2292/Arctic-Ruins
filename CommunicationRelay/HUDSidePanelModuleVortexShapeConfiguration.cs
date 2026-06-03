using System;
using Core.Localization;
using Game.Core.Localization;
using Unity.Core.View;
using UnityEngine;
using UnityEngine.UI;

namespace ArcticRuins.CommunicationRelay;

public class HUDSidePanelModuleVortexShapeConfiguration : HUDSidePanelModule
{
    private static readonly PrefabViewReference<HUDSidePanelModule> Prefab = new(GeneratePrefab());
    
    public override void OnDispose()
    {
        
        
    }

    [Core.Dependency.Construct]
    public void Construct(IShapeRegistry shapeRegistry, IShapeIdManager shapeIdManager)
    {
        var layout = gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(73f, 73f);
        var fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for(int i = 0; i < 10; i++)
        {
            //TODO: Add available belt count
            var button = RequestChildView(HUDSidePanelModuleShapeButton.Prefab).PlaceAt(transform);
            button.UpdateButton(new CombinedText([
                "ui.arctic-ruins.vortex-configuration.shape".T(),
                new GenericFormattedNumberText(new GenericIntegerFormatter(i + 1))
            ]), () => { }, shapeRegistry.GetItem(shapeIdManager.Resolve("RuRuRuRu")));
        }
    }

    public override void InitFromData(IHUDSidePanelModuleData rawData)
    {
        if (rawData is not Data data)
            throw new Exception("Invalid data");
    }
    
    private static HUDSidePanelModule GeneratePrefab()
    {
        var gameObject = new GameObject("HudSidePanelModuleVortexShapeConfigurationPrefab", typeof(RectTransform));
        return gameObject.AddComponent<HUDSidePanelModuleVortexShapeConfiguration>();
    }

    public class Data : IHUDSidePanelModuleData
    {
        public PrefabViewReference<HUDSidePanelModule> GetViewPrefabReference()
        {
            return Prefab;
        }
    }
}