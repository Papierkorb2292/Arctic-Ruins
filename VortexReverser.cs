using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Game.Core.GameMode;
using Game.Core.Map.Simulation;
using Game.Core.Rendering.MeshGeneration;
using Game.Core.Simulation;
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
        private static Hook _hubSystemUpdateHook;
        private static Hook _skipApplyGameBalancingParametersHook;

        private static ConditionalWeakTable<HubSystem, ReversedHubData> _reversedHubData = new();

        private static bool _skipApplyGameBalancingParameters;

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
                                ? new ConnectableSimulationPredictionAdapter(building, new ExtractorPredictionSimulation(hubData.GetSourceProvider(building.Transform.Rotation.ToTileDirection())))
                                : new ConnectableBuildingSimulation(building, new BeltPortReceiverFromHubSimulation(building.State.New<BeltPortReceiverFromHubSimulationState>(), hubSystem.ConveyorSpeed, hubSystem.ConveyorSpeed));
                        hubSystem.SimulationsByPosition.Add(buildingSimulation.Transform.Position, buildingSimulation);
                        hubSystem.OnSimulationCreated.InvokeSafe(buildingSimulation, hubSystem.Logger);                        
                        return;
                    }
                    orig(hubSystem, building);
                }));
            _hubSystemUpdateHook = DetourHelper.CreatePostfixHook<HubSystem, Ticks, Ticks>
            ((hubSystem, time, delta) => hubSystem.Update(time, delta),
                (hubSystem, _, delta) => 
                {
                    SupplyShapes(hubSystem, delta);
                });
            _skipApplyGameBalancingParametersHook = new Hook(
                DetourHelper.GetRuntimeMethod((Expression<Action<ResearchProgressionBalancer, ResearchProgression, ResearchPlayerLevelConfig>>)((balancer, progression, config) => balancer.ApplyGameBalancingParameters(progression, config))),
                (Action<Action<ResearchProgressionBalancer, ResearchProgression, ResearchPlayerLevelConfig>, ResearchProgressionBalancer, ResearchProgression, ResearchPlayerLevelConfig>)((orig, balancer, progression, config) => 
                {
                    if (_skipApplyGameBalancingParameters)
                    {
                        ArcticRuinsMod.Logger.Info!.Log("Skipping!!");
                        _skipApplyGameBalancingParameters = false;
                        return;
                    }
                    ArcticRuinsMod.Logger.Info!.Log("Not Skipping??");
                    orig(balancer, progression, config);
                }));
            
            RewireSenderReceiverPlacements();
            GameRewirers.AddRewirer<ISimulationSystemsRewirer>(new HubSystemRewirer());
            GameRewirers.AddRewirer<IPredictionSystemsRewirer>(new HubPredictionSystemRewirer());
            // Skip ApplyGameBalancingParameters for custom game mode to not mess with shape count
            var skipGameBalancingParametersRewireFilter = new GameScenarioBuildingExtender(
                scenarioFilter: ArcticRuinsMod.ArcticRuinsScenarioSelector,
                progressionExtender: NoopProgressionExtender.Instance,
                groupId: new());
            skipGameBalancingParametersRewireFilter.AfterHijack.Register(() => _skipApplyGameBalancingParameters = true);
            GameRewirers.AddRewirer(skipGameBalancingParametersRewireFilter);
        }

        public static void Dispose()
        {
            _hubSystemIsAtInputPositionHook.Dispose();
            _hubSystemCreateSimulationHook.Dispose();
            _hubSystemUpdateHook.Dispose();
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

        private static void SupplyShapes(HubSystem system, Ticks deltaTicks)
        {
            if (!_reversedHubData.TryGetValue(system, out var data) || data.IsPrediction)
                return;
            var saveData = ArcticRuinsMod.Instance.SaveData;
            if (system._HubIslands.Count == 0)
                return;
            var island = system._HubIslands.First();
            var locationCountPerSide = data.LayerCount * 12; 
            var locationCountTotal = locationCountPerSide * 4;
            foreach (var shape in saveData.VortexSides.Select(entry => entry.Value.Shape).Distinct())
            {
                var supplierData = saveData.GetVortexShapeSupplierData(shape.ShapeHash);
                
                var processedLocations = 0;
                var newProgress = supplierData.progressSteps + (int)shape.Amount * deltaTicks * data.BeltSpeed.StepsPerTick;
                var newShapes = newProgress / LaneConstants.ItemSpacing;
                supplierData.progressSteps = newProgress % LaneConstants.ItemSpacing;
                while (newShapes > 0 && processedLocations <= locationCountTotal)
                {
                    var direction = supplierData.roundRobinLocation / locationCountPerSide;
                    var layer = (supplierData.roundRobinLocation / 12) % data.LayerCount;
                    var offset = supplierData.roundRobinLocation % 12;
                    var (tileVector, tileDirection) = direction switch
                    {
                        0 => (new TileVector(4 + offset, 0, (short)layer), TileDirection.North),
                        1 => (new TileVector(19, 4 + offset, (short)layer), TileDirection.East),
                        2 => (new TileVector(15 - offset, 19, (short)layer), TileDirection.South),
                        _ => (new TileVector(0, 15 - offset, (short)layer), TileDirection.West),
                    };
                    var position = island.Position.ToOrigin_G() + tileVector;

                    if (!data.GetSourceProvider(tileDirection).TryPeek(out var shapeItem) ||
                        shapeItem.Definition.Hash != shape.ShapeHash)
                    {
                        // Skip this side
                        var nextSideLocation = (direction + 1) * locationCountPerSide;
                        processedLocations += nextSideLocation - supplierData.roundRobinLocation;
                        supplierData.roundRobinLocation = nextSideLocation % locationCountTotal;
                        continue;
                    }
                    
                    if (system.SimulationsByPosition.TryGetValue(position, out var simulation)
                        && simulation.Simulation is BeltPortReceiverFromHubSimulation receiver
                        && receiver.OfferItem(shapeItem))
                    {
                        newShapes--;
                    }

                    supplierData.roundRobinLocation = (supplierData.roundRobinLocation + 1) % locationCountTotal;
                    processedLocations++;
                }
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
                var beltSpeed = new BuffableBeltSpeed()
                {
                    BaseSpeed = BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
                    ResearchId = new ResearchSpeedId("BeltSpeed")
                };
                beltSpeed.OnAfterDeserialize();

                RewirerChain.BeginRewiringWith(new BuffablesExtender<BuffableBeltSpeed>(beltSpeed));
                
                _reversedHubData.Add(hubSystem, new ReversedHubData(shapeFactory, beltSpeed, dependencies.Mode.MaxBuildingLayer + 1, false));
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
                _reversedHubData.Add(hubSystem, new ReversedHubData(shapeFactory, null, dependencies.Mode.MaxBuildingLayer + 1, true));
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

        private class ReversedHubData(IShapeDefinitionFactory shapeDefinitionFactory, IBeltSpeed beltSpeed, int layerCount, bool isPrediction)
        {
            public IShapeSourceProvider GetSourceProvider(TileDirection direction) =>
                new VortexSideConstantShapeSourceProvider(direction, shapeDefinitionFactory);
            
            public bool IsPrediction => isPrediction;
            public IBeltSpeed BeltSpeed => beltSpeed;
            public int LayerCount => layerCount;
        }

        private class ConnectableSimulationPredictionAdapter : ConnectableBuildingSimulation
        {
            private readonly ConnectableBuildingPredictionSimulation _prediction;

            public ConnectableSimulationPredictionAdapter(BuildingInstance building,
                [JetBrains.Annotations.NotNull] IItemPredictionSimulation simulation) : base(building, simulation)
            {
                _prediction = new ConnectableBuildingPredictionSimulation(building, simulation);
                this.Set(connectable => connectable.Connectors, _prediction.Connectors);
            }
        }

        private class VortexSideConstantShapeSourceProvider(TileDirection direction, IShapeDefinitionFactory shapeFactory) : IShapeSourceProvider
        {
            private ShapeItem GetShape()
            {
                var researchCostShapes = ArcticRuinsMod.Instance.SaveData.GetShapeForVortexSide(direction);
                return researchCostShapes == null ? null : new ShapeItem(shapeFactory.CreateShapeDefinition(researchCostShapes.ShapeHash)); 
            }
            
            public bool TryPeek([UnscopedRef] out ShapeItem shape)
            {
                shape = GetShape();
                return shape != null;
            }

            public bool TryConsume([UnscopedRef] out ShapeItem shape)
            {
                shape = GetShape();
                return shape != null;
            }

            public IReadOnlyList<ShapeItem> DistinctPossibleShapes => [ GetShape() ];
        }
    }
}