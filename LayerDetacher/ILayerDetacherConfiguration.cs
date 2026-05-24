namespace ArcticRuins.LayerDetacher;

public interface ILayerDetacherConfiguration
{
    public BeltSpeed BeltSpeed { get; }
    public BeltDelay ProcessingDelay { get; }
}