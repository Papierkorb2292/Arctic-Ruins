using System;
using System.Collections.Generic;
using System.Linq;
using Core.Collections.Scoped;
using Core.Localization;
using Unity.Core.View;
using UnityEngine;
using UnityEngine.UI;

namespace ArcticRuins.DataFragment;

public class HUDSidePanelModuleDataFragmentInfo : HUDSidePanelModule
{
    private static readonly PrefabViewReference<HUDSidePanelModule> Prefab = new(GeneratePrefab());

    private HUDGenericIconWithLabel _label;
    private HUDSidePanelModule _text;
    private IGameData _gameData;
    private GameMode _mode;
    private ResearchManager _research;

    public override void OnDispose()
    {
    }

    [Core.Dependency.Construct]
    public void Construct(IGameData gameData, GameMode mode, ResearchManager research)
    {
        _gameData = gameData;
        _mode = mode;
        _research = research;
        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        _text = RequestChildView(Globals.Resources.HUDSidePanelModulesResources.InfoText).PlaceAt(transform);
        _label = RequestChildView(DataFragmentBuilding.UIRewardPrefab).PlaceAt(transform);
        //((RectTransform)transform).SetHeight(((RectTransform)_label.transform).sizeDelta.y);
    }

    public override void InitFromData(IHUDSidePanelModuleData rawData)
    {
        if (rawData is not Data data)
            throw new Exception("Invalid data");
        _text.InitFromData(new HUDSidePanelModuleInfoText.Data((
            "ui.arctic-ruins.data-fragment." + (data.IsUnlocked ? "unlocked-tech-info" : "tech-info")).T()
        ));
        var icons = Globals.Resources.Icons;
        using var rewards = ScopedList.Get<IResearchReward>();
        rewards.Add(data.Tech);
        using var scopedList = ScopedList<HUDResearchRewardHelpers.RewardDisplayEntry>.Get();
        HUDResearchRewardHelpers.CollectContentRewards(rewards, _mode, _gameData, _research.Layout, icons, scopedList);
        HUDResearchRewardHelpers.CollectCurrencyRewards(rewards, icons, scopedList);
        if (scopedList.Count == 0)
            return;
        var entry = scopedList[0];
        _label.Label = entry.Title;
        _label.Icon = entry.Icon;
        _label.TooltipTitle = entry.TooltipTitle;
        _label.TooltipText = entry.TooltipDescription;
    }
    
    private static HUDSidePanelModule GeneratePrefab()
    {
        var gameObject = new GameObject("HudSidePanelModuleDataFragmentInfoPrefab", typeof(RectTransform));
        return gameObject.AddComponent<HUDSidePanelModuleDataFragmentInfo>();
    }

    public class Data(IResearchReward tech, bool isUnlocked) : IHUDSidePanelModuleData
    {
        public IResearchReward Tech => tech;
        public bool IsUnlocked => isUnlocked;
        
        public PrefabViewReference<HUDSidePanelModule> GetViewPrefabReference()
        {
            return Prefab;
        }
    }
}