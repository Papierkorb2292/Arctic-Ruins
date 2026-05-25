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

        public ShapeAsteroidStabilizerSimulation([NotNull] ShapeAsteroidStabilizerSimulationState state, IShapeAsteroidStabilizerConfiguration configuration, ShapeMiningStream aggregatedResource, Action receivedCallback) : base(state)
        {
            ProcessingLane = new DelayBeltLane(configuration.ProcessingDelay, state.ProcessingLaneState, new AsteroidStabilizerTrash(aggregatedResource, receivedCallback));
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

        private class AsteroidStabilizerTrash(ShapeMiningStream aggregatedResource, Action receivedCallback) : IItemReceiver
        {
            Steps IItemReceiver.MaxStep_S => LaneConstants.ItemSpacing;
            
            // Convert to hash, because item definitions don't implement Equals
            private readonly HashSet<string> _allowedHashes = aggregatedResource.DistinctPossibleShapes.Select(item => item.Definition.Hash).ToHashSet();

            public bool CanAcceptItem(IBeltItem itemToTransfer) => true;

            public void HandOverItem(IBeltItem itemToTransfer, Ticks remainingTicks)
            {
                if(itemToTransfer is ShapeItem shape && _allowedHashes.Contains(shape.Definition.Hash)) {
                    receivedCallback();
                }
            }
        }
    }
}