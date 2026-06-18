using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Core.Localization;
using Game.Core.Localization;
using Game.Core.Research;
using MonoMod.RuntimeDetour;
using ShapezShifter.Hijack;
using ShapezShifter.SharpDetour;
using ShapezShifter.Textures;
using UnityEngine;

namespace ArcticRuins;

public static class MilestoneReverser
{
    private static Hook _milestoneCanUnlockHook;
    private static Hook _milestoneTryUnlockHook;
    private static Hook _includeUnlockedChunkLimitInRecomputationHook;
    private static Hook _markCompletedMilestoneRewardsHook;
    private static Hook _shapeCostTextHook;
    private static Hook _hideMilestoneSummaryHook;

    private static HUDResearchTabLevels _tabLevels;
    private static readonly ConditionalWeakTable<HUDResearchShapeCostDisplay, object> CostDisplaysWithBeltText = new();
    private static readonly ConditionalWeakTable<ResearchProgression, GameScenario> CustomProgressions = new();
    
    public static void Register()
    {
        _milestoneCanUnlockHook = new Hook(DetourHelper.GetRuntimeMethod<ResearchUnlockManager>(
            (Expression<Func<ResearchUnlockManager, IResearchUpgrade, bool>>)((unlockManager, upgrade) => unlockManager.CanUnlock(upgrade))),
                (Func<Func<ResearchUnlockManager, IResearchUpgrade, bool>, ResearchUnlockManager, IResearchUpgrade, bool>)((orig, unlockManager, upgrade) =>
                {
                    if(!IsCustomProgression(unlockManager.Layout) || upgrade is not ResearchLevel level)
                        return orig(unlockManager, upgrade);
                    return orig(unlockManager, new LevelWrapper(level)) && HasUnlockedLevelRewards(level, unlockManager);
                })
            );
        _milestoneTryUnlockHook = new Hook(DetourHelper.GetRuntimeMethod<ResearchUnlockManager>(
                (Expression<Func<ResearchUnlockManager, IResearchUpgrade, bool, bool>>)((unlockManager, upgrade, force) => unlockManager.TryUnlock(upgrade, force))), 
            (Func<Func<ResearchUnlockManager, IResearchUpgrade, bool, bool>, ResearchUnlockManager, IResearchUpgrade, bool, bool>)
            ((orig, unlockManager, upgrade, force) => orig(unlockManager, !IsCustomProgression(unlockManager.Layout) || upgrade is not ResearchLevel level ? upgrade : new LevelWrapper(level), force))
        );
        _includeUnlockedChunkLimitInRecomputationHook = DetourHelper.CreatePostfixHook<ResearchUnlockProgressManager>(
            manager => manager.RecomputeUnlocks(),
            manager =>
            {
                if (!IsCustomProgression(manager.Progression)) return;
                manager._CachedUnlockedRewards.UnionWith(ArcticRuinsMod.Instance.SaveData.Tech.UnlockedRewards
                    .Select(techReference => manager.Progression.Levels[techReference.Level].Rewards[techReference.Index])
                    .OfType<ResearchRewardChunkLimit>());
            });
        _markCompletedMilestoneRewardsHook = DetourHelper.CreatePostfixHook<HUDResearchTabLevels, ResearchManager, GameMode>(
            (tabLevels, research, mode) => tabLevels.Construct(research, mode),
            (tabLevels, research, mode) =>
            {
                if (!IsCustomProgression(research.Layout)) return;
                
                tabLevels.Instances[0].UIProgressConnectorCompletedInitial =
                    FileTextureLoader.LoadTextureAsSprite(
                        ArcticRuinsMod.Instance.Resources.SubPath("HUDResearchConnectorInitialWithCost.png"), out var _);
                tabLevels.Instances[0].SetViewToState(tabLevels.Instances[0].State); // Update with new sprite
                
                foreach (var level in tabLevels.Instances)
                {
                    // Rebuild reward list to exclude computed rewards, since they aren't unlocked through data fragments
                    level.UIRewardsDisplay.Rewards = level.Level.Rewards;
                    
                    // Swap location of rewards and costs. Honestly didn't expect this to work so well
                    var costs = (RectTransform)((HUDComponent)level.UICostsOverviewView).transform;
                    var rewards = (RectTransform)level.UIRewardsDisplay.transform;
                    
                    var rewardsParent = rewards.parent;
                    rewards.SetParent(costs.parent, false);
                    costs.SetParent(rewardsParent, false);
                    
                    (rewards.anchorMin, costs.anchorMin) = (costs.anchorMin, rewards.anchorMin);
                    (rewards.anchorMax, costs.anchorMax) = (costs.anchorMax, rewards.anchorMax);
                    (rewards.anchoredPosition, costs.anchoredPosition) =
                        (costs.anchoredPosition, rewards.anchoredPosition);
                    (rewards.sizeDelta, costs.sizeDelta) = (costs.sizeDelta, rewards.sizeDelta);
                    rewards.sizeDelta -= new Vector2(25, 70);
                    
                    // Save all shape cost displays where the text should be changed
                    // Change shape cost text to belt count
                    var shapeCostDisplays = ((HUDResearchLevelCostsOverview)level.UICostsOverviewView)
                        .UILineInstances.SelectMany(line => line.UICostInstances)
                        .OfType<HUDResearchShapeCostDisplay>();
                    foreach (var shapeCostDisplay in shapeCostDisplays)
                    {
                        if (!shapeCostDisplay.UIHasProgressText) continue;
                        CostDisplaysWithBeltText.Add(shapeCostDisplay, shapeCostDisplay);
                        shapeCostDisplay.SetState(shapeCostDisplay.State); // Update
                    }
                }
                
                // Color all unlocked rewards and save hud instance to add new unlocked rewards later 
                _tabLevels = tabLevels;
                foreach (var tech in ArcticRuinsMod.Instance.SaveData.Tech.UnlockedRewards)
                {
                    MarkMilestoneRewardUnlocked(tech, research.Layout);
                }
                // Initial rewards are always unlocked
                foreach (var label in tabLevels.Instances[0].UIRewardsDisplay.Instances)
                {
                    label.UITitle.Color = Color.green;
                }
            });
        _shapeCostTextHook = DetourHelper
            .CreatePostfixHook<HUDResearchShapeCostDisplay, HUDResearchShapeCostDisplay.DeliveryState>(
                (display, state) => display.SetState(state),
                (display, _) =>
                {
                    if (!CostDisplaysWithBeltText.TryGetValue(display, out var _))
                        return;
                    display.UIProgressText!.gameObject.SetActiveSelfExt(true);
                    var amount = ((ResearchCostShapes)display.Cost).Amount;
                    display.UIProgressText!.Text = new CombinedText(
                        new GenericFormattedNumberText(new GenericIntegerFormatter((int)amount)),
                        ("ui.arctic-ruins.vortex-configuration.belts." +
                         (amount == 1 ? "singular" : "plural")).T()
                    );
                });
        _hideMilestoneSummaryHook = DetourHelper.CreatePostfixHook<HUDMilestoneSummaryDisplay>(
            display => display.RebuildView(),
            display =>
            {
                if (!IsCustomProgression(display.ResearchManager.Layout)) return;
                display.ReleaseChildViews(display.Instances);
                display.Instances.Clear();
            });
        
        GameRewirers.AddRewirer(new CustomProgressionDiscoverer());
    }

