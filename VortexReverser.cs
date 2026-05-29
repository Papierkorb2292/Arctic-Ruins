using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using ArcticRuins.ReceiverFromHub;
using Core.Collections;
using Core.Events;
using Core.Events.Logging;
using Game.Buildings.Extractor.Prediction;
using Game.Content.BuildingPath.Simulation;
using Game.Content.Features.Predictions;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using Game.Core.Rendering.MeshGeneration;
using Game.Core.Simulation;
using JetBrains.Annotations;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;
using ShapezShifter.Hijack.Predictions;
using ShapezShifter.SharpDetour;

namespace ArcticRuins
{
    public static class VortexReverser
    {
        private static Hook _hubSystemIsAtInputPositionHook;
        private static Hook _hubSystemCreateSimulationHook;

        private static ConditionalWeakTable<HubSystem, ReversedHubData> _reversedHubData = new();

        public static void Register()
        {
            _hubSystemIsAtInputPositionHook = DetourHelper.CreatePrefixHook<HubSystem, BuildingInstance, IslandInstance, bool>(
                (hubSystem, building, island) =>
                    hubSystem.IsAtInputPosition(building, island),
                (hubSystem, building, island) =>
                {
                    if (!_reversedHubData.TryGetValue(hubSystem, out _)) return (building, island);
                    return (new BuildingInstance(
                        building.Definition,
                        building.Transform.Rotate(GridRotation.Rotate180),
                        building.State,
                        building.Configuration
                    ), island);
                }
            );
            _hubSystemCreateSimulationHook = new Hook(
                DetourHelper.GetRuntimeMethod((Expression<Action<HubSystem, BuildingInstance>>)((hubSystem, building) => hubSystem.CreateSimulation(building))),
                (Action<Action<HubSystem,BuildingInstance>, HubSystem,BuildingInstance>)((orig, hubSystem, building) => 
                {
                    if(_reversedHubData.TryGetValue(hubSystem, out var hubData))
                    {
                        var buildingSimulation = hubData.IsPrediction
                                ? new ConnectableSimulationPredictionAdapter(building, new ExtractorPredictionSimulation(hubData.ShapeSourceProvider))
                                : new ConnectableBuildingSimulation(building, new BeltPortReceiverFromHubSimulation(building.State.New<BeltPortReceiverFromHubSimulationState>(), hubSystem.ConveyorSpeed, hubSystem.ConveyorSpeed, hubData.ShapeSourceProvider));
                        hubSystem.SimulationsByPosition.Add(buildingSimulation.Transform.Position, buildingSimulation);
                        hubSystem.OnSimulationCreated.InvokeSafe(buildingSimulation, hubSystem.Logger);                        
                        return;
                    }
                    orig(hubSystem, building);
                }));
            
            
            RewireSenderReceiverPlacements();
            GameRewirers.AddRewirer<ISimulationSystemsRewirer>(new HubSystemRewirer());
            GameRewirers.AddRewirer<IPredictionSystemsRewirer>(new HubPredictionSystemRewirer());
            
        }

        public static void Dispose()
        {
            _hubSystemIsAtInputPositionHook.Dispose();
            _hubSystemCreateSimulationHook.Dispose();
        }

        private static void RewireSenderReceiverPlacements()
        {
            var chain = RewirerChain.BeginRewiringWith( // Filter the scenario
                new GameScenarioBuildingExtender(
                    scenarioFilter: ArcticRuinsMod.ArcticRuinsScenarioSelector,
                    progressionExtender: NoopProgressionExtender.Instance,
                    groupId: new())
                ).ThenContinueRewiringWith(() => new SenderReceiverPlacementSwapper()); // Configure building placements
            var aggregatedChain = AggregatedChain.WaitFor(chain);
            aggregatedChain.AfterHijack.Register(OnApplyPlacementSwapper);
            return;
            
            void OnApplyPlacementSwapper()
            {
                aggregatedChain.AfterHijack.Unregister(OnApplyPlacementSwapper);
                RewireSenderReceiverPlacements();
            }
        }

        private class SenderReceiverPlacementSwapper : IBuildingsRewirer, IChainableRewirer
        {
            private readonly MultiRegisterEvent _afterExtensionApplied = new();
            
            public bool Equals(IRewirer other) => other is SenderReceiverPlacementSwapper;

            public GameBuildings ModifyGameBuildings(MetaGameModeBuildings metaBuildings, GameBuildings gameBuildings,
                IMeshCache meshCache, VisualThemeBaseResources theme)
            {
                var receiver = gameBuildings._VariantsById[new("BeltPortReceiverVariant")];
                var sender = gameBuildings._VariantsById[new("BeltPortSenderVariant")];

                var receiverRequirements = (IBuildingPlacementRequirement[])receiver.PlacementRequirements;
                var senderRequirements = (IBuildingPlacementRequirement[])sender.PlacementRequirements;
                
                var receiverNotOnHubRequirementIndex = receiverRequirements.FindIndex(requirement =>
                    requirement is BuildingNotOnHubChunkRequirement);
                var senderOnHubRequirementIndex = senderRequirements.FindIndex(requirement =>
                        requirement is CatapultOnHubBorderRequirement);

                // Swap requirements
                senderRequirements[senderOnHubRequirementIndex] =
                    receiverRequirements[receiverNotOnHubRequirementIndex];
                receiverRequirements[receiverNotOnHubRequirementIndex] = new CatcherOnHubBorderRequirement();

                _afterExtensionApplied.Invoke();
                return gameBuildings;
            }

