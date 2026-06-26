using Game.Core.Content.Buildings;
using Game.Core.Content.Islands;
using ShapezShifter.Flow.Research;

namespace ArcticRuins;

public class NoopProgressionExtender : IBuildingResearchProgressionExtender, IIslandResearchProgressionExtender
{
    public static readonly NoopProgressionExtender Instance = new();
    
    public void ExtendResearch(ScenarioId scenarioId, ResearchProgression researchProgression, BuildingDefinitionGroupId groupId)
    {
    }

    public void ExtendResearch(ScenarioId scenarioId, ResearchProgression researchProgression, IslandDefinitionGroupId groupId)
    {
    }
}