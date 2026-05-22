using System;
using System.Linq;
using Core.Collections;
using Core.Localization;
using ArcticRuins.ArcticCutter;
using Game.Core.GameData.GameModeDefinition;
using Game.Core.Research;
using JetBrains.Annotations;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Kit;
using ShapezShifter.SharpDetour;
using ShapezShifter.Textures;
using UnityEngine;
using ILogger = Core.Logging.ILogger;
using Renderer = DiagonalCutterSimulationRenderer;
using Simulation = DiagonalCutterSimulation;
using RendererData = IDiagonalCutterDrawData;

namespace ArcticRuins
{
    [UsedImplicitly]
    public class ArcticRuinsMod : IMod
    {
        public static readonly ScenarioSelector ArcticRuinsScenarioSelector =
            scenario => scenario.UniqueId.Id.StartsWith("arctic-ruins");

        public ModFolderLocator resources { get; }

        public readonly Hook GameModeHook;

        public ArcticRuinsMod(ILogger logger)
        {
            GameModeHook = CreateGameModeHook();

            resources = ModDirectoryLocator.CreateLocator<ArcticRuinsMod>().SubLocator("Resources");

            ArticCutter.Register(logger, this);

            BuildingDefinitionGroupId groupId = new("DiagonalCutterGroup");
            BuildingDefinitionId definitionId = new("DiagonalCutter");

            string titleId = "building-variant.cutter-diagonal.title";
            string titleDescription = "building-variant.cutter-diagonal.description";

            using var assetBundleHelper =
                AssetBundleHelper.CreateForAssetBundleEmbeddedWithMod<ArcticRuinsMod>("Resources/DiagonalCutter");

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

        //TODO: Find a better place to hook into
        private static Hook CreateGameModeHook()
        {
            return DetourHelper
                .CreatePostfixHook<MainMenuOrchestrator, GameSessionOrchestrator, GlobalsData, IGameData>(
                    (mainMenuOrchestrator, gameSessionOrchestrator, globalsData, gameData) =>
                        mainMenuOrchestrator.Step_0_1_InitDependencies(gameSessionOrchestrator, globalsData, gameData),
                    (_, _, _, gameData) => AddGameMode((GameData)gameData));
        }

        private static void AddGameMode(GameData gameData)
        {
            var baseMode = gameData._GameModes.Values.First();
            gameData._GameModes[new GameModeId("ArcticRuins")] = new GameModeDefinition(
                (MetaGameModeDefinition)ScriptableObject.CreateInstance(typeof(MetaGameModeDefinition), scriptable =>
                {
                    //TODO: Add proper assets
                    var mode = (MetaGameModeDefinition)scriptable;
                    mode.Icon = baseMode.Icon;
                    mode.VideoPreview = baseMode.VideoPreview;
                    mode.ImagePreview = baseMode.ImagePreview;
                    mode.GameModeBuildings = baseMode.Buildings;
                    mode.GameModeIslands = baseMode.Islands;
                    mode.TrainSimulationConfiguration = baseMode.TrainSimulationConfiguration;
                    mode.name = "ArcticRuins";
                }));
        }

        public void Dispose()
        {
            GameModeHook.Dispose();
        }

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
}