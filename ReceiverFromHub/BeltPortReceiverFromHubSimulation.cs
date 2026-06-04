using Game.Core.Belts.BeltPath;
using Game.Core.Simulation;

namespace ArcticRuins.ReceiverFromHub;

public class BeltPortReceiverFromHubSimulation : Simulation<BeltPortReceiverFromHubSimulationState>, IItemSimulation, IUpdatableSimulation
{
  public readonly BeltLane OutputLane;
  public readonly FastBeltPathLane VortexLane;
  
  public int NumItemReceivers => 0;

  public int NumItemProviders => 1;
  
  public int ItemDeliveryCount { get; private set; }

  public BeltPortReceiverFromHubSimulation(
    BeltPortReceiverFromHubSimulationState state,
    BeltSpeed outputSpeed,
    BeltSpeed vortexSpeed)
    : base(state)
  {
    OutputLane = new BeltLane(outputSpeed, state.OutputLaneState);
    VortexLane = new FastBeltPathLane(vortexSpeed, state.VortexLaneState, OutputLane);
  }
  
  public IItemProvider GetItemProvider(int index) => OutputLane;

  public void TraverseLanes<TTraverser>(TTraverser traverser) where TTraverser : IItemLaneTraverser
  {
    traverser.Traverse(OutputLane);
    traverser.Traverse(VortexLane);
  }

  public void Update(Ticks startTicks, Ticks deltaTicks)
  {
    VortexLane.Update(deltaTicks);
    OutputLane.Update(deltaTicks);
    // Make sure that the renderer creates animations for items that were already on the belt when it was loaded
    if(VortexLane.ItemCount > ItemDeliveryCount)
      ItemDeliveryCount = VortexLane.ItemCount;
    if(State.BufferedItem is null || !VortexLane.CanAcceptItem(State.BufferedItem))
      return;
    TryCreateItemOnVortexLane(deltaTicks);
  }

  private void TryCreateItemOnVortexLane(Ticks progress_T)
  {
    VortexLane.HandOverItem(State.BufferedItem, progress_T);
    State.BufferedItem = null;
    ItemDeliveryCount++;
  }

  public void ClearContent()
  {
    State.VortexLaneState.Clear();
    State.OutputLaneState.Clear();
    State.BufferedItem = null;
  }

  public bool OfferItem(ShapeItem item)
  {
    if (State.BufferedItem != null) return false;
    State.BufferedItem = item;
    return true;

  }
}