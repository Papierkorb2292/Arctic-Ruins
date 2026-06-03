using System;
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
    private static readonly (PlacementKeybindingHintData, Vector2)[] Buttons =
    [
        (new PlacementKeybindingHintData
        {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.north".T(),
            Handler = () => { ArcticRuinsMod.Logger.Info!.Log("North"); }
        }, new Vector2(0.5f, 0.9f)),
        (new PlacementKeybindingHintData
            {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.east".T(),
            Handler = () => { ArcticRuinsMod.Logger.Info!.Log("East"); }
        }, new Vector2(0.9f, 0.5f)),
        (new PlacementKeybindingHintData
            {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.south".T(),
            Handler = () => { ArcticRuinsMod.Logger.Info!.Log("South"); }
        }, new Vector2(0.5f, 0.1f)),
        (new PlacementKeybindingHintData
        {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.west".T(),
            Handler = () => { ArcticRuinsMod.Logger.Info!.Log("West"); }
        },new Vector2(0.1f, 0.5f))
    ];

    public GameObject vortexPanel;
    public GameObject vortex;

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
        foreach (var (action, anchor) in Buttons)
        {
            var button = RequestChildView(HUDSidePanelModuleShapeButton.Prefab).PlaceAt(vortexPanel.transform);
            var buttonTransform = (RectTransform)button.transform;
            buttonTransform.anchorMin = buttonTransform.anchorMax = anchor;
            button.UpdateButton(action.OverrideTitle, action.Handler, null);
        }
    }

    public override void InitFromData(IHUDSidePanelModuleData rawData)
    {
        if (rawData is not Data data)
            throw new Exception("Invalid data");
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

    public class Data : IHUDSidePanelModuleData
    {
        public PrefabViewReference<HUDSidePanelModule> GetViewPrefabReference()
        {
            return Prefab;
        }
    }
}