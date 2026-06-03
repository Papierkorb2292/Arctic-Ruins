using System;
using Core.Localization;
using JetBrains.Annotations;
using ShapezShifter.Textures;
using Unity.Core.View;
using UnityEngine;
using UnityEngine.UI;

namespace ArcticRuins.CommunicationRelay;

public class HUDSidePanelModuleShapeButton : HUDComponent
{
    private static Sprite _highlightSprite = FileTextureLoader.LoadTextureAsSprite(ArcticRuinsMod.Instance.Resources.SubPath("rounded_corners.png"), out _);
    public static PrefabViewReference<HUDSidePanelModuleShapeButton> Prefab = new(GeneratePrefab());

    public HUDSidePanelModuleActionButton button;
    public Button infoButton;
    public Image highlight;
    
    private IBeltItemRenderer _beltItemRenderer;
    private HUDEvents _hudEvents;

    private ShapeItem _currentItem;
    private Action _currentAction;
    
    public override void OnDispose()
    {
        
    }

    [Core.Dependency.Construct]
    public void Construct(IBeltItemRenderer beltItemRenderer, HUDEvents hudEvents)
    {
        _beltItemRenderer = beltItemRenderer;
        _hudEvents = hudEvents;
        _currentItem = null;
        
        var buttonPrefab =
            ((HUDSidePanelModuleActionButtons)Globals.Resources.HUDSidePanelModulesResources.ActionButtons.ViewPrefab)
            .UIButtonPrefab;
        var infoButtonTemplate =
            ((HUDSidePanelModuleBeltItemContents)Globals.Resources.HUDSidePanelModulesResources.BeltItemContents
                .ViewPrefab)
            .UISlots[0].UIInfoButton;
        
        button = RequestChildView(buttonPrefab).PlaceAt(transform);
        button.Action = new PlacementKeybindingHintData
        {
            Handler = () => { _currentAction?.Invoke(); }
        };
        var buttonTransform = (RectTransform)button.transform;
        buttonTransform.anchorMin = buttonTransform.anchorMax = new Vector2(0.5f, 0.5f);
        infoButton = Instantiate(infoButtonTemplate, buttonTransform);
        var infoButtonTransform = (RectTransform)infoButton.transform; 
        infoButtonTransform.sizeDelta = new Vector2(20, 20);
        infoButtonTransform.anchorMin = infoButtonTransform.anchorMax = new Vector2(1.1f, -0.1f);
        infoButton.GetComponent<Image>().raycastPadding = Vector4.zero;
        infoButton.onClick.AddListener(() =>
        {
            if(_currentItem != null)
                _hudEvents.ShowShapeViewer.Invoke(_currentItem.Definition.Id);
        });
        infoButton.gameObject.SetActive(false);
        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        highlight.enabled = highlighted;
    }

    public void UpdateButton(IText title, Action action, [CanBeNull] ShapeItem shapeItem)
    {
        button.UIButton.TooltipTitle = title;
        _currentAction = action;
        _currentItem = shapeItem;
        var texture = shapeItem == null ? Texture2D.whiteTexture : (Texture2D)_beltItemRenderer.RenderToTexture(shapeItem);
        button.UIButton.Icon = Sprite.Create(texture, new Rect(0.2f * texture.width, 0.2f * texture.height, 0.6f * texture.width, 0.6f * texture.height), new Vector2(texture.width / 2f, texture.height / 2f));
        button.UIButton.UIIcon.color = shapeItem != null ? Color.white : Color.clear;
        infoButton.gameObject.SetActive(shapeItem != null);
    }
    
    private static HUDSidePanelModuleShapeButton GeneratePrefab()
    {
        var gameObject = new GameObject("HUDSidePanelModuleShapeButtonPrefab", typeof(RectTransform));
        var module = gameObject.AddComponent<HUDSidePanelModuleShapeButton>();
        var highlightObject = new GameObject("Highlight", typeof(RectTransform));
        var highlightTransform = (RectTransform)highlightObject.transform;
        highlightTransform.parent = gameObject.transform;
        highlightTransform.sizeDelta = new Vector2(50, 50);
        module.highlight = highlightObject.AddComponent<Image>();
        module.highlight.sprite = _highlightSprite;
        module.highlight.raycastTarget = false;
        module.highlight.maskable = true;
        module.highlight.color = new Color(1,1,1,0.1f);
        return module;
    }
}