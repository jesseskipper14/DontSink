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
}

[System.Serializable]
public sealed class BoatModuleStateManifest
{
    public int version = 1;
    public List<BoatModuleStateSnapshot> modules = new();
}

[System.Serializable]
public sealed class BoatPowerSnapshot
{
    public int version = 1;

    public float currentPower;
    public float maxPower;
}