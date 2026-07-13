using System;
using System.Collections.Generic;
using Core.Collections;
using Core.Localization;
using Game.Core.BuildingLogic.Data;
using Game.Core.Content.Buildings;
using Game.Core.Coordinates;
using Game.Core.Rendering.MeshGeneration;
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
        public static BuildingDefinitionId MirroredDefinitionId = new("LayerDetacherMirrored");
        public static Sprite Icon;
        
        public static void Register()
        { 
            string titleId = "building-variant.layer-detacher.title";
            string titleDescription = "building-variant.layer-detacher.description";
            
            string iconPath = ArcticRuinsMod.Instance.Resources.SubPath("LayerDetacher_Icon.png");
            Icon = FileTextureLoader.LoadTextureAsSprite(iconPath, out _);
            
            ArcticRuinsMod.Instance.CustomBuildings.Add((DefinitionId, GroupId));
            ArcticRuinsMod.Instance.CustomBuildings.Add((MirroredDefinitionId, GroupId));

            IBuildingGroupBuilder layerDetacherGroup = (BuildingGroupBuilder)BuildingGroup.Create(GroupId)
                .WithTitle(titleId.T())
                .WithDescription(titleDescription.T())
                .WithIcon(Icon)
                .AsNonTransportableBuilding()
                .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
                .WithDefaultStructureOverview();
            ((BuildingGroupBuilder)layerDetacherGroup).ShowStatBeltProcessingTime = true;
            ((BuildingGroupBuilder)layerDetacherGroup).ShowStatBuildingsPerFullBelt = true;
            layerDetacherGroup = new MultiDefinitionBuildingGroupBuilder(layerDetacherGroup); // Needs to be able to have multiple buildings registered

            RegisterBuilding(layerDetacherGroup, false, out var normalBuilding);
            RegisterBuilding(layerDetacherGroup, true, out var mirroredBuilding);
            
            normalBuilding.CustomData.Attach(new BuildingDefinitionFactory.BuildingMirroringDefinition(mirroredBuilding, false));
            mirroredBuilding.CustomData.Attach(new BuildingDefinitionFactory.BuildingMirroringDefinition(normalBuilding, true));
        }

        private static void RegisterBuilding(IBuildingGroupBuilder group, bool isMirrored, out BuildingDefinition buildingDefinition)
        {
            var tileBounds = new LocalTileBounds(isMirrored ? TileVector.Zero : TileVector.North, isMirrored ? TileVector.South : TileVector.Zero);
            TileDimensions dimensions = tileBounds.Dimensions;
            LocalVector tileBoundsCenter = LocalVector.Lerp((LocalVector) tileBounds.Min, (LocalVector) tileBounds.Max, 0.5f);
            
            IBuildingConnectorData connectorData = new BuildingConnectorData(
                [
                    new ShapeConnectorConfig(TileDirection.West, separators: true).ToInput(),
                    new ShapeConnectorConfig(TileDirection.East, separators: true).ToOutput(isMirrored ? TileVector.South : TileVector.North),
                    new ShapeConnectorConfig(TileDirection.East, separators: true).ToOutput(),
                ],
                [TileVector.Zero, isMirrored ? TileVector.South : TileVector.North],
                tileBounds,
                tileBoundsCenter,
                dimensions
            );
            
            var drawData = CreateDrawData(ArcticRuinsMod.Instance.Resources, isMirrored, out var customDrawData);
            IBuildingBuilder layerDetacherBuilder = Building.Create(isMirrored ? MirroredDefinitionId : DefinitionId)
               .WithConnectorData(connectorData)
               .DynamicallyRendering<LayerDetacherSimulationRenderer, LayerDetacherSimulation, ILayerDetacherDrawData>(customDrawData)
               .WithStaticDrawData(drawData)
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

            buildingDefinition = ((BuildingBuilder)layerDetacherBuilder).BuildingDefinition;

            AtomicBuildings.Extend()
                .SpecificScenarios(ArcticRuinsFeatures.GetSelectorForFeature(ArcticRuinsFeatures.LayerDetacherKey))
               .WithBuilding(layerDetacherBuilder, group)
               .NotUnlocked() // Unlocked through scenario file
               .WithDefaultPlacement()
               .InToolbar(isMirrored ? Helper.NoToolbarEntryLocation : ToolbarElementLocator.Root().ChildAt(0).ChildAt(3).ChildAt(^1).InsertAfter()) // At the end of the stacker section
               .WithSimulation(new LayerDetacherFactoryBuilder(), ArcticRuinsMod.Logger)
               .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.CutterSpeed, 3.0f)
               .WithPrediction(new LayerDetacherPredictionFactoryBuilder(), ArcticRuinsMod.Logger)
               .Build();
        }

        private static BuildingDrawData CreateDrawData(ModFolderLocator modResourcesLocator, bool isMirrored, out ILayerDetacherDrawData customDrawData)
        {
            string baseMeshPath = modResourcesLocator.SubPath("LayerDetacher.fbx");
            Mesh baseMesh = FileMeshLoader.LoadSingleMeshFromFile(baseMeshPath);
            Mesh launcherMesh = FileMeshLoader.LoadSingleMeshFromFile(modResourcesLocator.SubPath("LayerDetacherLauncher.fbx"));

            if (isMirrored)
            {
                baseMesh = MirroredMeshUtils.MirrorMeshOnZAxis(baseMesh);
                launcherMesh = MirroredMeshUtils.MirrorMeshOnZAxis(launcherMesh);
            }

            LOD6Mesh baseModLod = MeshLod.Create().AddLod0Mesh(baseMesh).BuildLod6Mesh();
            LOD6Mesh launcherModLod = MeshLod.Create().AddLod0Mesh(launcherMesh).BuildLod6Mesh();

            customDrawData = new LayerDetacherDrawData(launcherModLod, isMirrored);

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