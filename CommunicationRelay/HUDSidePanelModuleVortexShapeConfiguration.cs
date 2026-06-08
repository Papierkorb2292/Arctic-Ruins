using System;
using System.Collections.Generic;
using Core.Localization;
using Game.Core.Localization;
using JetBrains.Annotations;
using Unity.Core.View;
using UnityEngine;
using UnityEngine.UI;

namespace ArcticRuins.CommunicationRelay;

public class HUDSidePanelModuleVortexShapeConfiguration : HUDSidePanelModule
{
    private static readonly PrefabViewReference<HUDSidePanelModule> Prefab = new(GeneratePrefab());

    private List<(string, HUDSidePanelModuleShapeButton)> _shapeButtons = [];
    private Action<ResearchCostShapes> _onShapeSelected;
    
    public override void OnDispose()
    {
    }

    [Core.Dependency.Construct]
    public void Construct(IShapeRegistry shapeRegistry, IShapeIdManager shapeIdManager, ResearchLevelManager researchLevelManager)
    {
        var layout = gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(73f, 73f);
        var fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var shapeNumber = 0;
        foreach(var researchLevel in researchLevelManager.Progression.Levels)
        {
            if (researchLevel == researchLevelManager.CurrentLevel)
                break;
            foreach (var line in researchLevel.Lines)
            {
                foreach (var shape in line.Costs)
                {
                    var button = RequestChildView(HUDSidePanelModuleShapeButton.Prefab).PlaceAt(transform);
                    button.ConfigureButton(
                        new CombinedText(
                            "ui.arctic-ruins.vortex-configuration.shape".T(),
                            new GenericFormattedNumberText(new GenericIntegerFormatter(++shapeNumber))
                        ),
                        () =>
                        {
                            SetSelected(shape.ShapeHash);
                            _onShapeSelected(shape);
                        },
                        shapeRegistry.GetItem(shapeIdManager.Resolve(shape.ShapeHash))
                    );
                    button.button.UIButton.TooltipText = new CombinedText(
                        new GenericFormattedNumberText(new GenericIntegerFormatter((int)shape.Amount)),
                        ("ui.arctic-ruins.vortex-configuration.belts." + (shape.Amount == 1 ? "singular" : "plural")).T()
                    );
                    _shapeButtons.Add((shape.ShapeHash, button));
                }
            }
        }
    }

    public void SetSelected([CanBeNull] string shapeHash)
    {
        foreach (var (hash, button) in _shapeButtons)
        {
            button.SetHighlighted(shapeHash == hash);
        }
    }

    public override void InitFromData(IHUDSidePanelModuleData rawData)
    {
        if (rawData is not Data data)
            throw new Exception("Invalid data");
        _onShapeSelected = data.OnShapeSelected;
        data.ShapeChangeCallbackConsumer(SetSelected);
    }
    
    private static HUDSidePanelModule GeneratePrefab()
    {
        var gameObject = new GameObject("HudSidePanelModuleVortexShapeConfigurationPrefab", typeof(RectTransform));
        return gameObject.AddComponent<HUDSidePanelModuleVortexShapeConfiguration>();
    }

    public class Data(Action<ResearchCostShapes> onShapeSelected, Action<Action<string>> shapeChangeCallbackConsumer) : IHUDSidePanelModuleData
    {
        public Action<ResearchCostShapes> OnShapeSelected => onShapeSelected;
        public Action<Action<string>> ShapeChangeCallbackConsumer => shapeChangeCallbackConsumer;
        
        public PrefabViewReference<HUDSidePanelModule> GetViewPrefabReference()
        {
            return Prefab;
        }
    }
}