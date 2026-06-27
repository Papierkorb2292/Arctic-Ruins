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

namespace ArcticRuins.DataFragment
{
    public static class DataFragmentBuilding
    {
        public static BuildingDefinitionGroupId GroupId = new("DataFragmentGroup");
        public static BuildingDefinitionId DefinitionId = new("DataFragment");
        
        public static PrefabViewReference<HUDGenericIconWithLabel> UIRewardPrefab { get; private set; }

        private static Hook UIRewardPrefabFetchHook;

        public static void Register()
        {
            string titleId = "building-variant.data-fragment.title";
            string titleDescription = "building-variant.data-fragment.description";

            string iconPath = ArcticRuinsMod.Instance.Resources.SubPath("DataFragment_Icon.png");

            ArcticRuinsMod.Instance.CustomBuildings.Add((DefinitionId, GroupId));
            
            IBuildingGroupBuilder communicationRelayGroup = BuildingGroup.Create(GroupId)
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

            IBuildingBuilder communicationRelayBuilder = Building.Create(DefinitionId)
                .WithConnectorData(connectorData)
                .DynamicallyRendering<DataFragmentSimulationRenderer, DataFragmentSimulation,
                    IDataFragmentDrawData>(new DataFragmentDrawData(CreateCubeMesh(ArcticRuinsMod.Instance.Resources)))
                .WithStaticDrawData(CreateDrawData(ArcticRuinsMod.Instance.Resources))
                .WithoutSound()
                .WithoutSimulationConfiguration()
                .WithoutEfficiencyData();

            AtomicBuildings.Extend()
                .SpecificScenarios(ArcticRuinsMod.ArcticRuinsScenarioSelector)
                .WithBuilding(communicationRelayBuilder, communicationRelayGroup)
                .UnlockedInDevMode()
                .WithDefaultPlacement()
                .InToolbar(ArcticRuinsMod.IsModDevelopmentMode ? ToolbarElementLocator.Root().ChildAt(0).ChildAt(5).InsertAfter() : Helper.NoToolbarEntryLocation)
                .WithSimulation(new DataFragmentFactoryBuilder(), ArcticRuinsMod.Logger)
                .WithCustomModules(new DataFragmentModules())
                .WithoutPrediction()
                .Build();
            
            UIRewardPrefabFetchHook = DetourHelper.CreatePostfixHook<HUDResearchRewardsDisplay, IGameData, GameMode, ResearchManager>(
                (display, gameData, mode, research) => display.Construct(gameData, mode, research),
                (display, _, _, _) =>
                {
                    UIRewardPrefab = display.UIRewardPrefab;
                });
        }

        public static void Dispose()
        {
            UIRewardPrefabFetchHook.Dispose();
        }

        private static BuildingDrawData CreateDrawData(ModFolderLocator modResourcesLocator)
        {
            string baseMeshPath = modResourcesLocator.SubPath("DataFragment.fbx");
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
                new DataFragmentDrawData(CreateCubeMesh(modResourcesLocator)),
                false,
                null,
                false);
        }

        private static ILODMesh CreateCubeMesh(ModFolderLocator modResourcesLocator)
        {
            var cubeMeshPath = modResourcesLocator.SubPath("DataFragmentCube.fbx");
            var cubeMesh = FileMeshLoader.LoadSingleMeshFromFile(cubeMeshPath);
            return MeshLod.Create().AddLod0Mesh(cubeMesh).BuildLod6Mesh();
        }
    }
}