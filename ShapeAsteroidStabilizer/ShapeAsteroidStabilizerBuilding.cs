using System.Collections.Generic;
using Core.Events;
using Core.Localization;
using Game.Core.Coordinates;
using Game.Core.Rendering.MeshGeneration;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Hijack;
using ShapezShifter.Kit;
using ShapezShifter.SharpDetour;
using ShapezShifter.Textures;
using UnityEngine;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    public static class ShapeAsteroidStabilizerBuilding
    {
        public static BuildingDefinitionGroupId GroupId = new("ShapeAsteroidStabilizerGroup");
        public static BuildingDefinitionId DefinitionId = new("ShapeAsteroidStabilizer");

        public static void Register(ArcticRuinsMod mod)
        {
            string titleId = "building-variant.asteroid-stabilizer.title";
            string titleDescription = "building-variant.asteroid-stabilizer.description";

            using var assetBundleHelper =
                AssetBundleHelper.CreateForAssetBundleEmbeddedWithMod<ArcticRuinsMod>("Resources/DiagonalCutter");

            string iconPath = mod.Resources.SubPath("DiagonalCutter_Icon.png");

            IBuildingGroupBuilder asteroidStabilizerGroup = BuildingGroup.Create(GroupId)
                .WithTitle(titleId.T())
                .WithDescription(titleDescription.T())
                .WithIcon(FileTextureLoader.LoadTextureAsSprite(iconPath, out _))
                .AsNonTransportableBuilding()
                .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
                .WithDefaultStructureOverview();
            ((BuildingGroupBuilder)asteroidStabilizerGroup).PlacementRequirements =
            [
                new StabilizerOnResourcePatchPlacementRequirement(),
                new OnlyOnGroundLayerRequirement()
            ];

            var tileBounds = new LocalTileBounds(TileVector.Zero, TileVector.East + TileVector.Up);
            TileDimensions dimensions = tileBounds.Dimensions;
            LocalVector tileBoundsCenter =
                LocalVector.Lerp((LocalVector)tileBounds.Min, (LocalVector)tileBounds.Max, 0.5f);

            IBuildingConnectorData connectorData = new BuildingConnectorData(
                [
                    new ShapeConnectorConfig(TileDirection.West).ToInput()
                ],
                [TileVector.Zero, TileVector.Up, TileVector.East, TileVector.East + TileVector.Up],
                tileBounds,
                tileBoundsCenter,
                dimensions
            );

            IBuildingBuilder asteroidStabilizerBuilder = Building.Create(DefinitionId)
                .WithConnectorData(connectorData)
                .DynamicallyRendering<ShapeAsteroidStabilizerSimulationRenderer, ShapeAsteroidStabilizerSimulation,
                    IShapeAsteroidStabilizerDrawData>(new ShapeShapeAsteroidStabilizerDrawData())
                .WithStaticDrawData(CreateDrawData(mod.Resources))
                .WithoutSound()
                .WithoutSimulationConfiguration()
                .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

            AtomicBuildings.Extend()
                .SpecificScenarios(ArcticRuinsMod.ArcticRuinsScenarioSelector)
                .WithBuilding(asteroidStabilizerBuilder, asteroidStabilizerGroup)
                .UnlockedAtMilestone(new MilestoneSelector())
                .WithDefaultPlacement()
                .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(4).Replace()) // Replace extractor
                .WithSimulation(new ShapeAsteroidStabilizerFactoryBuilder(), ArcticRuinsMod.Logger)
                // Use custom module, because ShapezShifter measures ByOutput by default, which causes errors when highlighting the building and the entire world won't render 
                .WithCustomModules(new ItemSimulationBuildingModuleDataProvider(BuiltinResearchSpeed.BeltSpeed, BuiltinResearchSpeed.BeltSpeed, 2.0f, 0, ItemSimulationEfficiencyMeasurementMode.ByInput))
                .WithPrediction(new ShapeAsteroidStabilizerPredictionFactoryBuilder(), ArcticRuinsMod.Logger)
                .Build();
        }

        private static BuildingDrawData CreateDrawData(ModFolderLocator modResourcesLocator)
        {
            string baseMeshPath = modResourcesLocator.SubPath("DiagonalCutter.fbx");
            Mesh baseMesh = FileMeshLoader.LoadSingleMeshFromFile(baseMeshPath);

            LOD6Mesh baseModLod = MeshLod.Create().AddLod0Mesh(baseMesh).BuildLod6Mesh();

            return new BuildingDrawData(
                renderVoidBelow: false,
                new ILODMesh[] { baseModLod, baseModLod, baseModLod },
                baseModLod,
                baseModLod,
                baseModLod.LODClose,
                new LODEmptyMesh(),
                BoundingBoxHelper.CreateBasicCollider(baseMesh),
                new ShapeShapeAsteroidStabilizerDrawData(),
                false,
                null,
                false);
        }
    }

    internal class MilestoneSelector : IMilestoneSelector
    {
        public ResearchLevel Select(ScenarioId scenarioId, IReadOnlyList<ResearchLevel> milestones)
        {
            return milestones[0];
        }
    }
}