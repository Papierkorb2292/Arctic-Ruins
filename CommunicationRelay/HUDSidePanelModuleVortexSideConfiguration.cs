using System;
using System.Collections.Generic;
using Core.Localization;
using ShapezShifter.Textures;
using Unity.Core.View;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ArcticRuins.CommunicationRelay;

public class HUDSidePanelModuleVortexSideConfiguration : HUDSidePanelModule
{
    private static readonly PrefabViewReference<HUDSidePanelModule> Prefab = new(GeneratePrefab());

    public GameObject vortexPanel;
    public GameObject vortex;

    private Action<TileDirection> _onDirectionSelected;
    private List<(TileDirection, HUDSidePanelModuleShapeButton)> _buttons = [];

    private IShapeIdManager _shapeIdManager;
    private IShapeRegistry _shapeRegistry;
    
    public override void OnDispose()
    {
        
    }

    [Core.Dependency.Construct]
    public void Construct(IShapeRegistry shapeRegistry, IShapeIdManager shapeIdManager)
    {
        _shapeIdManager = shapeIdManager;
        _shapeRegistry = shapeRegistry;
        AddButton("ui.arctic-ruins.vortex-configuration.north", new Vector2(0.5f, 0.9f), TileDirection.North);
        AddButton("ui.arctic-ruins.vortex-configuration.east", new Vector2(0.9f, 0.5f), TileDirection.East);
        AddButton("ui.arctic-ruins.vortex-configuration.south", new Vector2(0.5f, 0.1f), TileDirection.South);
        AddButton("ui.arctic-ruins.vortex-configuration.west", new Vector2(0.1f, 0.5f), TileDirection.West);
        UpdateSelectedShapes();
    }

    private void AddButton(string title, Vector2 anchor, TileDirection direction)
    {
        var button = RequestChildView(HUDSidePanelModuleShapeButton.Prefab).PlaceAt(vortexPanel.transform);
        _buttons.Add((direction, button));
        var buttonTransform = (RectTransform)button.transform;
        buttonTransform.anchorMin = buttonTransform.anchorMax = anchor;
        if(direction == TileDirection.North)
            button.SetHighlighted(true);
        button.ConfigureButton(title.T(), () =>
        {
            foreach (var (_, createdButton) in _buttons)
            {
                createdButton.SetHighlighted(createdButton == button);
            }
            _onDirectionSelected(direction);
        }, null);
    }

    public override void InitFromData(IHUDSidePanelModuleData rawData)
    {
        if (rawData is not Data data)
            throw new Exception("Invalid data");
        _onDirectionSelected = data.OnDirectionSelected;
        data.ShapeUpdatedCallbackConsumer(UpdateSelectedShapes);
        _onDirectionSelected(TileDirection.North);
    }

    public void UpdateSelectedShapes()
    {
        foreach (var (dir, button) in _buttons)
        {
            var hash = ArcticRuinsMod.Instance.SaveData.GetShapeForVortexSide(dir);
            button.UpdateShape(hash == null ? null : _shapeRegistry.GetItem(_shapeIdManager.Resolve(hash)));
        }
    }

    public void Update()
    {
        vortex.transform.rotation *= Quaternion.AngleAxis(25 * Time.deltaTime, Vector3.forward);
    }

    private static HUDSidePanelModule GeneratePrefab()
    {
        var gameObject = new GameObject("HudSidePanelModuleVortexConfigurationPrefab", typeof(RectTransform));
        var module = gameObject.AddComponent<HUDSidePanelModuleVortexSideConfiguration>();
        module.InitializeModule();
        return module;
    }

    private void InitializeModule()
    {
        ((RectTransform)transform).sizeDelta += Vector2.up * 50;

        vortexPanel = new GameObject("VortexContainer", typeof(RectTransform));
        vortexPanel.layer = LayerMask.NameToLayer("UI");
        vortexPanel.transform.parent = transform;
        var vortexPanelTransform = vortexPanel.GetComponent<RectTransform>();
        vortexPanelTransform.sizeDelta = new Vector2(130, 130);
        vortexPanelTransform.anchoredPosition = vortexPanelTransform.anchorMin = vortexPanelTransform.anchorMax = new Vector2(0.5f, 1);
        vortexPanelTransform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        vortexPanelTransform.localScale = new Vector3(1f, 1f, 1f);
        vortexPanelTransform.localRotation = Quaternion.identity;

        vortex = new GameObject("VortexTexture");
        vortex.layer = LayerMask.NameToLayer("UI");
        vortex.transform.parent = vortexPanel.transform;
        var vortexImage = vortex.AddComponent<Image>();
        vortexImage.sprite =
            FileTextureLoader.LoadTextureAsSprite(ArcticRuinsMod.Instance.Resources.SubPath("vortex_ui.png"),
                out _);
        vortexImage.raycastTarget = false;
        vortexImage.maskable = true;
        vortexImage.material = Globals.Resources.DefaultUISpriteMaterial.GetMaterialInternal();
        var vortexTransform = vortex.GetComponent<RectTransform>();
        vortexTransform.sizeDelta = new Vector2(130, 130);
        vortexTransform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        vortexTransform.localScale = new Vector3(1f, 1f, 1f);
        vortexTransform.localRotation = Quaternion.identity;
        
        var compass = new GameObject("CompassTexture");
        compass.layer = LayerMask.NameToLayer("UI");
        compass.transform.parent = vortexPanel.transform;
        var compassImage = compass.AddComponent<Image>();
        compassImage.sprite =
            FileTextureLoader.LoadTextureAsSprite(ArcticRuinsMod.Instance.Resources.SubPath("vortex_ui_compass.png"),
                out _);
        compassImage.raycastTarget = false;
        compassImage.maskable = true;
        compassImage.material = Globals.Resources.DefaultUISpriteMaterial.GetMaterialInternal();
        var compassTransform = compassImage.GetComponent<RectTransform>();
        compassTransform.sizeDelta = new Vector2(220, 220);
        compassTransform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        compassTransform.localScale = new Vector3(1f, 1f, 1f);
        compassTransform.localRotation = Quaternion.identity;
    }

    public class Data(Action<TileDirection> onDirectionSelected, Action<Action> shapeUpdatedCallbackConsumer) : IHUDSidePanelModuleData
    {
        public Action<TileDirection> OnDirectionSelected { get; set; } = onDirectionSelected;
        public Action<Action> ShapeUpdatedCallbackConsumer => shapeUpdatedCallbackConsumer;
        
        public PrefabViewReference<HUDSidePanelModule> GetViewPrefabReference()
        {
            return Prefab;
        }
    }
}