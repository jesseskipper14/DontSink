public interface IPowerConsumerModule
{
    bool IsConsumingPower { get; }
    float PowerDemandPerSecond { get; }
}