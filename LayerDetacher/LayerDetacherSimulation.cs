using System;
using Game.Core.Simulation;

namespace ArcticRuins.LayerDetacher
{
    public class LayerDetacherSimulation : Simulation<LayerDetacherSimulationState>, IItemSimulation, IUpdatableSimulation
    {
        public readonly BeltLane InputLane;
        public readonly BeltLane LeftOutputLane;
        public readonly BeltLane RightOutputLane;
        public readonly DelayBeltLane LeftProcessingLane;
        public readonly DelayBeltLane RightProcessingLane;
        
        public int NumItemReceivers => 1;
        public int NumItemProviders => 2;
        
        public LayerDetacherSimulation(LayerDetacherSimulationState state, ILayerDetacherConfiguration configuration, IShapeRegistry shapeRegistry, ShapeOperationLayerDetach detachOp) : base(state)
        {
            LeftOutputLane = new BeltLane(configuration.BeltSpeed, state.LeftOutputLaneState);
            RightOutputLane = new BeltLane(configuration.BeltSpeed, state.RightOutputLaneState);
            LeftProcessingLane = new DelayBeltLane(
                configuration.ProcessingDelay,
                state.LeftProcessingLaneState,
                LeftOutputLane);
            RightProcessingLane = new DelayBeltLane(
                configuration.ProcessingDelay,
                state.RightProcessingLaneState,
                RightOutputLane);
            InputLane = new BeltLane(configuration.BeltSpeed, state.InputLaneState, RightProcessingLane);
            
            InputLane.MaxStepClampHook = (_, maxStep_S) =>
            { 
                if (LeftOutputLane.HasItem)
                    maxStep_S = Steps.Min(maxStep_S, LeftOutputLane.MaxStep_S + LaneConstants.ItemSpacing);
                if (RightOutputLane.HasItem)
                    maxStep_S = Steps.Min(maxStep_S, RightOutputLane.MaxStep_S + LaneConstants.ItemSpacing);
                if (LeftProcessingLane.HasItem || RightProcessingLane.HasItem)
                    maxStep_S = Steps.Min(maxStep_S, Steps.Zero);
                return Steps.Max(Steps.Zero, maxStep_S);
            };
            RightProcessingLane.PreAcceptHook = _ => !RightOutputLane.HasItem && !LeftOutputLane.HasItem && !LeftProcessingLane.HasItem && !RightProcessingLane.HasItem;
            RightProcessingLane.AcceptHook = (_, ref receivedItem, ref remainingTicks_T) =>
            {
                State.LastProcessedShape = (ShapeItem)receivedItem;
                var shapeCutResult = detachOp.Execute(((ShapeItem)receivedItem).Definition);
                var leftSide = shapeCutResult.TopLayer;
                var id1 = leftSide?.Shape ?? ShapeId.Invalid;
                var itemToTransfer = shapeRegistry.GetItem(id1);
                var rightSide = shapeCutResult.LowerLayers;
                var id2 = rightSide?.Shape ?? ShapeId.Invalid;
                var shapeItem = shapeRegistry.GetItem(id2);
                state.LeftCollapseResult = shapeCutResult.TopLayer;
                state.RightCollapseResult = shapeCutResult.LowerLayers;
                if (itemToTransfer == null && shapeItem == null)
                    return;
                if (itemToTransfer != null)
                    LeftProcessingLane.HandOverItem(itemToTransfer, remainingTicks_T);
                receivedItem = shapeItem;
            };
            RightOutputLane.AcceptHook = (_, ref item, ref _) =>
            {
                if(State.RightCollapseResult.ResultsInEmptyShape) item = null;
            };
            LeftOutputLane.AcceptHook = (_, ref item, ref _) =>
            {
                if(State.LeftCollapseResult.ResultsInEmptyShape) item = null;
            };
        }
        
        public IItemReceiver GetItemReceiver(int index) => InputLane;
        public IItemProvider GetItemProvider(int index) => index != 0 ? RightOutputLane : LeftOutputLane;

        public void TraverseLanes<TTraverser>(TTraverser traverser) where TTraverser : IItemLaneTraverser
        {
            traverser.Traverse(InputLane);
            traverser.Traverse(LeftProcessingLane);
            traverser.Traverse(RightProcessingLane);
            traverser.Traverse(LeftOutputLane);
            traverser.Traverse(RightOutputLane);
        }

        public void ClearContent()
        {
            TraverseLanes(ClearItemsItemLaneTraverser.Default);
        }
        
        public void Update(Ticks startTicks, Ticks deltaTicks)
        {
            LeftOutputLane.Update(deltaTicks);
            RightOutputLane.Update(deltaTicks);
            LeftProcessingLane.Update(deltaTicks);
            RightProcessingLane.Update(deltaTicks);
            InputLane.Update(deltaTicks);
        }
    }
}