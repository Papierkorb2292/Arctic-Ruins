using System;
using System.Collections.Generic;
using Core.Collections;
using Core.Localization;
using Game.Core.Coordinates;
using Game.Core.Research;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace DiagonalCutter.DropCutter
{
    public static class DropCutter
    {
        //TODO: Remove/fix sound
        public static void Register(ILogger logger, DiagonalCuttersMod mod)
        { 
            BuildingDefinitionGroupId groupId = new("DropCutterGroup");
            BuildingDefinitionId definitionId = new("DropCutter");

            string titleId = "building-variant.drop-cutter.title";
            string titleDescription = "building-variant.drop-cutter.description";
            
            using var assetBundleHelper =
                AssetBundleHelper.CreateForAssetBundleEmbeddedWithMod<DiagonalCuttersMod>("Resources/DiagonalCutter");

            string iconPath = mod.resources.SubPath("DiagonalCutter_Icon.png");

            IBuildingGroupBuilder dropCutterGroup = BuildingGroup.Create(groupId)
               .WithTitle(titleId.T())
               .WithDescription(titleDescription.T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(iconPath, out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
               .WithDefaultStructureOverview();

            var tileBounds = new LocalTileBounds(TileVector.Zero, TileVector.Up);
            TileDimensions dimensions = tileBounds.Dimensions;
            LocalVector tileBoundsCenter = LocalVector.Lerp((LocalVector) tileBounds.Min, (LocalVector) tileBounds.Max, 0.5f);
            
            IBuildingConnectorData connectorData = new BuildingConnectorData(
                new IBuildingIO[]
                {
                    new ShapeConnectorConfig(TileDirection.West).ToInput(TileVector.Up),
                    new ShapeConnectorConfig(TileDirection.East).ToOutput(),
                    new ShapeConnectorConfig(TileDirection.North).ToOutput()
                },
                new[] { TileVector.Zero, TileVector.Up },
                tileBounds,
                tileBoundsCenter,
                dimensions
            );

            IBuildingBuilder dropCutterBuilder = Building.Create(definitionId)
               .WithConnectorData(connectorData)
               .DynamicallyRendering<DropCutterSimulationRenderer, DropCutterSimulation, IDropCutterDrawData>(new DropCutterDrawData())
               .WithStaticDrawData(CreateDrawData(mod.resources))
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

            AtomicBuildings.Extend()
               .SpecificScenarios(DiagonalCuttersMod.ArcticRuinsScenarioSelector)
               .WithBuilding(dropCutterBuilder, dropCutterGroup)
               .UnlockedAtMilestone(new MilestoneSelector())
               .WithDefaultPlacement()
               .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(2).ChildAt(0).Replace()) // Replace normal cutter
               .WithSimulation(new DropCutterFactoryBuilder(), logger)
               .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.CutterSpeed, 2.0f)
               .WithPrediction(new DropCutterPredictionFactoryBuilder(), logger)
               .Build();
        }
        
        private static SideUpgradePresentationData CreateSideUpgradePresentationData(string titleId, string titleDescription)
        {
            return new SideUpgradePresentationData(
                new ResearchUpgradeId("Patience"),
                GameImageId.Empty,
                GameVideoId.Empty,
                titleId.T(),
                titleDescription.T(),
                false,
                "Buildings");
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
                new DropCutterDrawData(),
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