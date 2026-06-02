using System;
using Core.Localization;
using ShapezShifter.Textures;
using Unity.Core.View;
using UnityEngine;
using UnityEngine.UI;

namespace ArcticRuins.CommunicationRelay;

public class HudSidePanelModuleVortexConfiguration : HUDSidePanelModule
{
    private static readonly PrefabViewReference<HUDSidePanelModule> Prefab = new(GeneratePrefab());
    private static readonly (PlacementKeybindingHintData, Vector2)[] Buttons =
    [
        (new PlacementKeybindingHintData
        {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.north".T(),
            Handler = () => { }
        }, new Vector2(0.5f, 1.2f)),
        (new PlacementKeybindingHintData
            {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.east".T(),
            Handler = () => { }
        }, new Vector2(1.2f, 0.5f)),
        (new PlacementKeybindingHintData
            {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.south".T(),
            Handler = () => { }
        }, new Vector2(0.5f, -0.2f)),
        (new PlacementKeybindingHintData
        {
            OverrideTitle = "ui.arctic-ruins.vortex-configuration.west".T(),
            Handler = () => { }
        },new Vector2(-0.2f, 0.5f))
    ];

    public GameObject voortexPanel;
    public GameObject vortex;
    
    public override void OnDispose()
    {
        
    }

    public override void InitFromData(IHUDSidePanelModuleData rawData)
    {
        if (rawData is not Data data)
            throw new Exception("Invalid data");
        var buttonPrefab =
            ((HUDSidePanelModuleActionButtons)Globals.Resources.HUDSidePanelModulesResources.ActionButtons.ViewPrefab)
            .UIButtonPrefab;
        foreach (var (action, anchor) in Buttons)
        {
            var button = RequestChildView(buttonPrefab).PlaceAt(voortexPanel.transform);
            button.Action = action;
            var buttonTransform = ((RectTransform)button.transform);
            buttonTransform.anchorMin = buttonTransform.anchorMax = anchor;
        }
    }

    public void Update()
    {
        vortex.transform.rotation *= Quaternion.AngleAxis(25 * Time.deltaTime, Vector3.forward);
    }

    private static HUDSidePanelModule GeneratePrefab()
    {
        var gameObject = new GameObject("HudSidePanelModuleVortexConfigurationPrefab", typeof(RectTransform));
        var module = gameObject.AddComponent<HudSidePanelModuleVortexConfiguration>();
        module.InitializeModule();
        return module;
    }

    private void InitializeModule()
    {
        ((RectTransform)transform).sizeDelta += Vector2.up * 50;

        voortexPanel = new GameObject("VortexContainer", typeof(RectTransform));
        voortexPanel.layer = LayerMask.NameToLayer("UI");
        voortexPanel.transform.parent = transform;
        var vortexPanelTransform = voortexPanel.GetComponent<RectTransform>();
        vortexPanelTransform.sizeDelta = new Vector2(100, 100);
        vortexPanelTransform.anchoredPosition = vortexPanelTransform.anchorMin = vortexPanelTransform.anchorMax = new Vector2(0.5f, 1);
        vortexPanelTransform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        vortexPanelTransform.localScale = new Vector3(1f, 1f, 1f);
        vortexPanelTransform.localRotation = Quaternion.identity;

        vortex = new GameObject("VortexTexture");
        vortex.layer = LayerMask.NameToLayer("UI");
        vortex.transform.parent = voortexPanel.transform;
        var rawImage = vortex.AddComponent<Image>();
        rawImage.sprite =
            FileTextureLoader.LoadTextureAsSprite(ArcticRuinsMod.Instance.Resources.SubPath("vortex_ui.png"),
                out _);
        rawImage.raycastTarget = false;
        rawImage.maskable = true;
        rawImage.material = Globals.Resources.DefaultUISpriteMaterial.GetMaterialInternal();
        var vortexTransform = vortex.GetComponent<RectTransform>();
        vortexTransform.sizeDelta = new Vector2(100, 100);
        vortexTransform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        vortexTransform.localScale = new Vector3(1f, 1f, 1f);
        vortexTransform.localRotation = Quaternion.identity;
    }

    public class Data : IHUDSidePanelModuleData
    {
        public PrefabViewReference<HUDSidePanelModule> GetViewPrefabReference()
        {
            return Prefab;
        }
    }
}