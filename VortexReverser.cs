using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Core.Events;
using Core.Logging;
using Game.Core.Coordinates;
using Game.Core.Rendering.MeshGeneration;
using Game.Placement.Data;
using Game.Placement.Processing;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;
using ShapezShifter.SharpDetour;

namespace ArcticRuins
{
    public static class VortexReverser
    {
        private static Hook _hubSystemIsAtInputPositionHook;
        public static void Register(ArcticRuinsMod mod)
        {
            
            _hubSystemIsAtInputPositionHook = DetourHelper.CreatePrefixHook<HubSystem, BuildingInstance, IslandInstance, bool>(
                (hubSystem, building, island) =>
                    hubSystem.IsAtInputPosition(building, island),
                (hubSystem, building, island) =>
                {
                    var rotatedBuilding = new BuildingInstance(
                        building.Definition,
                        building.Transform.Rotate(GridRotation.Rotate180),
                        building.State,
                        building.Configuration
                        );
                    return (rotatedBuilding, island);
                }
            );
            
            RewireSenderReceiverPlacements();
            GameRewirers.AddRewirer<ISimulationSystemsRewirer>(new HubSystemRewirer());
            
        }

        public static void Dispose()
        {
            _hubSystemIsAtInputPositionHook.Dispose();   
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
                typeof(HubSystem)
                    .GetField("BeltPortSenderBuildingId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(hubSystem, dependencies.Mode.Buildings.BeltPortReceiver.Id);
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
    }
}