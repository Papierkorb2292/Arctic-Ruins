using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Core.Localization;
using Game.Content.BuildingPath.Prediction;
using Game.Core.Research;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using Random = UnityEngine.Random;

namespace ArcticRuins;

public static class MilestoneReverser
{
    private static Hook _milestoneCanUnlockHook;
    private static Hook _milestoneTryUnlockHook;
    private static Hook _includeUnlockedChunkLimitInRecomputationHook;
    
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
    }

    public static void Dispose()
    {
        _milestoneCanUnlockHook.Dispose();
        _milestoneTryUnlockHook.Dispose();
        _includeUnlockedChunkLimitInRecomputationHook.Dispose();
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

    public static void MarkTechUnlocked(SaveData.TechReference techReference)
    {
        var techTracker = ArcticRuinsMod.Instance.SaveData.Tech;
        techTracker.UnlockedRewards.Add(techReference);
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