using System.Collections.Generic;
using System.Linq;
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
using Unity.Mathematics;
using UnityEngine;

namespace ArcticRuins.CommunicationRelay
{
    public static class CommunicationRelayBuilding
    {
        public static BuildingDefinitionGroupId GroupId = new("CommunicationRelayGroup");
        public static BuildingDefinitionId DefinitionId = new("CommunicationRelay");

        public static void Register()
        {
            string titleId = "building-variant.communication-relay.title";
            string titleDescription = "building-variant.communication-relay.description";

            using var assetBundleHelper =
                AssetBundleHelper.CreateForAssetBundleEmbeddedWithMod<ArcticRuinsMod>("Resources/DiagonalCutter");

            string iconPath = ArcticRuinsMod.Instance.Resources.SubPath("DiagonalCutter_Icon.png");

            IBuildingGroupBuilder communicationRelayGroup = BuildingGroup.Create(GroupId)
                .WithTitle(titleId.T())
                .WithDescription(titleDescription.T())
                .WithIcon(FileTextureLoader.LoadTextureAsSprite(iconPath, out _))
                .AsNonTransportableBuilding()
                .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
                .WithDefaultStructureOverview()
                .NotBuildable(); // Note: The names in ShapezShifter are currently wrong. This is actually NotRemovable() and NotBuildable() respectively
                //.NotSelectable(); // Don't forget to change this back once ShapezShifter is fixed


            var tileBounds = new LocalTileBounds(new TileVector(-1, -1, 0), new TileVector(1, 1, 2));
            TileDimensions dimensions = tileBounds.Dimensions;
            LocalVector tileBoundsCenter =
                LocalVector.Lerp((LocalVector)tileBounds.Min, (LocalVector)tileBounds.Max, 0.5f);

            IBuildingConnectorData connectorData = new BuildingConnectorData(
                [],
                Enumerable.Range(-1, 3).SelectMany(x => Enumerable.Range(-1, 3).SelectMany(y => Enumerable.Range(0, 3).Select(z => new TileVector(x,y,(short)z)))),
                tileBounds,
                tileBoundsCenter,
                dimensions
            );

            IBuildingBuilder communicationRelayBuilder = Building.Create(DefinitionId)
                .WithConnectorData(connectorData)
                .DynamicallyRendering<CommunicationRelaySimulationRenderer, CommunicationRelaySimulation,
                    ICommunicationRelayDrawData>(new CommunicationRelayDrawData())
                .WithStaticDrawData(CreateDrawData(ArcticRuinsMod.Instance.Resources))
                .WithoutSound()
                .WithoutSimulationConfiguration()
                .WithoutEfficiencyData();

            AtomicBuildings.Extend()
                .SpecificScenarios(ArcticRuinsMod.ArcticRuinsScenarioSelector)
                .WithBuilding(communicationRelayBuilder, communicationRelayGroup)
                .UnlockedAtMilestone(new MilestoneSelector())
                .WithDefaultPlacement()
                .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(5).InsertAfter())
                .WithSimulation(new CommunicationRelayFactoryBuilder(), ArcticRuinsMod.Logger)
                .WithCustomModules(new CommunicationRelayModules())
                .WithoutPrediction()
                .Build();
        }

        private static BuildingDrawData CreateDrawData(ModFolderLocator modResourcesLocator)
        {
            string baseMeshPath = modResourcesLocator.SubPath("DiagonalCutter.fbx");
            Mesh baseMesh = FileMeshLoader.LoadSingleMeshFromFile(baseMeshPath);

            var scaledMesh = new MeshBuilder("CommunicationRelay", 0);
            scaledMesh.AddTranslateScale(new TemporaryMeshReference(baseMesh), 0, new float3(3,3,3));
            var genMesh = scaledMesh.GenerateSingleMeshMax65KVertices()._Mesh;
            LOD6Mesh baseModLod = MeshLod.Create().AddLod0Mesh(genMesh).BuildLod6Mesh();

            return new BuildingDrawData(
                renderVoidBelow: false,
                new ILODMesh[] { baseModLod, baseModLod, baseModLod },
                baseModLod,
                baseModLod,
                baseModLod.LODClose,
                new LODEmptyMesh(),
                [ new CollisionBox(new LocalVector(0.5f, 0.5f, 0.5f), new LocalDimension(3, 3, 10)) ],
                new CommunicationRelayDrawData(),
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