using System;
using System.Collections.Generic;
using System.Linq;
using Core.Collections.Scoped;
using Core.Events;
using Game.Core.Coordinates;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Hijack;
using UnityEngine;

namespace ArcticRuins
{
    public static class Helper
    {
        public static IToolbarEntryInsertLocation NoToolbarEntryLocation = new NoToolbarLocation();
        public static IMilestoneSelector FirstMilestoneSelector = new MilestoneSelectorFirst();
        
        public static BuildingItemInput ToInput(this ShapeConnectorConfig shapeConnectorConfig, TileVector? pos = null)
        {
            return new BuildingItemInput
            {
                Position_L = pos ?? TileVector.Zero,
                Direction_L = shapeConnectorConfig.Direction.Value,
                StandType = shapeConnectorConfig.StandType,
                IOType = shapeConnectorConfig.CapsType,
                Seperators = shapeConnectorConfig.Separators
            };
        }
        
        public static BuildingItemOutput ToOutput(this ShapeConnectorConfig shapeConnectorConfig, TileVector? pos = null)
        {
            return new BuildingItemOutput
            {
                Position_L = pos ?? TileVector.Zero,
                Direction_L = shapeConnectorConfig.Direction.Value,
                StandType = shapeConnectorConfig.StandType,
                IOType = shapeConnectorConfig.CapsType,
                Seperators = shapeConnectorConfig.Separators
            };
        }
        
        public static IToolbarEntryInsertLocation Replace(this IToolbarElementLocator elementLocator)
        {
            return new ToolbarEntryReplaceLocation(elementLocator);
        }

        public static IAtomicBuildingExtender WithCustomSimulationSystem<TConfig>(this IDefinedPlaceableAccessibleBuildingExtender extenderAbstract, CustomSimulationSystemFactory<TConfig> systemFactory)
        {
            var extender = (AtomicBuildingExtender)extenderAbstract;
            extender.LazySimulationExtender = new BuildingSimulationSystemsExtender<TConfig>(systemFactory);
            return extender;
        }

        public static IDefinedUnlockableBuildingExtender UnlockedInDevMode(this IDefinedBuildingExtender extenderAbstract)
        {
            if (ArcticRuinsMod.IsModDevelopmentMode)
                return extenderAbstract.UnlockedAtMilestone(FirstMilestoneSelector);
            return extenderAbstract.NotUnlocked();
        }
        
        public static IDefinedUnlockableBuildingExtender NotUnlocked(this IDefinedBuildingExtender extenderAbstract)
        {
            var extender = (AtomicBuildingExtender)extenderAbstract;
            extender.ProgressionExtender = NoopProgressionExtender.Instance;
            return extender;
        }

        public static IDefinedUnlockableIslandExtender UnlockedInDevMode(this IDefinedIslandExtender extenderAbstract)
        {
            if (ArcticRuinsMod.IsModDevelopmentMode)
                return extenderAbstract.UnlockedAtMilestone(FirstMilestoneSelector);
            var extender = (AtomicIslandExtender)extenderAbstract;
            extender.ProgressionExtender = NoopProgressionExtender.Instance;
            return extender;
        }

        public static IMaterialReference Copy(this IMaterialReference material)
        {
            return new MaterialReference
            {
                _Material = new Material(material.GetMaterialInternal())
            };
        }
        
        public static System.Numerics.Vector2 RotateCCW90(this System.Numerics.Vector2 v)
        {
            return new System.Numerics.Vector2(-v.Y, v.X);
        }

        public static float ManhattanDistToOrigin(this Vector4 v)
        {
            return Mathf.Abs(v.x) + Mathf.Abs(v.y) + Mathf.Abs(v.z) + Mathf.Abs(v.w);
        }

        private class ToolbarEntryReplaceLocation : IToolbarEntryInsertLocation
        {
            public readonly IToolbarElementLocator ElementLocator;
            
            public ToolbarEntryReplaceLocation(IToolbarElementLocator elementLocator)
            {
                ElementLocator = elementLocator;
            }
            
            void IToolbarEntryInsertLocation.AddEntry(
                ToolbarData toolbarData,
                IToolbarElementData elementData)
            {
                IParentToolbarElementData elementParent = ElementLocator.FindElementParent(toolbarData);
                Index index1 = ElementLocator.LeafIndex();
                int index2 = (index1.IsFromEnd ? elementParent.Children.Count() - index1.Value : index1.Value) + 1;
                Debug.Log("Replacing");

                using ScopedList<IToolbarElementData> scopedList = ScopedList<IToolbarElementData>.Get(elementParent.Children);
                scopedList[index2] = elementData;
                IToolbarElementData[] array = scopedList.ToArray();
                switch (elementParent)
                {
                    case RootToolbarElementData toolbarElementData1:
                        toolbarElementData1.Children = array;
                        break;
                    case ParentToolbarElementData toolbarElementData2:
                        toolbarElementData2.Children = array;
                        break;
                }
            }

            public override string ToString() => $"Replace \n{ElementLocator}";
        }

        private class NoToolbarLocation : IToolbarEntryInsertLocation
        {
            public void AddEntry(ToolbarData toolbarData, IToolbarElementData elementData) { }
        }
        
        private class MilestoneSelectorFirst : IMilestoneSelector
        {
            public ResearchLevel Select(ScenarioId scenarioId, IReadOnlyList<ResearchLevel> milestones)
            {
                return milestones[0];
            }
        }

        public delegate ISimulationSystem CustomSimulationSystemFactory<TConfig>(ICollection<ISimulationSystem> simulationSystems, SimulationSystemsDependencies dependencies, BuildingDefinition building, out TConfig config);
        
        private class BuildingSimulationSystemsExtender<TConfig>(CustomSimulationSystemFactory<TConfig> simulationSystemFactory) : AtomicBuildingExtender.ISimulationExtender
        {
            public RewirerChainLink ContinueAfter(
                RewirerChainLink<BuildingDefinition> rewirerChainLink)
            {
                return rewirerChainLink.ThenContinueRewiringWith(building => new BuildingSimulationExtender<TConfig>(building, simulationSystemFactory))
                    .ThenContinueRewiringWith(config => new BuffablesExtender<TConfig>(config));
            }
        }

        private class BuildingSimulationExtender<TConfig>(BuildingDefinition building, CustomSimulationSystemFactory<TConfig> simulationSystemFactory) : ISimulationSystemsRewirer, IChainableRewirer<TConfig>
        {
            private readonly CustomSimulationSystemFactory<TConfig> _simulationSystemFactory = simulationSystemFactory;
            
            private readonly MultiRegisterEvent<TConfig> _afterExtensionApplied = new();
            public IEvent<TConfig> AfterHijack => _afterExtensionApplied;
            public bool Equals(IRewirer other)
            {
                return other is BuildingSimulationExtender<TConfig> extender &&
                       _simulationSystemFactory == extender._simulationSystemFactory;
            }

            public void ModifySimulationSystems(ICollection<ISimulationSystem> simulationSystems, SimulationSystemsDependencies dependencies)
            {
                var simulationSystem = _simulationSystemFactory(simulationSystems, dependencies, building, out var config);
                simulationSystems.Add(simulationSystem);
                _afterExtensionApplied.Invoke(config);
            }
        }

    }
}