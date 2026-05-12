using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatCompartmentStatePersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;

    [Header("Restore")]
    [Tooltip("If true, missing/empty compartment IDs are reported during capture/restore.")]
    [SerializeField] private bool warnForMissingIds = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private void Awake()
    {
        if (boat == null)
            boat = GetComponent<Boat>();
    }

    public BoatCompartmentStateManifest CaptureManifest()
    {
        var manifest = new BoatCompartmentStateManifest();

        Compartment[] compartments = GetComponentsInChildren<Compartment>(true);

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];
            if (c == null)
                continue;

            if (string.IsNullOrWhiteSpace(c.CompartmentId))
            {
                if (warnForMissingIds)
                    LogWarning($"Skipping compartment '{c.name}' because CompartmentId is empty.");
                continue;
            }

            manifest.compartments.Add(new BoatCompartmentStateSnapshot
            {
                version = 1,
                compartmentId = c.CompartmentId,
                waterArea = c.WaterArea,
                maxWaterAreaAtCapture = c.MaxWaterArea,
                airIntegrity = c.airIntegrity
            });
        }

        Log($"Captured compartment state. count={manifest.compartments.Count}");
        return manifest;
    }

    public void RestoreManifest(BoatCompartmentStateManifest manifest)
    {
        if (manifest == null || manifest.compartments == null)
        {
            Log("RestoreManifest skipped: manifest/list null.");
            return;
        }

        Compartment[] compartments = GetComponentsInChildren<Compartment>(true);

        for (int i = 0; i < manifest.compartments.Count; i++)
        {
            BoatCompartmentStateSnapshot snap = manifest.compartments[i];
            if (snap == null || string.IsNullOrWhiteSpace(snap.compartmentId))
                continue;

            Compartment c = FindCompartmentById(compartments, snap.compartmentId);
            if (c == null)
            {
                LogWarning($"No compartment found for saved id='{snap.compartmentId}'. Skipping.");
                continue;
            }

            c.RestorePersistentFluidState(snap.waterArea, snap.airIntegrity);

            Log(
                $"Restored compartment '{snap.compartmentId}' " +
                $"water={snap.waterArea:F3}/{c.MaxWaterArea:F3} air={snap.airIntegrity:F2}");
        }
    }

    private static Compartment FindCompartmentById(Compartment[] compartments, string compartmentId)
    {
        if (compartments == null || string.IsNullOrWhiteSpace(compartmentId))
            return null;

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];
            if (c != null && c.CompartmentId == compartmentId)
                return c;
        }

        return null;
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatCompartmentStatePersistence:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatCompartmentStatePersistence:{name}] {msg}", this);
    }
}