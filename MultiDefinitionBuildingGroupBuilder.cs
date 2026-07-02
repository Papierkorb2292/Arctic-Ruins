using Game.Core.Content.Buildings;
using ShapezShifter.Flow;

namespace ArcticRuins;

// Delegates to a normal building group builder, but makes sure to only register it once, so multiple definitions can be added to the same group (for mirrored buildings)
public class MultiDefinitionBuildingGroupBuilder(IBuildingGroupBuilder _delegate) : IBuildingGroupBuilder
{
    public BuildingDefinitionGroupId GroupId => _delegate.GroupId;
    public IBuildingGroupBuilder Removable() => _delegate.Removable();
    public IBuildingGroupBuilder NotRemovable() => _delegate.NotRemovable();
    public IBuildingGroupBuilder Selectable() => _delegate.Selectable();
    public IBuildingGroupBuilder NotSelectable() => _delegate.NotSelectable();
    public IBuildingGroupBuilder Buildable() => _delegate.Buildable();
    public IBuildingGroupBuilder NotBuildable() => _delegate.NotBuildable();
    public IBuildingGroupBuilder AllowedOnNonFilledTiles() => _delegate.AllowedOnNonFilledTiles();
    public IBuildingGroupBuilder NotAllowedOnNonFilledTiles() => _delegate.NotAllowedOnNonFilledTiles();
    public IBuildingGroupBuilder AllowedOnNotches() => _delegate.AllowedOnNotches();
    public IBuildingGroupBuilder NotAllowedOnNotches() => _delegate.NotAllowedOnNotches();
    public IBuildingGroupBuilder AutoConnected() => _delegate.AutoConnected();
    public IBuildingGroupBuilder NotAutoConnected() => _delegate.NotAutoConnected();
    public IBuildingGroupBuilder AutoRotated() => _delegate.AutoRotated();
    public IBuildingGroupBuilder NotAutoRotated() => _delegate.NotAutoRotated();
    public IBuildingGroupBuilder AllowedToBeReplacedWithoutForce() => _delegate.AllowedToBeReplacedWithoutForce();
    public IBuildingGroupBuilder NotAllowedToBeReplacedWithoutForce() => _delegate.NotAllowedToBeReplacedWithoutForce();
    public IBuildingGroupBuilder RenderingConflictingIndicatorMeshes() => _delegate.RenderingConflictingIndicatorMeshes();
    public IBuildingGroupBuilder NotRenderingConflictingIndicatorMeshes() => _delegate.NotRenderingConflictingIndicatorMeshes();
    public IBuildingGroupBuilder RenderingConflictingIndicatorVisualization() => _delegate.RenderingConflictingIndicatorVisualization();
    public IBuildingGroupBuilder NotRenderingConflictingIndicatorVisualization() => _delegate.NotRenderingConflictingIndicatorVisualization();
    public IBuildingGroupBuilder ProducingConflictingIndicatorsAlways() => _delegate.ProducingConflictingIndicatorsAlways();
    public IBuildingGroupBuilder NotProducingConflictingIndicatorsAlways() => _delegate.NotProducingConflictingIndicatorsAlways();
    public IBuildingGroupBuilder RenderingConnectorIndicators() => _delegate.RenderingConnectorIndicators();
    public IBuildingGroupBuilder NotRenderingConnectorIndicator() => _delegate.NotRenderingConnectorIndicator();
    public IBuildingGroupBuilder RenderingConnectorConflictIndicators() => _delegate.RenderingConnectorConflictIndicators();
    public IBuildingGroupBuilder NotRenderingConnectorConflictIndicator() => _delegate.NotRenderingConnectorConflictIndicator();
    public IBuildingGroupBuilder ShowingBeltProcessingTimeStat() => _delegate.ShowingBeltProcessingTimeStat();
    public IBuildingGroupBuilder NotShowingBeltProcessingTimeStat() => _delegate.NotShowingBeltProcessingTimeStat();
    public IBuildingGroupBuilder ShowingBuildingsPerFullBeltStat() => _delegate.ShowingBuildingsPerFullBeltStat();
    public IBuildingGroupBuilder NotShowingBuildingsPerFullBeltStat() => _delegate.NotShowingBuildingsPerFullBeltStat();
    public IBuildingGroupBuilder DisplayableAsReward() => _delegate.DisplayableAsReward();
    public IBuildingGroupBuilder NotDisplayableAsReward() => _delegate.NotDisplayableAsReward();
    public IBuildingGroupBuilder SkippingReplacementConnectorChecks() => _delegate.SkippingReplacementConnectorChecks();
    public IBuildingGroupBuilder NotSkippingReplacementConnectorChecks() => _delegate.NotSkippingReplacementConnectorChecks();
    public IBuildingGroupBuilder WithConnectionMultiplier(int autoAttractScore) => _delegate.WithConnectionMultiplier(autoAttractScore);
    public IBuildingGroupBuilder WithPipetteOverride(BuildingDefinitionGroupId overrideGroup) => _delegate.WithPipetteOverride(overrideGroup);
    public IBuildingGroupBuilder WithoutPipetteOverride() => _delegate.WithoutPipetteOverride();
    public IBuildingGroupBuilder WithPlacementIndicator<TPlacementIndicator>() where TPlacementIndicator : IBuildingPlacementIndicator => _delegate.WithPlacementIndicator<TPlacementIndicator>();
    public IBuildingGroupBuilder WithoutPlacementIndicators() => _delegate.WithoutPlacementIndicators();
    public IBuildingGroupBuilder WithPlacementRequirements() => _delegate.WithPlacementRequirements();
    public IBuildingGroupBuilder WithoutPlacementRequirements() => _delegate.WithoutPlacementRequirements();
    public IBuildingGroupBuilder WithCustomStructureOverview(MetaStructureOverview structureOverview) => _delegate.WithCustomStructureOverview(structureOverview);
    public IBuildingGroupBuilder WithDefaultStructureOverview() => _delegate.WithDefaultStructureOverview();
    public IBuildingGroupBuilder WithoutStructureOverview() => _delegate.WithoutStructureOverview();
    public BuildingDefinitionGroup BuildAndRegister(GameBuildings gameBuildings)
    {
        if (gameBuildings.TryGetDefinitionGroup(GroupId, out var definition))
            return (BuildingDefinitionGroup)definition;
        return _delegate.BuildAndRegister(gameBuildings);
    }
}