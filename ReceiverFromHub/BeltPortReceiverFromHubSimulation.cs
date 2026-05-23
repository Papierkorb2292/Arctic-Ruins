using Game.Core.Belts.BeltPath;
using Game.Core.Simulation;

namespace ArcticRuins.ReceiverFromHub;

public class BeltPortReceiverFromHubSimulation : Simulation<BeltPortReceiverFromHubSimulationState>, IItemSimulation, IUpdatableSimulation
{
  public readonly BeltLane OutputLane;
  public readonly FastBeltPathLane VortexLane;
  private readonly IShapeSourceProvider _shapeSourceProvider;

  public int NumItemReceivers => 0;

  public int NumItemProviders => 1;

  public BeltPortReceiverFromHubSimulation(
    BeltPortReceiverFromHubSimulationState state,
    BeltSpeed outputSpeed,
    BeltSpeed vortexSpeed,
    IShapeSourceProvider shapeSourceProvider)
    : base(state)
  {
    _shapeSourceProvider = shapeSourceProvider;
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
    if(!_shapeSourceProvider.TryPeek(out var shape) || !VortexLane.CanAcceptItem(shape))
      return;
    TryCreateItemOnVortexLane(deltaTicks);
  }

  private void TryCreateItemOnVortexLane(Ticks progress_T)
  {
    if (!_shapeSourceProvider.TryConsume(out var shape))
      return;
    VortexLane.HandOverItem(shape, progress_T);
  }

  public void ClearContent()
  {
    State.VortexLaneState.Clear();
    State.OutputLaneState.Clear();
  }
}