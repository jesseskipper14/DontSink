using System;
using UnityEngine;

[Serializable]
public struct WorldMapEventInstance
{
    public WorldMapEventDefinition def;
    public int sourceNodeId;
    public int seed;

    // Generic lifecycle
    public float elapsedHours;
    public float durationHours;

    // State flags
    public bool isResolved;

    // Event-specific state blob (keeps base generic)
    public string stateJson; // cheap + moddable; later replace with typed state if you want

    public void Tick(float dtHours)
    {
        elapsedHours += dtHours;
    }

    public float RemainingHours => Mathf.Max(0f, durationHours - elapsedHours);
}
