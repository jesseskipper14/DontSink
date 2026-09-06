using System.Collections.Generic;

[System.Serializable]
public sealed class BoatModuleStateSnapshot
{
    public int version = 1;

    public string hardpointId;
    public string moduleId;

    public bool isOn;

    // Used by EngineModule / GeneratorModule.
    public ItemInstanceSnapshot fuelContainer;

    // Used by StorageModule lockers/racks.
    public ItemContainerSnapshot storageContainer;
}

[System.Serializable]
public sealed class BoatHelmLinkSnapshot
{
    public int version = 1;

    public string pilotStationId;
    public string helmHardpointId;

    // Zero-based order among PilotChair controllers on this Helm hardpoint.
    // This preserves which stations fit inside limited Helm connection capacity.
    public int controllerOrder;
}

[System.Serializable]
public sealed class BoatModuleStateManifest
{
    // v2 adds runtime Helm <-> Pilot Station wiring persistence.
    public int version = 2;

    public List<BoatModuleStateSnapshot> modules = new();
    public List<BoatHelmLinkSnapshot> helmLinks = new();
}

[System.Serializable]
public sealed class BoatPowerSnapshot
{
    public int version = 1;

    public float currentPower;
    public float maxPower;
}