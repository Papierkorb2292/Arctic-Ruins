using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Core.Collections;
using Core.Localization;
using ArcticRuins.ArcticCutter;
using ArcticRuins.ArcticPlatform;
using ArcticRuins.CommunicationRelay;
using ArcticRuins.DataFragment;
using ArcticRuins.LayerDetacher;
using ArcticRuins.ShapeAsteroidStabilizer;
using ArcticRuins.ReceiverFromHub;
using ArcticRuins.Ruins;
using Core.Factory;
using Game.Core.Content.Buildings;
using Game.Core.Content.Islands;
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
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace ArcticRuins
{
    [UsedImplicitly]
    public class ArcticRuinsMod : IMod
    {
        public static readonly ScenarioSelector ArcticRuinsScenarioSelector =
            scenario => scenario.UniqueId.Id.StartsWith("arctic-ruins");

        public const bool IsModDevelopmentMode = false;

        public static ILogger Logger { get; private set; }
        public static ArcticRuinsMod Instance;
        
        public ModFolderLocator Resources { get; }
        public AssetBundle AssetBundle { get; }
        public SaveData SaveData { get; private set; }
        
        public List<(BuildingDefinitionId, BuildingDefinitionGroupId)> CustomBuildings = [];

        [CanBeNull] public StormRenderer StormRenderer { get; private set; }
        [CanBeNull] public IntroRenderer IntroRenderer { get; private set; }
        
        private readonly Hook _mainMenuHook;
        private readonly Hook _createSimulationRenderersHook;
        private readonly Hook _customMapRenderersHook;
        private readonly Hook _customHUDRenderersHook;
        private readonly Hook _addBuildinsToCatalogHook;

        public ArcticRuinsMod(ILogger logger)
        {
            Logger = logger;
            Instance = this;
            
            Resources = ModDirectoryLocator.CreateLocator<ArcticRuinsMod>().SubLocator("Resources");
            string assetBundleFolder;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                    assetBundleFolder = "Windows";
                    break;
                case RuntimePlatform.LinuxPlayer:
                    assetBundleFolder = "Linux";
                    break;
                case RuntimePlatform.OSXPlayer:
                    assetBundleFolder = "Mac";
                    break;
                default:
                    Logger.Error?.Log($"Game uses unknown platform type {Application.platform}, could not determine asset bundle to use. Selecting Windows as default");
                    assetBundleFolder = "Windows";
                    break;
            }

            AssetBundle = AssetBundle.LoadFromFile(Resources.SubPath($"{assetBundleFolder}/assets.bundle"));
            
            _mainMenuHook = InitMainMenu();
            _createSimulationRenderersHook = CreateCustomSimulationRenderersHook();
            _customMapRenderersHook = CreateCustomMapRenderersHook();
            _customHUDRenderersHook = CreateCustomHUDRenderersHook();
            _addBuildinsToCatalogHook = CreateAddBuildingsToCatalogHook();
            RegisterSaveData();
            
            VortexReverser.Register();
            MilestoneReverser.Register();
            AsteroidProgressSystem.Register();
            //ArcticCutterBuilding.Register(); // Not done in time for the deadline
            ShapeAsteroidStabilizerBuilding.Register();
            LayerDetacherBuilding.Register();
            CommunicationRelayBuilding.Register();
            DataFragmentBuilding.Register();
            RuinsBuildings.Register();
            MeshRecolorer.Register();
            StormRenderer.Register();
            ArcticPlatformIsland.Register();
            ArcticMapGenerator.Register();
            IntroRenderer.Register();
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


        private static Hook InitMainMenu()
        {
            return DetourHelper
                .CreatePostfixHook<MainMenuOrchestrator, GameSessionOrchestrator, GlobalsData, IGameData>(
                    (mainMenuOrchestrator, gameSessionOrchestrator, globalsData, gameData) =>
                        mainMenuOrchestrator.Step_0_1_InitDependencies(gameSessionOrchestrator, globalsData, gameData),
                    (_, _, _, gameData) =>
                    {
                        AddGameMode((GameData)gameData);
                        AddIcons((GameData)gameData);
                    });
        }

        private static void AddGameMode(GameData gameData)
        {
            var baseMode = gameData._GameModes.Values.First();
            gameData._GameModes[new GameModeId("ArcticRuins")] = new GameModeDefinition(
                (MetaGameModeDefinition)ScriptableObject.CreateInstance(typeof(MetaGameModeDefinition), scriptable =>
                {
                    //TODO: Add proper assets
                    var mode = (MetaGameModeDefinition)scriptable;
                    mode.Icon = FileTextureLoader.LoadTextureAsSprite(Instance.Resources.SubPath("DataFragment_Icon.png"), out _);
                    mode.VideoPreview = baseMode.VideoPreview;
                    mode.ImagePreview = baseMode.ImagePreview;
                    mode.TrainSimulationConfiguration = baseMode.TrainSimulationConfiguration;
                    mode.name = "ArcticRuins";
                }));
            gameData._Images[new GameImageId("OperatorBadgeArcticRuins")] =
                FileTextureLoader.LoadTextureAsSprite(Instance.Resources.SubPath("OperatorBadgeArcticRuins.png"),
                    out _);
        }

        private static void AddIcons(GameData gameData)
        {
            gameData._Icons.AddIcon(new GameIconId($"building.{LayerDetacherBuilding.GroupId}"), LayerDetacherBuilding.Icon);
            gameData._Icons.AddIcon(new GameIconId($"building.{ShapeAsteroidStabilizerBuilding.GroupId}"), ShapeAsteroidStabilizerBuilding.Icon);
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
            return DetourHelper.CreatePrefixHook<GameSessionOrchestrator>(
                orchestrator => orchestrator.Init_8_HUD(),
                orchestrator =>
                {
                    IntroRenderer = IntroRenderer.HookRenderer(orchestrator);
                    StormRenderer = StormRenderer.HookRenderer(orchestrator);
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

        private Hook CreateAddBuildingsToCatalogHook()
        {
            // Add the buildings when the research progression tries to remove unavailable rewards, so
            // the building rewards are not removed
            return DetourHelper.CreateStaticPrefixHook<ResearchProgression, IResearchUpgrade, IBuildingsCatalog, IIslandsCatalog>(
                (progression, upgrade, buildingsCatalog, islandsCatalog) => ResearchProgression.RemoveNotAvailableRewards(upgrade, buildingsCatalog, islandsCatalog), 
                (upgrade, buildingsCatalog, islandsCatalog) =>
                {
                    var buildings = (BuildingsCatalog)buildingsCatalog;
                    foreach (var (building, group) in CustomBuildings)
                    {
                        if(!buildings.GroupMap.TryAdd(building, group))
                            continue;
                        buildings.GroupedMap.AddValue(group, building);
                    }
                    
                    return (upgrade, buildingsCatalog, islandsCatalog);
                });
        }

        public void Dispose()
        {
            _mainMenuHook.Dispose();
            _createSimulationRenderersHook.Dispose();
            _customMapRenderersHook.Dispose();
            _customHUDRenderersHook.Dispose();
            _addBuildinsToCatalogHook.Dispose();
            VortexReverser.Dispose();
            MilestoneReverser.Dispose();
            AsteroidProgressSystem.Dispose();
            MeshRecolorer.Dispose();
            DataFragmentBuilding.Dispose();
            StormRenderer.Dispose();
            ArcticMapGenerator.Dispose();
            IntroRenderer.Dispose();
        }

        public SoundEffect LoadSoundFromAssetBundle(string name, GameSessionOrchestrator session)
        {
            var effect = ScriptableObject.CreateInstance<SoundEffect>();
            effect.Clips = [
                    AssetBundle.LoadAsset<AudioClip>(name)
            ];
            // Copy some values from a sound from the game, so I don't need to deal with them
            var baseSound = session.AssetRefs.AudioConfig.UISoundEffects.ResearchAvailable;
            effect.MixerGroup = baseSound.MixerGroup;
            effect.SoundEffectSourcePrefab = baseSound.SoundEffectSourcePrefab;
            return effect;
        }
    }
}