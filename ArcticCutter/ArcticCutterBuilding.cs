using System;
using System.Collections.Generic;
using Core.Collections;
using Core.Localization;
using Game.Core.Content.Buildings;
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

namespace ArcticRuins.ArcticCutter
{
    public static class ArcticCutterBuilding
    {
        public static BuildingDefinitionGroupId GroupId = new("ArcticCutterGroup");
        public static BuildingDefinitionId DefinitionId = new("ArcticCutter");
        //TODO: Remove/fix sound
        public static void Register()
        { 
            string titleId = "building-variant.arctic-cutter.title";
            string titleDescription = "building-variant.arctic-cutter.description";
            
            ArcticRuinsMod.Instance.CustomBuildings.Add((DefinitionId, GroupId));
            
            string iconPath = ArcticRuinsMod.Instance.Resources.SubPath("DiagonalCutter_Icon.png");

            IBuildingGroupBuilder arcticCutterGroup = BuildingGroup.Create(GroupId)
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
                [
                    new ShapeConnectorConfig(TileDirection.West).ToInput(TileVector.Up),
                    new ShapeConnectorConfig(TileDirection.East).ToOutput(),
                    new ShapeConnectorConfig(TileDirection.North).ToOutput()
                ],
                [TileVector.Zero, TileVector.Up],
                tileBounds,
                tileBoundsCenter,
                dimensions
            );

            IBuildingBuilder arcticCutterBuilder = Building.Create(DefinitionId)
               .WithConnectorData(connectorData)
               .DynamicallyRendering<ArcticCutterSimulationRenderer, ArcticCutterSimulation, IArcticCutterDrawData>(new ArcticCutterDrawData())
               .WithStaticDrawData(CreateDrawData(ArcticRuinsMod.Instance.Resources))
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

            AtomicBuildings.Extend()
               .SpecificScenarios(ArcticRuinsFeatures.GetSelectorForFeature(ArcticRuinsFeatures.ArcticCutterKey))
               .WithBuilding(arcticCutterBuilder, arcticCutterGroup)
               .UnlockedAtMilestone(Helper.FirstMilestoneSelector)
               .WithDefaultPlacement()
               .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(2).Replace("CutterDefaultVariant")) // Replace normal cutter
               .WithSimulation(new ArcticCutterFactoryBuilder(), ArcticRuinsMod.Logger)
               .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.CutterSpeed, 2.0f)
               .WithPrediction(new ArcticCutterPredictionFactoryBuilder(), ArcticRuinsMod.Logger)
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
                new ArcticCutterDrawData(),
                false,
                null,
                false);
        }
    }
}