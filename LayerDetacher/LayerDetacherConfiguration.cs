namespace ArcticRuins.LayerDetacher;

public class LayerDetacherConfiguration : ILayerDetacherConfiguration
{
    public BeltSpeed BeltSpeed => _speed;

    public BeltDelay ProcessingDelay => _delay;
    private readonly BuffableBeltDelay _delay;

    private readonly BuffableBeltSpeed _speed;

    public LayerDetacherConfiguration(
        BuffableBeltSpeed.DiscreteSpeed beltSpeed,
        BuffableBeltDelay.DiscreteDuration cutDuration,
        ResearchSpeedId researchSpeed)
    {
        _speed = new BuffableBeltSpeed
        {
            BaseSpeed = beltSpeed,
            ResearchId = researchSpeed
        };

        _delay = new BuffableBeltDelay
        {
            BaseDuration = cutDuration,
            Research = researchSpeed
        };

        _speed.OnAfterDeserialize();
        _delay.OnAfterDeserialize();
    }
}