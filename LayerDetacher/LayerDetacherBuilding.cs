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

namespace ArcticRuins.LayerDetacher
{
    public static class LayerDetacherBuilding
    {
        public static BuildingDefinitionGroupId GroupId = new("LayerDetacherGroup");
        public static BuildingDefinitionId DefinitionId = new("LayerDetacher");
        
        public static void Register()
        { 
            string titleId = "building-variant.layer-detacher.title";
            string titleDescription = "building-variant.layer-detacher.description";
            
            using var assetBundleHelper =
                AssetBundleHelper.CreateForAssetBundleEmbeddedWithMod<ArcticRuinsMod>("Resources/DiagonalCutter");

            string iconPath = ArcticRuinsMod.Instance.Resources.SubPath("DiagonalCutter_Icon.png");

            IBuildingGroupBuilder arcticCutterGroup = BuildingGroup.Create(GroupId)
               .WithTitle(titleId.T())
               .WithDescription(titleDescription.T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(iconPath, out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
               .WithDefaultStructureOverview();

            var tileBounds = new LocalTileBounds(TileVector.North, TileVector.Zero);
            TileDimensions dimensions = tileBounds.Dimensions;
            LocalVector tileBoundsCenter = LocalVector.Lerp((LocalVector) tileBounds.Min, (LocalVector) tileBounds.Max, 0.5f);
            
            IBuildingConnectorData connectorData = new BuildingConnectorData(
                [
                    new ShapeConnectorConfig(TileDirection.West).ToInput(),
                    new ShapeConnectorConfig(TileDirection.East).ToOutput(TileVector.North),
                    new ShapeConnectorConfig(TileDirection.East).ToOutput(),
                ],
                [TileVector.Zero, TileVector.North],
                tileBounds,
                tileBoundsCenter,
                dimensions
            );
            IBuildingBuilder arcticCutterBuilder = Building.Create(DefinitionId)
               .WithConnectorData(connectorData)
               .DynamicallyRendering<LayerDetacherSimulationRenderer, LayerDetacherSimulation, ILayerDetacherDrawData>(new LayerDetacherDrawData())
               .WithStaticDrawData(CreateDrawData(ArcticRuinsMod.Instance.Resources))
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

            AtomicBuildings.Extend()
               .SpecificScenarios(ArcticRuinsMod.ArcticRuinsScenarioSelector)
               .WithBuilding(arcticCutterBuilder, arcticCutterGroup)
               .UnlockedAtMilestone(new MilestoneSelector())
               .WithDefaultPlacement()
               .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(3).ChildAt(^1).InsertAfter()) // At the end of the stacker section
               .WithSimulation(new LayerDetacherFactoryBuilder(), ArcticRuinsMod.Logger)
               .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.CutterSpeed, 2.0f)
               .WithPrediction(new LayerDetacherPredictionFactoryBuilder(), ArcticRuinsMod.Logger)
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
                new LayerDetacherDrawData(),
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