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

        public readonly HashSet<string> AllowedHashes;

        public ShapeAsteroidStabilizerSimulation([NotNull] ShapeAsteroidStabilizerSimulationState state, IShapeAsteroidStabilizerConfiguration configuration, ShapeMiningStream aggregatedResource, Action receivedCallback) : base(state)
        {
            // Convert to hash, because item definitions don't implement Equals
            // TODO(opt): The proper solution would be ShapeId
            AllowedHashes = aggregatedResource.DistinctPossibleShapes.Select(item => item.Definition.Hash)
                .ToHashSet();
            ProcessingLane = new DelayBeltLane(configuration.ProcessingDelay, state.ProcessingLaneState, new AsteroidStabilizerTrash(AllowedHashes, receivedCallback));
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

        private class AsteroidStabilizerTrash(HashSet<string> allowedHashes, Action receivedCallback) : IItemReceiver
        {
            Steps IItemReceiver.MaxStep_S => LaneConstants.ItemSpacing;

            public bool CanAcceptItem(IBeltItem itemToTransfer) => true;

            public void HandOverItem(IBeltItem itemToTransfer, Ticks remainingTicks)
            {
                if(itemToTransfer is ShapeItem shape && allowedHashes.Contains(shape.Definition.Hash)) {
                    receivedCallback();
                }
            }
        }
    }
}