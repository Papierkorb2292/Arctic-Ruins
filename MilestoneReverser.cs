using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Core.Localization;
using DG.Tweening;
using Game.Core.Content;
using Game.Core.Coordinates;
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
    private static Hook _discoverCustomProgressionHook;

    private static HUDResearchTabLevels _tabLevels;
    private static readonly ConditionalWeakTable<HUDResearchShapeCostDisplay, object> CostDisplaysWithBeltText = new();
    private static readonly ConditionalWeakTable<ResearchProgression, GameSessionOrchestrator> CustomProgressions = new();
    
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
            ((orig, unlockManager, upgrade, force) =>
            {
                if (!IsCustomProgression(unlockManager.Layout) || upgrade is not ResearchLevel level)
                    return orig(unlockManager, upgrade, force);
                var success = orig(unlockManager, new LevelWrapper(level), force);
                if (success)
                {
                    ShowMilestoneUnlockedMessage(level);
                }
                return success;
            })
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

        _discoverCustomProgressionHook =
            DetourHelper.CreatePostfixHook<GameSessionOrchestrator, IContent, IGameData, IGameStartOptions>(
                (orchestrator, content, data, options) =>
                    orchestrator.Init_3_SavegameAndMode(content, data, options),
                (orchestrator, _, _, _) =>
                {
                    if(ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(orchestrator.Mode.Scenario))
                        CustomProgressions.Add(orchestrator.Research.Layout, orchestrator);
                });
    }

    public static void Dispose()
    {
        _milestoneCanUnlockHook.Dispose();
        _milestoneTryUnlockHook.Dispose();
        _includeUnlockedChunkLimitInRecomputationHook.Dispose();
        _markCompletedMilestoneRewardsHook.Dispose();
        _shapeCostTextHook.Dispose();
        _hideMilestoneSummaryHook.Dispose();
        _discoverCustomProgressionHook.Dispose();
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

    private static void ShowMilestoneUnlockedMessage(IResearchUpgrade milestone)
    {
        var hudIntro = ArcticRuinsMod.Instance.IntroRenderer!.HUDIntro; 
        hudIntro.gameObject.SetActiveSelfExt(true);
        DOTween.To(
            () => hudIntro.UIMainGroup.alpha,
            alpha => hudIntro.UIMainGroup.alpha = hudIntro.UITextGroup.alpha = alpha,
            1, 0.3f);
        hudIntro.UIText.Text = ("ui.arctic-ruins.shapes-unlocked." + milestone.Id).T();

        hudIntro.UIContinueBtn.OnClick.AddListener(MilestoneUnlockedMessageOnContinue);
    }

    private static void MilestoneUnlockedMessageOnContinue()
    {
        var hudIntro = ArcticRuinsMod.Instance.IntroRenderer!.HUDIntro;
        hudIntro.UIContinueBtn.OnClick.RemoveListener(MilestoneUnlockedMessageOnContinue);
        DOTween.To(
            () => hudIntro.UIMainGroup.alpha,
            alpha => hudIntro.UIMainGroup.alpha = hudIntro.UITextGroup.alpha = alpha,
            0, 0.3f)
            .OnComplete(() => hudIntro.gameObject.SetActiveSelfExt(false));
    }
    
    public static SaveData.TechReference? PickNextTech(ResearchProgression layout, GlobalChunkCoordinate coord)
    {
        var levelMap = ArcticRuinsMod.Instance.SaveData.DataFragmentChunkLevels;
        if (levelMap == null || !levelMap.TryGetValue(coord, out var level))
            return null;
        var rewards = GetRewardQueuesPerLevel(layout)[level];
        if (rewards.Count == 0)
            return null;
        var nextReward = rewards[0];
        rewards.RemoveAt(0);
        return nextReward;
    }

    public static List<List<SaveData.TechReference>> GetRewardQueuesPerLevel(ResearchProgression layout)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        if (techTracker.QueuedRewards == null)
            techTracker.QueuedRewards = GenerateRewardQueuePerLevel(layout);
        return techTracker.QueuedRewards.Select(level => level.queue).ToList();
    }

    public static List<int> GetLevelRewardCount(ResearchProgression layout)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        if (techTracker.QueuedRewards == null)
            techTracker.QueuedRewards = GenerateRewardQueuePerLevel(layout);
        return techTracker.QueuedRewards.Select(level => level.levelRewardCount).ToList();
    }
    private static List<(List<SaveData.TechReference> queue, int levelRewardCount)> GenerateRewardQueuePerLevel(ResearchProgression layout)
    {
        // Initialize with empty first milestone
        List<(List<SaveData.TechReference> queue, int levelRewardCount)> result = [([], 0)];
        // Shuffle rewards of all milestones
        for(int i = 1; i < layout.Levels.Count; i++)
        {
            var level = layout.Levels[i];
            List<int> indices;
            if (i == 1)
            {
                // For the first milestone, keep the stabilizer as the first reward since that building is necessary from the start
                indices = Enumerable.Range(1, level.Rewards.Count - 1).ToList();
                indices.Shuffle();
                indices.Insert(0, 0);
            }
            else
            {
                indices = Enumerable.Range(0, level.Rewards.Count).ToList();
                indices.Shuffle();
            }

            result.Add((indices.Select(index => new SaveData.TechReference(i, index)).ToList(), level.Rewards.Count));
        }
        return result;
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