    public static void Dispose()
    {
        _milestoneCanUnlockHook.Dispose();
        _milestoneTryUnlockHook.Dispose();
        _includeUnlockedChunkLimitInRecomputationHook.Dispose();
        _markCompletedMilestoneRewardsHook.Dispose();
        _shapeCostTextHook.Dispose();
        _hideMilestoneSummaryHook.Dispose();
    }

    private static bool IsCustomProgression(ResearchProgression progression)
    {
        return CustomProgressions.TryGetValue(progression, out _);
    }

    private static bool HasUnlockedLevelRewards(ResearchLevel level, ResearchUnlockManager unlockManager)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        var levelIndex = unlockManager.Layout.Levels.FindIndex(level2 => level2.Id == level.Id);
        return Enumerable.Range(0, level.Rewards.Count).All(rewardIndex => techTracker.UnlockedRewards.Contains(new SaveData.TechReference(levelIndex, rewardIndex)));
    }
    
    public static SaveData.TechReference? PickNextTech(ResearchProgression layout)
    {
        var rewards = GetRewardQueue(layout);
        if (rewards.Count == 0)
            return null;
        var nextReward = rewards[0];
        rewards.RemoveAt(0);
        return nextReward;
    }

    public static List<SaveData.TechReference> GetRewardQueue(ResearchProgression layout)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        if (techTracker.QueuedRewards == null)
            (techTracker.QueuedRewards, techTracker.RewardCountPerLevel) = GenerateRewardQueue(layout);
        return techTracker.QueuedRewards;
    }

    public static List<int> GetLevelRewardCount(ResearchProgression layout)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        if (techTracker.RewardCountPerLevel == null)
            (techTracker.QueuedRewards, techTracker.RewardCountPerLevel) = GenerateRewardQueue(layout);
        return techTracker.RewardCountPerLevel;
    }
    private static (List<SaveData.TechReference> queue, List<int> levelRewardCount) GenerateRewardQueue(ResearchProgression layout)
    {
        // TODO: Some milestones should be able to add their rewards at any point in the list (for example third space layer or pin pusher)
        var queue =  new List<SaveData.TechReference>();
        List<int> levelRewardCount = [0];
        // Randomly add rewards from all milestones (except initial milestone).
        for(int i = 1;  i < layout.Levels.Count; i++)
        {
            var level = layout.Levels[i];
            var indices = Enumerable.Range(0, level.Rewards.Count).ToArray();
            indices.Shuffle();
            queue.AddRange(indices.Select(index => new SaveData.TechReference(i, index)));
            levelRewardCount.Add(level.Rewards.Count);
        }
        return (queue, levelRewardCount);
    }

    public static void MarkTechUnlocked(SaveData.TechReference techReference, ResearchProgression layout)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        techTracker.UnlockedRewards.Add(techReference);
        MarkMilestoneRewardUnlocked(techReference, layout);
    }

    private static void MarkMilestoneRewardUnlocked(SaveData.TechReference techReference, ResearchProgression layout)
    {
        // Calculate the index at which HudResearchRewardsDisplay.Level placed the reward
        var rewards = layout.Levels[techReference.Level].Rewards; 
        var reward = rewards[techReference.Index];
        int labelIndex;
        switch (reward)
        {
            case ResearchRewardBuildingGroup or ResearchRewardIslandGroup or ResearchRewardMechanic
                or ResearchRewardSideUpgrade:
                labelIndex = rewards.GetRange(0, techReference.Index)
                                 .Count(entry => entry is ResearchRewardBuildingGroup or ResearchRewardIslandGroup
                                     or ResearchRewardMechanic or ResearchRewardSideUpgrade);
                break;
            case ResearchRewardResearchPoints or ResearchRewardBlueprintCurrency
                or ResearchRewardChunkLimit:
                labelIndex = rewards.Count(entry => entry is ResearchRewardBuildingGroup or ResearchRewardIslandGroup
                                 or ResearchRewardMechanic or ResearchRewardSideUpgrade)
                             + rewards.GetRange(0, techReference.Index)
                                 .Count(entry => entry is ResearchRewardResearchPoints or ResearchRewardBlueprintCurrency
                                     or ResearchRewardChunkLimit);
                break;
            default:
                return;
        }

        _tabLevels.Instances[techReference.Level].UIRewardsDisplay.Instances[labelIndex].UITitle.Color = Color.green;
    }

    public class CustomProgressionDiscoverer : IGameScenarioRewirer
    {
        public bool Equals(IRewirer other) => other is CustomProgressionDiscoverer;

        public GameScenario ModifyGameScenario(GameScenario gameScenario)
        {
            if (ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(gameScenario))
            {
                CustomProgressions.Add(gameScenario.Progression, gameScenario);
            }
            return gameScenario;
        }
    }

    private class LevelWrapper(ResearchLevel level) : IResearchUpgrade
    {
        public IReadOnlyList<ResearchUpgradeId> RequiredUpgrades => level.RequiredUpgrades;
        public IReadOnlyList<ResearchMechanicId> RequiredMechanics => level.RequiredMechanics;
        public ResearchUpgradeId Id => level.Id;
        public IText Title => level.Title;
        public IText Description => level.Description;
        public GameIconId IconId => level.IconId;
        public GameVideoId VideoId => level.VideoId;
        public GameImageId ImageId => level.ImageId;
        // Don't award currencies twice
        public List<IResearchReward> Rewards { get; set; } = level.Rewards.Where(reward => reward is ResearchRewardSideUpgrade).ToList();
        // No shape costs
        public List<IResearchCost> Costs { get; set; } = [];
    } 
}