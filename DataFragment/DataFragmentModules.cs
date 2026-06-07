using System.Collections.Generic;
using Core.Localization;
using Game.Core.Map.Simulation;
using UnityEngine;

namespace ArcticRuins.DataFragment;

public class DataFragmentModules : SimulationBasedBuildingModuleDataProvider<DataFragmentSimulation>
{
    public override IEnumerable<IHUDSidePanelModuleData> GetSimulationModules(BuildingModel building, ILocalizedSimulation localizedSimulation,
        DataFragmentSimulation actualSimulation)
    {
        if (!actualSimulation.State.GeneratedReward)
        {
            actualSimulation.State.Reward = MilestoneReverser.PickNextTech(actualSimulation.Progression);
            actualSimulation.State.GeneratedReward = true;
        }
        var techReference = actualSimulation.State.Reward;

        var reward = techReference != null
            ? actualSimulation.Progression.Levels[techReference.Value.Level].Rewards[techReference.Value.Index]
            : new ResearchRewardResearchPoints(new ResearchPointCurrency(4));
        if (actualSimulation.State.UnlockedReward)
        {
            yield return new HUDSidePanelModuleDataFragmentInfo.Data(reward, true);
            yield break;
        }
        yield return new HUDSidePanelModuleDataFragmentInfo.Data(reward, false);
        yield return new HUDSidePanelModuleGenericButton.Data("ui.arctic-ruins.data-fragment.unlock".T(), () =>
        {
            if (actualSimulation.State.UnlockedReward)
                return;
            var unlockManager = StaticGameCoreAccessor.G.Research.UnlockManager;
            if (techReference != null)
                MilestoneReverser.MarkTechUnlocked(techReference.Value, actualSimulation.Progression);
            ArcticRuinsMod.Logger.Info!.LogFormat("Unlocking data fragment: {0} at {1}", reward, techReference);
            switch (reward)
            {
                case ResearchRewardBlueprintCurrency or ResearchRewardResearchPoints:
                    StaticGameCoreAccessor.G.Research.RewardManager.GrantReward(reward);
                    break;
                case ResearchRewardSideUpgrade upgradeReference:
                    var upgrade = actualSimulation.Progression.GetUpgrade(upgradeReference.SideUpgradeId);
                    ArcticRuinsMod.Logger.Info!.LogFormat("Called TryUnlock: {0}", unlockManager.TryUnlock(upgrade));
                    break;
                case ResearchRewardChunkLimit:
                    // Recompute so the reward that is now marked as unlocked will be added
                    unlockManager.UnlockProgressManager.RecomputeUnlocks();
                    break;
            }

            unlockManager.UnlockProgressManager._OnChanged.Invoke();
            actualSimulation.State.UnlockedReward = true;
            actualSimulation.RewardUnlockSimulationTime = StaticGameCoreAccessor.G.SimulationSpeed.SimulationTime_G;
        });
    }
}