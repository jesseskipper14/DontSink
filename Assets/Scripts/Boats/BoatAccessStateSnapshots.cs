using System.Collections.Generic;

[System.Serializable]
public sealed class BoatAccessStateSnapshot
{
    public int version = 1;

    public string accessId;
    public string accessType; // "Hatch" or "Door", debug/migration only

    public bool isOpen;
}

[System.Serializable]
public sealed class BoatAccessStateManifest
{
    public int version = 1;
    public List<BoatAccessStateSnapshot> accessPoints = new();
}