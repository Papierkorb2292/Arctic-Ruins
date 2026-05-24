namespace ArcticRuins.ShapeAsteroidStabilizer;

public class ShapeShapeAsteroidStabilizerConfiguration : IShapeAsteroidStabilizerConfiguration
{
    public BeltSpeed BeltSpeed => _speed;
    public BeltDelay ProcessingDelay => _delay;

    private readonly BuffableBeltSpeed _speed;
    private readonly BuffableBeltDelay _delay;
    
    public ShapeShapeAsteroidStabilizerConfiguration(
        BuffableBeltSpeed.DiscreteSpeed beltSpeed,
        BuffableBeltDelay.DiscreteDuration processingDelay,
        ResearchSpeedId researchSpeed)
    {
        _speed = new BuffableBeltSpeed
        {
            BaseSpeed = beltSpeed,
            ResearchId = researchSpeed
        };
        _delay = new BuffableBeltDelay
        {
            BaseDuration = processingDelay,
            Research = researchSpeed
        };
        
        _speed.OnAfterDeserialize();
        _delay.OnAfterDeserialize();
    }
}