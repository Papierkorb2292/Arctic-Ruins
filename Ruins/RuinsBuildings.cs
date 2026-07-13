using System.Collections.Generic;
using System.Linq;
using Core.Events;
using Core.Localization;
using Game.Core.Content.Buildings;
using Game.Core.Coordinates;
using Game.Core.Rendering.MeshGeneration;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Hijack;
using ShapezShifter.Kit;
using ShapezShifter.SharpDetour;
using ShapezShifter.Textures;
using Unity.Core.View;
using Unity.Mathematics;
using UnityEngine;

namespace ArcticRuins.Ruins
{
    public static class RuinsBuildings
    {
        public static BuildingDefinitionId WallBuildingId { get; private set; }
        public static BuildingDefinitionId RubbleBuildingId { get; private set; }
        
        public static void Register()
        {
            WallBuildingId = RegisterRuin("wall", "RuinWall.fbx", "Wall_Icon.png");
            RubbleBuildingId = RegisterRuin("rubble", "Rubble.fbx", "Rubble_Icon.png");
        }

        public static BuildingDefinitionId RegisterRuin(string name, string model, string icon)
        {
            BuildingDefinitionGroupId groupId = new($"Ruin{name}Group");
            BuildingDefinitionId definitionId = new($"Ruin{name}");
            
            string titleId = $"building-variant.ruin-{name}.title";
            string titleDescription = $"building-variant.ruin-{name}.description";

            string iconPath = ArcticRuinsMod.Instance.Resources.SubPath(icon);

            IBuildingGroupBuilder ruinsGroup = BuildingGroup.Create(groupId)
                .WithTitle(titleId.T())
                .WithDescription(titleDescription.T())
                .WithIcon(FileTextureLoader.LoadTextureAsSprite(iconPath, out _))
                .AsNonTransportableBuilding()
                .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
                .WithDefaultStructureOverview()
                .NotRemovable();


            var tileBounds = new LocalTileBounds(TileVector.Zero, TileVector.Zero);
            TileDimensions dimensions = tileBounds.Dimensions;
            LocalVector tileBoundsCenter =
                LocalVector.Lerp((LocalVector)tileBounds.Min, (LocalVector)tileBounds.Max, 0.5f);

            IBuildingConnectorData connectorData = new BuildingConnectorData(
                [],
                [TileVector.Zero],
                tileBounds,
                tileBoundsCenter,
                dimensions
            );

            IBuildingBuilder ruinsBuilder = ((IIdentifiableConnectableDynamicallyRenderableBuildingBuilder)Building.Create(definitionId)
                .WithConnectorData(connectorData))
                .WithStaticDrawData(CreateDrawData(ArcticRuinsMod.Instance.Resources, model))
                .WithoutSound()
                .WithoutSimulationConfiguration()
                .WithoutEfficiencyData();

            AtomicBuildings.Extend()
                .SpecificScenarios(ArcticRuinsFeatures.GetSelectorForFeature(ArcticRuinsFeatures.RuinBuildingsKey))
                .WithBuilding(ruinsBuilder, ruinsGroup)
                .UnlockedInDevMode()
                .WithDefaultPlacement()
                .InToolbar(ArcticRuinsMod.IsModDevelopmentMode ? ToolbarElementLocator.Root().ChildAt(0).ChildAt(5).InsertAfter() : Helper.NoToolbarEntryLocation)
                .WithSimulation(new DataFragmentFactoryBuilder(), ArcticRuinsMod.Logger)
                .WithCustomModules(new RuinsModules())
                .WithoutPrediction()
                .Build();

            return definitionId;
        }

        private static BuildingDrawData CreateDrawData(ModFolderLocator modResourcesLocator, string model)
        {
            string baseMeshPath = modResourcesLocator.SubPath(model);
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
                null,
                false,
                null,
                false);
        }
    }
}