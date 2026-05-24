using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.ShapeAsteroidStabilizer;

[SyncableIdentifier("AsteroidStabilizerState")]
public class ShapeAsteroidStabilizerSimulationState : ISimulationState
{
    public readonly BeltLaneState InputLaneState = new();
    public readonly BeltLaneState ProcessingLaneState = new();
    
    public void Sync(ISerializationVisitor visitor)
    {
        InputLaneState.Sync(visitor);
        ProcessingLaneState.Sync(visitor);
    }
}