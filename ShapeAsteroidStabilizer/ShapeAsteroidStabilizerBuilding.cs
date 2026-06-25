using System.Collections.Generic;
using System.Linq;
using Core.Events;
using Core.Localization;
using Game.Core.Content.Buildings;
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

        public static void Register()
        {
            string titleId = "building-variant.asteroid-stabilizer.title";
            string titleDescription = "building-variant.asteroid-stabilizer.description";

            string iconPath = ArcticRuinsMod.Instance.Resources.SubPath("Stabilizer_Icon.png");

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
                    new ShapeConnectorConfig(TileDirection.West, BuildingItemIOType.Regular).ToInput()
                ],
                [TileVector.Zero, TileVector.Up, TileVector.East, TileVector.East + TileVector.Up],
                tileBounds,
                tileBoundsCenter,
                dimensions
            );

            var drawData = CreateDrawData(ArcticRuinsMod.Instance.Resources, out var customDrawData);
            IBuildingBuilder asteroidStabilizerBuilder = Building.Create(DefinitionId)
                .WithConnectorData(connectorData)
                .DynamicallyRendering<ShapeAsteroidStabilizerSimulationRenderer, ShapeAsteroidStabilizerSimulation,
                    IShapeAsteroidStabilizerDrawData>(customDrawData)
                .WithStaticDrawData(drawData)
                .WithoutSound()
                .WithoutSimulationConfiguration()
                .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

            AtomicBuildings.Extend()
                .SpecificScenarios(ArcticRuinsMod.ArcticRuinsScenarioSelector)
                .WithBuilding(asteroidStabilizerBuilder, asteroidStabilizerGroup)
                .UnlockedAtMilestone(Helper.FirstMilestoneSelector)
                .WithDefaultPlacement()
                .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(4).Replace()) // Replace extractor
                .WithCustomSimulationSystem<IShapeAsteroidStabilizerConfiguration>((systems, dependencies, building, out config) =>
                {
                    config = new ShapeShapeAsteroidStabilizerConfiguration(
                        BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
                        BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
                        new ResearchSpeedId("BeltSpeed"));
                    systems.Remove(systems.First(system => system is ShapeMiningSystem));
                    var asteroidProgressSystem = (AsteroidProgressSystem)systems.First(system => system is AsteroidProgressSystem);
                    return new ShapeStabilizingSystem(config, building, dependencies.ResourcesMap, dependencies.ShapeRegistry, asteroidProgressSystem,
                        dependencies.Mode, ArcticRuinsMod.Logger);
                })
                // Use custom module, because ShapezShifter measures ByOutput by default, which causes errors when highlighting the building and the entire world won't render 
                .WithCustomModules(new ItemSimulationBuildingModuleDataProvider(BuiltinResearchSpeed.BeltSpeed, BuiltinResearchSpeed.BeltSpeed, 2.0f, 0, ItemSimulationEfficiencyMeasurementMode.ByInput))
                .WithPrediction(new ShapeAsteroidStabilizerPredictionFactoryBuilder(), ArcticRuinsMod.Logger)
                .Build();
        }

        private static BuildingDrawData CreateDrawData(ModFolderLocator modResourcesLocator, out ShapeShapeAsteroidStabilizerDrawData customDrawData)
        {
            string baseMeshPath = modResourcesLocator.SubPath("Stabilizer.fbx");
            Mesh baseMesh = FileMeshLoader.LoadSingleMeshFromFile(baseMeshPath);
            Mesh hammerMesh = FileMeshLoader.LoadSingleMeshFromFile(modResourcesLocator.SubPath("StabilizerHammer.fbx"));

            LOD6Mesh baseModLod = MeshLod.Create().AddLod0Mesh(baseMesh).BuildLod6Mesh();
            LOD6Mesh hammerModLod = MeshLod.Create().AddLod0Mesh(hammerMesh).BuildLod6Mesh();

            customDrawData = new ShapeShapeAsteroidStabilizerDrawData(hammerModLod);
            
            return new BuildingDrawData(
                renderVoidBelow: false,
                new ILODMesh[] { baseModLod, baseModLod, baseModLod },
                baseModLod,
                baseModLod,
                baseModLod.LODClose,
                new LODEmptyMesh(),
                BoundingBoxHelper.CreateBasicCollider(baseMesh),
                customDrawData,
                false,
                null,
                false);
        }
    }
}