using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Core.Collections;
using Core.Localization;
using ArcticRuins.ArcticCutter;
using ArcticRuins.CommunicationRelay;
using ArcticRuins.DataFragment;
using ArcticRuins.LayerDetacher;
using ArcticRuins.ShapeAsteroidStabilizer;
using ArcticRuins.ReceiverFromHub;
using Core.Factory;
using Game.Core.Coordinates;
using Game.Core.GameData.GameModeDefinition;
using Game.Core.Rendering.Islands;
using Game.Core.Rendering.Islands.Connectors;
using Game.Core.Rendering.MeshGeneration;
using Game.Core.Research;
using Global.Core;
using JetBrains.Annotations;
using MonoMod.RuntimeDetour;
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
using UnityEngine.UI;
using ILogger = Core.Logging.ILogger;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;
using Renderer = DiagonalCutterSimulationRenderer;
using Simulation = DiagonalCutterSimulation;
using RendererData = IDiagonalCutterDrawData;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace ArcticRuins
{
    [UsedImplicitly]
    public class ArcticRuinsMod : IMod
    {
        public static readonly ScenarioSelector ArcticRuinsScenarioSelector =
            scenario => scenario.UniqueId.Id.StartsWith("arctic-ruins");
        
        public static ILogger Logger { get; private set; }
        public static ArcticRuinsMod Instance;
        
        public ModFolderLocator Resources { get; }
        public AssetBundle AssetBundle { get; }
        public SaveData SaveData { get; private set; }
        
        private readonly Hook _gameModeHook;
        private readonly Hook _createSimulationRenderersHook;
        private readonly Hook _customMapRenderersHook;
        private readonly Hook _customHUDRenderersHook;

        private StormRenderer _stormRenderer;

        public ArcticRuinsMod(ILogger logger)
        {
            Logger = logger;
            Instance = this;
            
            Resources = ModDirectoryLocator.CreateLocator<ArcticRuinsMod>().SubLocator("Resources");
            AssetBundle = AssetBundle.LoadFromFile(Resources.SubPath("Windows/assets.bundle")); //TODO(opt): Multiplatform
            
            _gameModeHook = CreateGameModeHook();
            _createSimulationRenderersHook = CreateCustomSimulationRenderersHook();
            _customMapRenderersHook = CreateCustomMapRenderersHook();
            _customHUDRenderersHook = CreateCustomHUDRenderersHook();
            RegisterSaveData();
            
            VortexReverser.Register();
            MilestoneReverser.Register();
            AsteroidProgressSystem.Register();
            ArcticCutterBuilding.Register();
            ShapeAsteroidStabilizerBuilding.Register();
            LayerDetacherBuilding.Register();
            CommunicationRelayBuilding.Register();
            DataFragmentBuilding.Register();
            MeshRecolorer.Register();

            BuildingDefinitionGroupId groupId = new("DiagonalCutterGroup");
            BuildingDefinitionId definitionId = new("DiagonalCutter");

            string titleId = "building-variant.cutter-diagonal.title";
            string titleDescription = "building-variant.cutter-diagonal.description";

            using var assetBundleHelper =
                AssetBundleHelper.CreateForAssetBundleEmbeddedWithMod<ArcticRuinsMod>("Resources/DiagonalCutter");

            string iconPath = Resources.SubPath("DiagonalCutter_Icon.png");

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
                .WithStaticDrawData(CreateDrawData(Resources))
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

        private void RegisterSaveData()
        {
            var key = this.ResolveId<SaveData.RawSaveData>();
            var rewirer = new ModSaveDataRewirer<SaveData.RawSaveData>(key + ".json", new LambdaFactory<SaveData.RawSaveData>(() =>
            {
                SaveData = new SaveData();
                return new SaveData.RawSaveData();
            }), Logger);
            rewirer.AfterSaveDataDeserialized.Register(data =>
            {
                SaveData = new SaveData(data);
            });
            rewirer.BeforeSaveDataSerialized.Register(data =>
            {
                data.CopyFrom(SaveData);
            });
            var activeRewirer = new ModSaveDataExtensions.ActiveRewirer(GameRewirers.AddRewirer(rewirer), rewirer);
            ModSaveDataExtensions.Rewirers.Add(key, activeRewirer);
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
            gameData._Images[new GameImageId("OperatorBadgeArcticRuins")] =
                FileTextureLoader.LoadTextureAsSprite(Instance.Resources.SubPath("OperatorBadgeArcticRuins.png"),
                    out _);
        }

        private static Hook CreateCustomSimulationRenderersHook()
        {
            return DetourHelper.CreatePostfixHook<GameSessionOrchestrator, IEnumerable<ISimulationRenderer>>(
                orchestrator => orchestrator.CreateSimulationRenderers(),
                (orchestrator, renderers) =>
                {
                    var renderersList = renderers.ToList();
                    var spacePathReceiverRenderer = (BeltPortTransferSimulationRenderer)renderersList.First(renderer => renderer is BeltPortTransferSimulationRenderer);
                    return renderersList.Concat([
                        new BeltPortReceiverFromHubSimulationRenderer(orchestrator.MapModel,
                            spacePathReceiverRenderer.DrawData, spacePathReceiverRenderer.StopperRenderer,
                            orchestrator.SimulationSpeed, orchestrator._AudioManager.BuildingSound)
                    ]);
                });
        }

        private Hook CreateCustomMapRenderersHook()
        {
            return DetourHelper.CreatePostfixHook<GameSessionOrchestrator, IGameData>(
                (orchestrator, gameData) => orchestrator.Init_7_Rendering(gameData),
                (orchestrator, _) =>
                {
                    StormRenderer.Hook(orchestrator);
                });
        }
        
        private Hook CreateCustomHUDRenderersHook()
        {
            return DetourHelper.CreatePostfixHook<GameSessionOrchestrator>(
                (orchestrator) => orchestrator.Init_8_HUD(),
                (orchestrator) =>
                {
                    AsteroidProgressSystem.HookRenderer(orchestrator);
                });
        }

        public void Dispose()
        {
            _gameModeHook.Dispose();
            _createSimulationRenderersHook.Dispose();
            _customMapRenderersHook.Dispose();
            _customHUDRenderersHook.Dispose();
            VortexReverser.Dispose();
            MilestoneReverser.Dispose();
            AsteroidProgressSystem.Dispose();
            MeshRecolorer.Dispose();
            DataFragmentBuilding.Dispose();
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