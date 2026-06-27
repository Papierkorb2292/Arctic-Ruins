using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Simulation;
using JetBrains.Annotations;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    public class ShapeAsteroidStabilizerSimulation : Simulation<ShapeAsteroidStabilizerSimulationState>, IItemSimulation, IUpdatableSimulation
    {
        public readonly DelayBeltLane ProcessingLane;
        public readonly BeltLane InputLane;

        public readonly HashSet<ShapeId> AllowedIds = [];

        public ShapeAsteroidStabilizerSimulation([NotNull] ShapeAsteroidStabilizerSimulationState state, IShapeAsteroidStabilizerConfiguration configuration, ShapeMiningStream aggregatedResource, Action receivedCallback) : base(state)
        {
            // Allow all rotations, also use ids because they implement Equals
            var shapeUnifier = StaticGameCoreAccessor.G.Research.ShapeUnifier;
            foreach (var possibleShape in aggregatedResource.DistinctPossibleShapes)
            {
                shapeUnifier.GetRotatedShapeIds(possibleShape.Definition.Id, "Stabilizer", AllowedIds);
            }
            ProcessingLane = new DelayBeltLane(configuration.ProcessingDelay, state.ProcessingLaneState, new AsteroidStabilizerTrash(AllowedIds, receivedCallback));
            InputLane = new BeltLane(configuration.BeltSpeed, state.InputLaneState, ProcessingLane);
        }

        public int NumItemReceivers => 1;
        public int NumItemProviders => 0;

        public void TraverseLanes<TTraverser>(TTraverser traverser) where TTraverser : IItemLaneTraverser
        {
            traverser.Traverse(InputLane);
            traverser.Traverse(ProcessingLane); 
        }
        
        public void Update(Ticks startTicks, Ticks deltaTicks)
        {
            ProcessingLane.Update(deltaTicks);
            InputLane.Update(deltaTicks);
        }

        public IItemReceiver GetItemReceiver(int index) => InputLane;

        private class AsteroidStabilizerTrash(HashSet<ShapeId> allowedIds, Action receivedCallback) : IItemReceiver
        {
            Steps IItemReceiver.MaxStep_S => LaneConstants.ItemSpacing;

            public bool CanAcceptItem(IBeltItem itemToTransfer) => true;

            public void HandOverItem(IBeltItem itemToTransfer, Ticks remainingTicks)
            {
                if(itemToTransfer is ShapeItem shape && allowedIds.Contains(shape.Definition.Id)) {
                    receivedCallback();
                }
            }
        }
    }
}