using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatCompartmentStatePersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;

    [Header("Validation")]
    [SerializeField] private bool validateIdsOnStart = true;

    [Tooltip("If true, missing/empty compartment IDs are reported during capture/restore/start validation.")]
    [SerializeField] private bool warnForMissingIds = true;

    [Tooltip("If true, duplicate compartment IDs are reported and only the first is used.")]
    [SerializeField] private bool warnForDuplicateIds = true;

    [Tooltip("If true, warnings are printed even when verbose logging is off.")]
    [SerializeField] private bool warningsIgnoreVerboseLogging = true;

    [Header("Restore")]
    [Tooltip("If the compartment max area changed since capture, restore by saved fill fraction instead of raw water area.")]
    [SerializeField] private bool restoreByFractionWhenMaxAreaChanged = true;

    [SerializeField, Min(0f)] private float maxAreaMismatchTolerance = 0.001f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private void Awake()
    {
        ResolveRefs();
    }

    private void Start()
    {
        if (validateIdsOnStart)
            ValidateCompartmentIds("Start");
    }

    private void Reset()
    {
        ResolveRefs();
    }

    private void OnValidate()
    {
        maxAreaMismatchTolerance = Mathf.Max(0f, maxAreaMismatchTolerance);
        ResolveRefs();
    }

    public BoatCompartmentStateManifest CaptureManifest()
    {
        ResolveRefs();

        var manifest = new BoatCompartmentStateManifest();

        Compartment[] compartments = GetComponentsInChildren<Compartment>(true);

        HashSet<string> usedIds = new HashSet<string>();

        int skippedMissingId = 0;
        int skippedDuplicateId = 0;

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];
            if (c == null)
                continue;

            string id = c.CompartmentId;

            if (string.IsNullOrWhiteSpace(id))
            {
                skippedMissingId++;

                if (warnForMissingIds)
                {
                    Warn(
                        $"Skipping compartment '{c.name}' during save capture because CompartmentId is empty. " +
                        "This compartment water will NOT save. Generate a stable compartment ID.");
                }

                continue;
            }

            id = id.Trim();

            if (!usedIds.Add(id))
            {
                skippedDuplicateId++;

                if (warnForDuplicateIds)
                {
                    Warn(
                        $"Skipping duplicate compartment id='{id}' on '{c.name}' during save capture. " +
                        "Duplicate IDs restore ambiguously. Give every compartment a unique stable ID.");
                }

                continue;
            }

            c.RecomputeWaterSurface();

            manifest.compartments.Add(new BoatCompartmentStateSnapshot
            {
                version = 1,
                compartmentId = id,
                waterArea = c.WaterArea,
                maxWaterAreaAtCapture = c.MaxWaterArea,
                airIntegrity = c.airIntegrity
            });
        }

        Log(
            $"Captured compartment state. " +
            $"found={compartments.Length}, saved={manifest.compartments.Count}, " +
            $"skippedMissingId={skippedMissingId}, skippedDuplicateId={skippedDuplicateId}");

        if (compartments.Length > 0 && manifest.compartments.Count == 0)
        {
            Warn(
                $"Captured ZERO compartment states even though {compartments.Length} compartments exist. " +
                "Compartment water will not restore. Most likely every compartment is missing a stable ID.");
        }

        return manifest;
    }

    public void RestoreManifest(BoatCompartmentStateManifest manifest)
    {
        ResolveRefs();

        if (manifest == null || manifest.compartments == null)
        {
            Log("RestoreManifest skipped: manifest/list null.");
            return;
        }

        Compartment[] compartments = GetComponentsInChildren<Compartment>(true);
        Dictionary<string, Compartment> byId = BuildCompartmentLookup(compartments);

        int restored = 0;
        int missingSavedIds = 0;

        for (int i = 0; i < manifest.compartments.Count; i++)
        {
            BoatCompartmentStateSnapshot snap = manifest.compartments[i];

            if (snap == null || string.IsNullOrWhiteSpace(snap.compartmentId))
                continue;

            string id = snap.compartmentId.Trim();

            if (!byId.TryGetValue(id, out Compartment c) || c == null)
            {
                missingSavedIds++;

                Warn(
                    $"Saved compartment id='{id}' was not found on loaded boat. " +
                    "That compartment water state will not restore.");

                continue;
            }

            float waterAreaToRestore = ResolveWaterAreaForRestore(c, snap);

            c.RestorePersistentFluidState(waterAreaToRestore, snap.airIntegrity);
            c.RecomputeWaterSurface();

            restored++;

            Log(
                $"Restored compartment '{id}' " +
                $"water={waterAreaToRestore:F3}/{c.MaxWaterArea:F3} air={snap.airIntegrity:F2}");
        }

        Log(
            $"RestoreManifest complete. " +
            $"saved={manifest.compartments.Count}, restored={restored}, " +
            $"missingSavedIds={missingSavedIds}, sceneCompartments={compartments.Length}");
    }

    [ContextMenu("DEBUG Validate Compartment IDs")]
    public void DebugValidateCompartmentIds()
    {
        ValidateCompartmentIds("ContextMenu");
    }

    private void ValidateCompartmentIds(string reason)
    {
        Compartment[] compartments = GetComponentsInChildren<Compartment>(true);

        HashSet<string> used = new HashSet<string>();

        int missing = 0;
        int duplicate = 0;

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];
            if (c == null)
                continue;

            if (string.IsNullOrWhiteSpace(c.CompartmentId))
            {
                missing++;

                if (warnForMissingIds)
                {
                    Warn(
                        $"[{reason}] Compartment '{c.name}' has no stable CompartmentId. " +
                        "Its water/air state will not save or restore.");
                }

                continue;
            }

            string id = c.CompartmentId.Trim();

            if (!used.Add(id))
            {
                duplicate++;

                if (warnForDuplicateIds)
                {
                    Warn(
                        $"[{reason}] Duplicate CompartmentId='{id}' found on '{c.name}'. " +
                        "Duplicate compartment IDs make water restore ambiguous.");
                }
            }
        }

        Log(
            $"ValidateCompartmentIds reason='{reason}' " +
            $"count={compartments.Length}, missing={missing}, duplicate={duplicate}");
    }

    private Dictionary<string, Compartment> BuildCompartmentLookup(Compartment[] compartments)
    {
        Dictionary<string, Compartment> result = new Dictionary<string, Compartment>();

        if (compartments == null)
            return result;

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];
            if (c == null)
                continue;

            if (string.IsNullOrWhiteSpace(c.CompartmentId))
            {
                if (warnForMissingIds)
                {
                    Warn(
                        $"Restore lookup skipped compartment '{c.name}' because it has no stable CompartmentId.");
                }

                continue;
            }

            string id = c.CompartmentId.Trim();

            if (result.ContainsKey(id))
            {
                if (warnForDuplicateIds)
                {
                    Warn(
                        $"Restore lookup found duplicate CompartmentId='{id}'. " +
                        $"Existing='{result[id].name}', duplicate='{c.name}'. Keeping first.");
                }

                continue;
            }

            result.Add(id, c);
        }

        return result;
    }

    private float ResolveWaterAreaForRestore(
        Compartment c,
        BoatCompartmentStateSnapshot snap)
    {
        if (c == null || snap == null)
            return 0f;

        float savedWater = Mathf.Max(0f, snap.waterArea);
        float currentMax = Mathf.Max(0f, c.MaxWaterArea);
        float savedMax = Mathf.Max(0f, snap.maxWaterAreaAtCapture);

        if (!restoreByFractionWhenMaxAreaChanged)
            return Mathf.Clamp(savedWater, 0f, currentMax);

        if (savedMax <= 0.0001f)
            return Mathf.Clamp(savedWater, 0f, currentMax);

        if (Mathf.Abs(savedMax - currentMax) <= maxAreaMismatchTolerance)
            return Mathf.Clamp(savedWater, 0f, currentMax);

        float fraction = Mathf.Clamp01(savedWater / savedMax);
        float scaled = currentMax * fraction;

        Warn(
            $"Compartment '{c.name}' max water area changed since save. " +
            $"savedMax={savedMax:F3}, currentMax={currentMax:F3}. Restoring by fraction={fraction:P0}.");

        return Mathf.Clamp(scaled, 0f, currentMax);
    }

    private void ResolveRefs()
    {
        if (boat == null)
            boat = GetComponent<Boat>();

        if (boat == null)
            boat = GetComponentInParent<Boat>();
    }

    [ContextMenu("DEBUG Log Capture Preview")]
    private void DebugLogCapturePreview()
    {
        BoatCompartmentStateManifest manifest = CaptureManifest();

        if (manifest == null || manifest.compartments == null)
        {
            Debug.LogWarning("[BoatCompartmentStatePersistence] Capture preview produced NULL manifest/list.", this);
            return;
        }

        Debug.Log(
            $"[BoatCompartmentStatePersistence:{name}] Capture Preview count={manifest.compartments.Count}",
            this);

        for (int i = 0; i < manifest.compartments.Count; i++)
        {
            BoatCompartmentStateSnapshot s = manifest.compartments[i];

            Debug.Log(
                $"  [{i}] id='{s.compartmentId}' water={s.waterArea:F3}/{s.maxWaterAreaAtCapture:F3} air={s.airIntegrity:F2}",
                this);
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatCompartmentStatePersistence:{name}] {msg}", this);
    }

    private void Warn(string msg)
    {
        if (!warningsIgnoreVerboseLogging && !verboseLogging)
            return;

        Debug.LogWarning($"[BoatCompartmentStatePersistence:{name}] {msg}", this);
    }
}