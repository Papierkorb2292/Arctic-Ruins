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
        private readonly ShapeMiningStream _aggregatedResource; 

        public ShapeAsteroidStabilizerSimulation([NotNull] ShapeAsteroidStabilizerSimulationState state, IShapeAsteroidStabilizerConfiguration configuration, ShapeMiningStream aggregatedResource, Action receivedCallback) : base(state)
        {
            _aggregatedResource = aggregatedResource;
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
            if (AllowedIds.Count == 0 && _aggregatedResource.DistinctPossibleShapes.Count != 0)
            {
                // Allow all rotations, also use ids because they implement Equals
                // This is done in Update, because the shape unifier isn't available when the map is loaded
                // and it would be too much effort to get the game session from somewhere
                var shapeUnifier = StaticGameCoreAccessor.G.Research.ShapeUnifier;
                foreach (var possibleShape in _aggregatedResource.DistinctPossibleShapes)
                {
                    shapeUnifier.GetRotatedShapeIds(possibleShape.Definition.Id, "Stabilizer", AllowedIds);
                }
            }

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