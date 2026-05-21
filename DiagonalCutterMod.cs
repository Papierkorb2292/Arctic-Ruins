using System;
using Core.Collections;
using Core.Localization;
using DiagonalCutter;
using DiagonalCutter.DropCutter;
using Game.Core.Research;
using JetBrains.Annotations;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using UnityEngine;
using ILogger = Core.Logging.ILogger;
using Renderer = DiagonalCutterSimulationRenderer;
using Simulation = DiagonalCutterSimulation;
using RendererData = IDiagonalCutterDrawData;

[UsedImplicitly]
public class DiagonalCuttersMod : IMod
{
    public ModFolderLocator resources { get; }
    
    public DiagonalCuttersMod(ILogger logger)
    {
        resources = ModDirectoryLocator.CreateLocator<DiagonalCuttersMod>().SubLocator("Resources");
        
        DropCutter.Register(logger, this);
        
        BuildingDefinitionGroupId groupId = new("DiagonalCutterGroup");
        BuildingDefinitionId definitionId = new("DiagonalCutter");

        string titleId = "building-variant.cutter-diagonal.title";
        string titleDescription = "building-variant.cutter-diagonal.description";

        using var assetBundleHelper =
            AssetBundleHelper.CreateForAssetBundleEmbeddedWithMod<DiagonalCuttersMod>("Resources/DiagonalCutter");

        string iconPath = resources.SubPath("DiagonalCutter_Icon.png");

        IBuildingGroupBuilder diagonalCutterGroup = BuildingGroup.Create(groupId)
           .WithTitle(titleId.T())
           .WithDescription(titleDescription.T())
           .WithIcon(FileTextureLoader.LoadTextureAsSprite(iconPath, out _))
           .AsNonTransportableBuilding()
           .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
           .WithDefaultStructureOverview();

        IBuildingConnectorData connectorData = BuildingConnectors.SingleTile()
           .AddShapeInput(ShapeConnectorConfig.DefaultInput())
           .AddShapeOutput(ShapeConnectorConfig.DefaultOutput())
           .Build();

        IBuildingBuilder diagonalCutterBuilder = Building.Create(definitionId)
           .WithConnectorData(connectorData)
           .DynamicallyRendering<Renderer, Simulation, RendererData>(new DiagonalCutterDrawData())
           .WithStaticDrawData(CreateDrawData(resources))
           .WithoutSound()
           .WithoutSimulationConfiguration()
           .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

        IPresentableUnlockableSideUpgradeBuilder sideUpgradeBuilder = SideUpgrade.New()
           .WithPresentationData(CreateSideUpgradePresentationData(titleId, titleDescription))
           .WithCost(new ResearchCostPoints(new ResearchPointCurrency(50)).AsEnumerable())
           .WithCustomRequirements(Array.Empty<ResearchMechanicId>(), Array.Empty<ResearchUpgradeId>());
        AtomicBuildings.Extend()
           .AllScenarios()
           .WithBuilding(diagonalCutterBuilder, diagonalCutterGroup)
           .UnlockedWithNewSideUpgrade(sideUpgradeBuilder)
           .WithDefaultPlacement()
           .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(2).ChildAt(^1).InsertAfter())
           .WithSimulation(new DiagonalCutterFactoryBuilder(), logger)
           .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.CutterSpeed, 2.0f)
           .WithPrediction(new DiagonalCutterPredictionFactoryBuilder(), logger)
           .Build();
    }

    public void Dispose() { }

    private SideUpgradePresentationData CreateSideUpgradePresentationData(string titleId, string titleDescription)
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
            new DiagonalCutterDrawData(),
            false,
            null,
            false);
    }
}
