using Game.Core.Simulation;
using JetBrains.Annotations;

namespace ArcticRuins.ShapeAsteroidStabilizer
{
    public class ShapeAsteroidStabilizerSimulation : Simulation<ShapeAsteroidStabilizerSimulationState>, IItemSimulation, IUpdatableSimulation
    {
        public readonly DelayBeltLane ProcessingLane;
        public readonly BeltLane InputLane;
        private readonly AsteroidStabilizerTrash TrashInstance = new();

        public ShapeAsteroidStabilizerSimulation([NotNull] ShapeAsteroidStabilizerSimulationState state, IShapeAsteroidStabilizerConfiguration configuration) : base(state)
        {
            ProcessingLane = new DelayBeltLane(configuration.ProcessingDelay, state.ProcessingLaneState, TrashInstance);
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

        public class AsteroidStabilizerTrash : IItemReceiver
        {
            Steps IItemReceiver.MaxStep_S => LaneConstants.ItemSpacing;

            public bool CanAcceptItem(IBeltItem itemToTransfer) => true;

            public void HandOverItem(IBeltItem itemToTransfer, Ticks remainingTicks)
            {
            }
        }
    }
}