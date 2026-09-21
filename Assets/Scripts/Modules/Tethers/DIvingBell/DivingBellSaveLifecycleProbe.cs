using UnityEngine;

/// <summary>
/// Temporary Pass 13C diagnostic. Tracks one occupied diving bell through the
/// synchronous manual-save capture pipeline so we can identify the exact step
/// after which the live physical bell becomes disabled or destroyed.
///
/// Remove this class and its Mark calls after the culprit is identified.
/// </summary>
public sealed class DivingBellSaveLifecycleProbe
{
    private readonly DivingBellOccupancy bell;
    private readonly int instanceId;
    private readonly string bellName;
    private readonly string context;
    private bool failureAlreadyReported;

    private DivingBellSaveLifecycleProbe(
        DivingBellOccupancy bell,
        string context)
    {
        this.bell = bell;
        this.context = context;

        if (bell != null)
        {
            instanceId = bell.GetInstanceID();
            bellName = bell.name;
        }
        else
        {
            instanceId = 0;
            bellName = "NONE";
        }
    }

    public static DivingBellSaveLifecycleProbe Begin(string context)
    {
        DivingBellOccupancy[] bells =
            Object.FindObjectsByType<DivingBellOccupancy>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        DivingBellOccupancy occupied = null;

        if (bells != null)
        {
            for (int i = 0; i < bells.Length; i++)
            {
                DivingBellOccupancy candidate = bells[i];

                if (candidate != null && candidate.HasOccupants)
                {
                    occupied = candidate;
                    break;
                }
            }
        }

        if (occupied == null)
            return null;

        DivingBellSaveLifecycleProbe probe =
            new DivingBellSaveLifecycleProbe(
                occupied,
                context);

        probe.Mark("BEGIN");
        return probe;
    }

    public void Mark(string stage)
    {
        bool unityObjectAlive = bell != null;

        if (!unityObjectAlive)
        {
            if (!failureAlreadyReported)
            {
                failureAlreadyReported = true;

                Debug.LogError(
                    $"[DivingBellSaveLifecycleProbe] LOST bell between save stages | " +
                    $"context='{context}' stage='{stage}' " +
                    $"bell='{bellName}' instanceId={instanceId}. " +
                    "The previous probe stage is the last point where the bell was known alive.");
            }

            return;
        }

        GameObject go = bell.gameObject;
        Transform parent = bell.transform.parent;

        string state =
            $"alive=YES activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} " +
            $"enabled={bell.enabled} scene='{go.scene.name}' sceneLoaded={go.scene.isLoaded} " +
            $"parent='{(parent != null ? parent.name : "<root>")}' occupants={bell.OccupantCount}";

        if (!go.activeSelf || !go.activeInHierarchy || !bell.enabled)
        {
            if (!failureAlreadyReported)
            {
                failureAlreadyReported = true;
                Debug.LogError(
                    $"[DivingBellSaveLifecycleProbe] Bell became unavailable during save | " +
                    $"context='{context}' stage='{stage}' bell='{bellName}' instanceId={instanceId} | {state}",
                    bell);
            }

            return;
        }

        Debug.Log(
            $"[DivingBellSaveLifecycleProbe] context='{context}' stage='{stage}' " +
            $"bell='{bellName}' instanceId={instanceId} | {state}",
            bell);
    }
}