            public IEvent AfterHijack => _afterExtensionApplied;
        }

        private class HubSystemRewirer : ISimulationSystemsRewirer
        {
            public void ModifySimulationSystems(ICollection<ISimulationSystem> simulationSystems, SimulationSystemsDependencies dependencies)
            {
                if (!ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(dependencies.Mode.Scenario))
                    return;
                var hubSystem = (HubSystem)simulationSystems.First(system => system is HubSystem);
                hubSystem.Set(
                    system => system.BeltPortSenderBuildingId,
                    dependencies.Mode.Buildings.BeltPortReceiver.Id
                );
                var shapeFactory = new StrictShapeDefinitionFactory(
                    dependencies.Mode.ShapesConfiguration.PartCount,
                    dependencies.Mode.ShapesConfiguration.Parts,
                    dependencies.Mode.ShapeColorScheme.Colors,
                    new ShapeHashParser(),
                    dependencies.ShapeIdManager
                    );
                // TODO: Replace with proper shape provider
                _reversedHubData.Add(hubSystem, new ReversedHubData(() => {
                    return new TileExtractionShapeProvider(new ShapeMiningStream(new ShapeItem(shapeFactory.CreateShapeDefinition("RuRuRuRu")).AsEnumerable()));
                }, false));
            }

            public bool Equals(IRewirer other) => other is HubSystemRewirer;
        }

        private class HubPredictionSystemRewirer : IPredictionSystemsRewirer
        {
            public bool Equals(IRewirer other) => other is HubPredictionSystemRewirer;

            public void ModifyPredictionSystems(ICollection<ISimulationSystem> simulationSystems, PredictionSystemsDependencies dependencies)
            {
                if (!ArcticRuinsMod.ArcticRuinsScenarioSelector.Invoke(dependencies.Mode.Scenario))
                    return;
                var conveyorSpeed = dependencies.Mode.Buildings.ForwardBelt.ConfigAs<IConveyorConfiguration>()
                    .ConveyorSpeed;
                var hubSystem = new HubSystem(
                    dependencies.Mode.Islands.Hub.Id,
                    dependencies.Mode.Buildings.BeltPortReceiver.Id,
                    conveyorSpeed,
                    dependencies.Logger);
                var shapeFactory = new StrictShapeDefinitionFactory(
                    dependencies.Mode.ShapesConfiguration.PartCount,
                    dependencies.Mode.ShapesConfiguration.Parts,
                    dependencies.Mode.ShapeColorScheme.Colors,
                    new ShapeHashParser(),
                    dependencies.ShapeIdManager
                );
                ((List<ISimulationSystem>)simulationSystems).Insert(0, hubSystem); // Make sure to take priority
                // TODO: Replace with proper shape provider, keep list
                _reversedHubData.Add(hubSystem, new ReversedHubData(() => {
                    return new TileExtractionShapeProvider(new ShapeMiningStream(new ShapeItem(shapeFactory.CreateShapeDefinition("RuRuRuRu")).AsEnumerable()));
                }, true));
            }
        }

        private class CatcherOnHubBorderRequirement : IBuildingPlacementRequirement
        {
            public bool Check(IMapModel map, BuildingDescriptor building, IslandDescriptor island)
            {
                HubSlots hubSlots = StaticGameCoreAccessor.G.HubObserver.HubSlots;
                var transform = building.Transform.Rotate(GridRotation.Rotate180);
                return !hubSlots.IsCoordinateInHubChunk(building.Transform.Position) || hubSlots.IsValidBeltPortInput(in transform);
            }
        }

        private class ReversedHubData(Func<IShapeSourceProvider> shapeSourceProviderFunc, bool isPrediction)
        {
            // Create lazily
            public IShapeSourceProvider ShapeSourceProvider
            {
                get
                {
                    field ??= shapeSourceProviderFunc();
                    return field;
                }
            }
            
            public bool IsPrediction => isPrediction;
        }

        private class ConnectableSimulationPredictionAdapter : ConnectableBuildingSimulation
        {
            private readonly ConnectableBuildingPredictionSimulation _prediction;

            public ConnectableSimulationPredictionAdapter(BuildingInstance building,
                [NotNull] IItemPredictionSimulation simulation) : base(building, simulation)
            {
                _prediction = new ConnectableBuildingPredictionSimulation(building, simulation);
                this.Set(connectable => connectable.Connectors, _prediction.Connectors);
            }
        }
    }
}