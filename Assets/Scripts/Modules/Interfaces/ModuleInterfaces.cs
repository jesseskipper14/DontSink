public interface IModuleToggleable
{
    bool IsOn { get; }
    bool CanRun();
    bool Toggle();
}

public interface IInstalledModuleLifecycle
{
    void OnInstalled(Hardpoint owner);
    void OnRemoved();
}

public interface IPowerConsumerModule
{
    bool IsConsumingPower { get; }
    float PowerDemandPerSecond { get; }
}