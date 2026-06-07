using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Core.Localization;
using Game.Content.BuildingPath.Prediction;
using Game.Core.Research;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ArcticRuins;

public static class MilestoneReverser
{
    private static Hook _milestoneCanUnlockHook;
    private static Hook _milestoneTryUnlockHook;
    private static Hook _includeUnlockedChunkLimitInRecomputationHook;
    private static Hook _markCompletedMilestoneRewardsHook;

    private static HUDResearchTabLevels _tabLevels;
    
    public static void Register()
    {
        //TODO: Filter scenario
        _milestoneCanUnlockHook = new Hook(DetourHelper.GetRuntimeMethod<ResearchUnlockManager>(
            (Expression<Func<ResearchUnlockManager, IResearchUpgrade, bool>>)((unlockManager, upgrade) => unlockManager.CanUnlock(upgrade))),
                (Func<Func<ResearchUnlockManager, IResearchUpgrade, bool>, ResearchUnlockManager, IResearchUpgrade, bool>)((orig, unlockManager, upgrade) =>
                {
                    if(upgrade is not ResearchLevel level)
                        return orig(unlockManager, upgrade);
                    return orig(unlockManager, new LevelWrapper(level)) && HasUnlockedLevelRewards(level, unlockManager);
                })
            );
        _milestoneTryUnlockHook = new Hook(DetourHelper.GetRuntimeMethod<ResearchUnlockManager>(
                (Expression<Func<ResearchUnlockManager, IResearchUpgrade, bool, bool>>)((unlockManager, upgrade, force) => unlockManager.TryUnlock(upgrade, force))), 
            (Func<Func<ResearchUnlockManager, IResearchUpgrade, bool, bool>, ResearchUnlockManager, IResearchUpgrade, bool, bool>)
            ((orig, unlockManager, upgrade, force) => orig(unlockManager, upgrade is not ResearchLevel level ? upgrade : new LevelWrapper(level), force))
        );
        _includeUnlockedChunkLimitInRecomputationHook = DetourHelper.CreatePostfixHook<ResearchUnlockProgressManager>(
            manager => manager.RecomputeUnlocks(),
            manager =>
            {
                manager._CachedUnlockedRewards.UnionWith(ArcticRuinsMod.Instance.SaveData.Tech.UnlockedRewards
                    .Select(techReference => manager.Progression.Levels[techReference.Level].Rewards[techReference.Index])
                    .OfType<ResearchRewardChunkLimit>());
            });
        _markCompletedMilestoneRewardsHook = DetourHelper.CreatePostfixHook<HUDResearchTabLevels, ResearchManager, GameMode>(
            (tabLevels, research, mode) => tabLevels.Construct(research, mode),
            (tabLevels, research, _) =>
            {
                // Color all unlocked rewards and save hud instance to add new unlocked rewards later 
                _tabLevels = tabLevels;
                foreach (var tech in ArcticRuinsMod.Instance.SaveData.Tech.UnlockedRewards)
                {
                    MarkMilestoneRewardUnlocked(tech, research.Layout);
                }
            });
    }

    public static void Dispose()
    {
        _milestoneCanUnlockHook.Dispose();
        _milestoneTryUnlockHook.Dispose();
        _includeUnlockedChunkLimitInRecomputationHook.Dispose();
        _markCompletedMilestoneRewardsHook.Dispose();
    }

    private static bool HasUnlockedLevelRewards(ResearchLevel level, ResearchUnlockManager unlockManager)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        var levelIndex = unlockManager.Layout.Levels.FindIndex(level2 => level2.Id == level.Id);
        return Enumerable.Range(0, level.Rewards.Count).All(rewardIndex => techTracker.UnlockedRewards.Contains(new SaveData.TechReference(levelIndex, rewardIndex)));
    }
    
    public static SaveData.TechReference? PickNextTech(ResearchProgression layout)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        var rewards = techTracker.QueuedRewards ??= GenerateRewardQueue(layout);
        if (rewards.Count == 0)
            return null;
        var nextReward = rewards[0];
        rewards.RemoveAt(0);
        return nextReward;
    }

    private static List<SaveData.TechReference> GenerateRewardQueue(ResearchProgression layout)
    {
        // TODO: Some milestones should be able to add their rewards at any point in the list (for example third space layer or pin pusher)
        var result =  new List<SaveData.TechReference>();
        // Randomly add rewards from all milestones (except initial milestone).
        for(int i = 1;  i < layout.Levels.Count; i++)
        {
            var level = layout.Levels[i];
            var indices = Enumerable.Range(0, level.Rewards.Count).ToArray();
            indices.Shuffle();
            result.AddRange(indices.Select(index => new SaveData.TechReference(i, index)));
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