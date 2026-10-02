using System;
using UnityEngine;

/// <summary>Item-owned consequential state. Operator/UI state is deliberately absent.</summary>
[Serializable]
public sealed class ChartingInstrumentState
{
    public int version = 1;
    public string boatInstanceId;
    public int revision;
    public bool invalidated;
    public CelestialObservation pendingObservation;

    public bool HasPendingObservation => pendingObservation != null && !string.IsNullOrWhiteSpace(pendingObservation.observationId);
    public bool HasState => !string.IsNullOrWhiteSpace(boatInstanceId) || revision != 0 || invalidated || HasPendingObservation;

    public ChartingInstrumentState Copy() => new ChartingInstrumentState
    {
        version = version,
        boatInstanceId = boatInstanceId,
        revision = revision,
        invalidated = invalidated,
        // Unity inline serialization can materialize a default class for a null value.
        // Explicitly normalize that empty payload instead of inventing a resumable observation.
        pendingObservation = HasPendingObservation
            ? JsonUtility.FromJson<CelestialObservation>(JsonUtility.ToJson(pendingObservation)) : null
    };
}
