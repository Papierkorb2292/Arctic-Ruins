using System.Collections.Generic;
using ShapezShifter.Flow;

namespace ArcticRuins;

public class ArcticRuinsFeatures
{
    public const string ArcticCutterKey = "ArcticCutter"; // Not done
    public const string TheOtherSideMapGeneratorKey = "TheOtherSideMapGenerator";
    public const string TheOtherSideAsteroidProgressKey = "TheOtherSideAsteroidProgress";
    public const string CommunicationRelayKey = "CommunicationRelay";
    public const string DataFragmentKey = "DataFragment";
    public const string TheOtherSideIntroKey = "TheOtherSideIntro";
    public const string LayerDetacherKey = "LayerDetacher";
    public const string MeshRecolorKey = "MeshRecolor";
    public const string TheOtherSideMilestoneReverserKey = "TheOtherSideMilestoneReverser";
    public const string RuinBuildingsKey = "RuinBuildings";
    public const string StabilizerKey = "Stabilizer";
    public const string StormKey = "Storm";
    public const string TheOtherSideVortexReverserKey = "TheOtherSideVortexReverser";
    public const string DisableDifficultyEditAfterCreation = "DisableDifficultyEditAfterCreation";

    public static Dictionary<string, HashSet<string>> ScenarioFeatures = new()
    {
        { "arctic-ruins-default", [
            TheOtherSideMapGeneratorKey,
            TheOtherSideAsteroidProgressKey,
            CommunicationRelayKey,
            DataFragmentKey,
            TheOtherSideIntroKey,
            LayerDetacherKey,
            MeshRecolorKey,
            TheOtherSideMilestoneReverserKey,
            RuinBuildingsKey,
            StabilizerKey,
            StormKey,
            TheOtherSideVortexReverserKey,
            DisableDifficultyEditAfterCreation,
        ] }
    };

    public static ScenarioSelector GetSelectorForFeature(string featureKey) =>
        scenario => ScenarioHasFeature(scenario.UniqueId, featureKey);

    public static bool ScenarioHasFeature(ScenarioId scenario, string featureKey) =>
        ScenarioFeatures.GetValueOrDefault(scenario.Id)?.Contains(featureKey) ?? false;
}