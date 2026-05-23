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
using Game.Core.Coordinates;
using Game.Core.Rendering.MeshGeneration;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;
using ShapezShifter.SharpDetour;

namespace ArcticRuins
{
    public static class VortexReverser
    {
        private static Hook _hubSystemIsAtInputPositionHook;
        private static Hook _hubSystemCreateSimulationHook;

        private static ConditionalWeakTable<HubSystem, ReversedHubData> _reversedHubData = new();

        public static void Register(ArcticRuinsMod mod)
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
                        BeltPortReceiverFromHubSimulation receiverFromHubSimulation = new(building.State.New<BeltPortReceiverFromHubSimulationState>(), hubSystem.ConveyorSpeed, hubSystem.ConveyorSpeed, hubData.ShapeSourceProvider);
                        ConnectableBuildingSimulation buildingSimulation = new(building, receiverFromHubSimulation);
                        hubSystem.SimulationsByPosition.Add(buildingSimulation.Transform.Position, buildingSimulation);
                        hubSystem.OnSimulationCreated.InvokeSafe(buildingSimulation, hubSystem.Logger);                        
                        return;
                    }
                    orig(hubSystem, building);
                }));
            
            
            RewireSenderReceiverPlacements();
            GameRewirers.AddRewirer<ISimulationSystemsRewirer>(new HubSystemRewirer());
            
        }

        public static void Dispose()
        {
            _hubSystemIsAtInputPositionHook.Dispose();
            //_hubSystemCreateSimulationHook.Dispose();
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
            
            public bool Equals(IRewirer other) => this == other;

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
                }));
            }

            public bool Equals(IRewirer other) => this == other;
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

        private class ReversedHubData(Func<IShapeSourceProvider> shapeSourceProviderFunc)
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
        }
    }
}