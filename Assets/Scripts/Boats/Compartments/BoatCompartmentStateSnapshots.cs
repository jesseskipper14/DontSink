using System.Collections.Generic;

[System.Serializable]
public sealed class BoatCompartmentStateSnapshot
{
    public int version = 1;

    public string compartmentId;

    public float waterArea;
    public float maxWaterAreaAtCapture;

    public float airIntegrity;
}

[System.Serializable]
public sealed class BoatCompartmentStateManifest
{
    public int version = 1;
    public List<BoatCompartmentStateSnapshot> compartments = new();
}