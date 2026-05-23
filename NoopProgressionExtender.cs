using ShapezShifter.Flow.Research;

namespace ArcticRuins;

public class NoopProgressionExtender : IBuildingResearchProgressionExtender
{
    public static readonly NoopProgressionExtender Instance = new();
    
    public void ExtendResearch(ScenarioId scenarioId, ResearchProgression researchProgression, BuildingDefinitionGroupId groupId)
    {
    }
}