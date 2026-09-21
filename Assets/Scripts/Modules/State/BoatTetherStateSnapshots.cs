using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class BoatTetherStateManifest
{
    public int version = 2;

    public List<BoatWinchStateSnapshot> winches = new();
    public List<BoatTetherDeploymentSnapshot> deployments = new();
}

[System.Serializable]
public sealed class BoatWinchStateSnapshot
{
    public int version = 1;

    public string hardpointId;
    public string linkedDeploymentHardpointId;

    // Ordinary ItemInstance container snapshot for rope/chain loaded into this winch.
    public ItemContainerSnapshot lineContainer;
}

[System.Serializable]
public sealed class BoatTetherDeploymentSnapshot
{
    // v2 adds optional diving-bell trapped-air state.
    public int version = 2;

    public string deploymentHardpointId;

    [SerializeReference]
    public ItemInstanceSnapshot payloadItem;

    // Stored relative to the boat so the rig can be reconstructed after the boat
    // itself is spawned at a different scene-space X/Y.
    public Vector2 localPosition;
    public float localRotationZ;

    public float deployedLengthMeters;

    // Diagnostic only. Runtime physics re-evaluates bottom/holding state after restore.
    public TetherDeploymentState savedDeploymentState;

    // v2: optional runtime state for a deployed diving-bell payload.
    // CompressedAirVolume01 / WaterFill01 / waterline are deliberately NOT saved;
    // they are derived from trapped air + the payload's restored depth on the next physics step.
    public bool hasDivingBellAirState;

    [Range(0f, 1f)]
    public float divingBellTrappedAirMoles01 = 1f;

    [Range(0f, 1f)]
    public float divingBellAirQuality01 = 1f;
}
