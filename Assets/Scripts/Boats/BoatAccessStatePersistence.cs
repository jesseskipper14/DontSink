using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatAccessStatePersistence : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool warnForMissingIds = true;
    [SerializeField] private bool verboseLogging = false;

    public BoatAccessStateManifest CaptureManifest()
    {
        var manifest = new BoatAccessStateManifest();

        HatchRuntime[] hatches = GetComponentsInChildren<HatchRuntime>(true);
        for (int i = 0; i < hatches.Length; i++)
        {
            HatchRuntime hatch = hatches[i];
            if (hatch == null)
                continue;

            string id = hatch.AccessStateId;
            if (string.IsNullOrWhiteSpace(id))
            {
                if (warnForMissingIds)
                    LogWarning($"Skipping hatch '{hatch.name}' because HatchId is empty.");
                continue;
            }

            manifest.accessPoints.Add(new BoatAccessStateSnapshot
            {
                version = 1,
                accessId = id,
                accessType = "Hatch",
                isOpen = hatch.IsOpen
            });
        }

        DoorRuntime[] doors = GetComponentsInChildren<DoorRuntime>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            DoorRuntime door = doors[i];
            if (door == null)
                continue;

            string id = door.AccessStateId;
            if (string.IsNullOrWhiteSpace(id))
            {
                if (warnForMissingIds)
                    LogWarning($"Skipping door '{door.name}' because DoorId is empty.");
                continue;
            }

            manifest.accessPoints.Add(new BoatAccessStateSnapshot
            {
                version = 1,
                accessId = id,
                accessType = "Door",
                isOpen = door.IsOpen
            });
        }

        Log($"Captured access state. count={manifest.accessPoints.Count}");
        return manifest;
    }

    public void RestoreManifest(BoatAccessStateManifest manifest)
    {
        if (manifest == null || manifest.accessPoints == null)
        {
            Log("RestoreManifest skipped: manifest/list null.");
            return;
        }

        HatchRuntime[] hatches = GetComponentsInChildren<HatchRuntime>(true);
        DoorRuntime[] doors = GetComponentsInChildren<DoorRuntime>(true);

        for (int i = 0; i < manifest.accessPoints.Count; i++)
        {
            BoatAccessStateSnapshot snap = manifest.accessPoints[i];
            if (snap == null || string.IsNullOrWhiteSpace(snap.accessId))
                continue;

            bool restored = false;

            // Prefer type hint if available, but still fall back because future-us
            // will absolutely rename something and pretend it was archaeology.
            if (snap.accessType == "Hatch" || string.IsNullOrWhiteSpace(snap.accessType))
                restored = TryRestoreHatch(hatches, snap.accessId, snap.isOpen);

            if (!restored && (snap.accessType == "Door" || string.IsNullOrWhiteSpace(snap.accessType)))
                restored = TryRestoreDoor(doors, snap.accessId, snap.isOpen);

            if (!restored)
            {
                // Fallback cross-type lookup.
                restored = TryRestoreHatch(hatches, snap.accessId, snap.isOpen) ||
                           TryRestoreDoor(doors, snap.accessId, snap.isOpen);
            }

            if (!restored)
                LogWarning($"No hatch/door found for saved accessId='{snap.accessId}'.");
        }
    }

    private bool TryRestoreHatch(HatchRuntime[] hatches, string id, bool isOpen)
    {
        if (hatches == null)
            return false;

        for (int i = 0; i < hatches.Length; i++)
        {
            HatchRuntime hatch = hatches[i];
            if (hatch == null)
                continue;

            if (hatch.AccessStateId != id)
                continue;

            hatch.RestoreOpenState(isOpen);
            Log($"Restored hatch id='{id}' isOpen={isOpen}");
            return true;
        }

        return false;
    }

    private bool TryRestoreDoor(DoorRuntime[] doors, string id, bool isOpen)
    {
        if (doors == null)
            return false;

        for (int i = 0; i < doors.Length; i++)
        {
            DoorRuntime door = doors[i];
            if (door == null)
                continue;

            if (door.AccessStateId != id)
                continue;

            door.RestoreOpenState(isOpen);
            Log($"Restored door id='{id}' isOpen={isOpen}");
            return true;
        }

        return false;
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatAccessStatePersistence:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatAccessStatePersistence:{name}] {msg}", this);
    }
}