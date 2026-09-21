using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatTetherStatePersistence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private void Awake()
    {
        CacheRefs();
    }

    public BoatTetherStateManifest CaptureManifest()
    {
        CacheRefs();

        BoatTetherStateManifest manifest =
            new BoatTetherStateManifest();

        if (boat == null)
        {
            LogWarning("CaptureManifest skipped: Boat is null.");
            return manifest;
        }

        Hardpoint[] hardpoints =
            boat.GetComponentsInChildren<Hardpoint>(
                true);

        for (int i = 0;
             i < hardpoints.Length;
             i++)
        {
            Hardpoint hardpoint =
                hardpoints[i];

            if (hardpoint == null ||
                !hardpoint.HasInstalledModule ||
                hardpoint.InstalledModule == null ||
                string.IsNullOrWhiteSpace(
                    hardpoint.HardpointId))
            {
                continue;
            }

            CaptureWinch(
                manifest,
                hardpoint);

            CaptureDeployment(
                manifest,
                hardpoint);
        }

        Log(
            $"Captured tether state | winches={manifest.winches.Count} " +
            $"deployments={manifest.deployments.Count}");

        return manifest;
    }

    public void RestoreManifest(
        BoatTetherStateManifest manifest)
    {
        CacheRefs();

        if (manifest == null)
        {
            Log("RestoreManifest skipped: manifest is null.");
            return;
        }

        if (boat == null)
        {
            LogWarning("RestoreManifest skipped: Boat is null.");
            return;
        }

        Hardpoint[] hardpoints =
            boat.GetComponentsInChildren<Hardpoint>(
                true);

        RestoreWinches(
            manifest,
            hardpoints);

        RestoreDeployments(
            manifest,
            hardpoints);

        Log(
            $"Restored tether state | " +
            $"winches={(manifest.winches != null ? manifest.winches.Count : 0)} " +
            $"deployments={(manifest.deployments != null ? manifest.deployments.Count : 0)}");
    }

    private void CaptureWinch(
        BoatTetherStateManifest manifest,
        Hardpoint hardpoint)
    {
        WinchModule winch =
            hardpoint.InstalledModule
                .GetComponent<WinchModule>();

        if (winch == null)
            return;

        TetherWinchLink link =
            hardpoint.GetComponent<TetherWinchLink>();

        manifest.winches.Add(
            new BoatWinchStateSnapshot
            {
                version =
                    1,

                hardpointId =
                    hardpoint.HardpointId,

                linkedDeploymentHardpointId =
                    link != null &&
                    link.LinkedDeploymentHardpoint != null
                        ? link.LinkedDeploymentHardpoint.HardpointId
                        : null,

                lineContainer =
                    winch.CaptureLineContainerSnapshot()
            });
    }

    private void CaptureDeployment(
        BoatTetherStateManifest manifest,
        Hardpoint hardpoint)
    {
        TetherDeploymentModule deployment =
            hardpoint.InstalledModule
                .GetComponent<TetherDeploymentModule>();

        if (deployment == null ||
            !deployment.HasDeployedPayload ||
            deployment.DeployedWorldItem == null)
        {
            return;
        }

        ItemInstance item =
            deployment.ReservedPayloadItem;

        if (item == null)
        {
            LogWarning(
                $"Skipping deployed tether on hardpoint '{hardpoint.HardpointId}': " +
                "reserved payload ItemInstance is null.");

            return;
        }

        ItemInstanceSnapshot itemSnapshot =
            item.ToSnapshot();

        if (itemSnapshot == null)
        {
            LogWarning(
                $"Skipping deployed tether on hardpoint '{hardpoint.HardpointId}': " +
                "payload ItemInstance could not produce a snapshot.");

            return;
        }

        WorldItem worldItem =
            deployment.DeployedWorldItem;

        Vector3 localPosition =
            boat.transform.InverseTransformPoint(
                worldItem.transform.position);

        float localRotationZ =
            Mathf.DeltaAngle(
                boat.transform.eulerAngles.z,
                worldItem.transform.eulerAngles.z);

        float deployedLength =
            deployment.TetherConstraint != null
                ? deployment.TetherConstraint.DeployedLength
                : Vector2.Distance(
                    deployment.TetherExitPoint.position,
                    worldItem.transform.position);

        DivingBellAirVolume bellAir =
            worldItem.GetComponent<DivingBellAirVolume>() ??
            worldItem.GetComponentInChildren<DivingBellAirVolume>(true);

        bool hasDivingBellAirState =
            bellAir != null;

        manifest.deployments.Add(
            new BoatTetherDeploymentSnapshot
            {
                version =
                    2,

                deploymentHardpointId =
                    hardpoint.HardpointId,

                payloadItem =
                    itemSnapshot,

                localPosition =
                    localPosition,

                localRotationZ =
                    localRotationZ,

                deployedLengthMeters =
                    Mathf.Max(
                        0.05f,
                        deployedLength),

                savedDeploymentState =
                    deployment.DeploymentState,

                hasDivingBellAirState =
                    hasDivingBellAirState,

                divingBellTrappedAirMoles01 =
                    hasDivingBellAirState
                        ? bellAir.TrappedAirMoles01
                        : 1f,

                divingBellAirQuality01 =
                    hasDivingBellAirState
                        ? bellAir.AirQuality01
                        : 1f
            });
    }

    private void RestoreWinches(
        BoatTetherStateManifest manifest,
        Hardpoint[] hardpoints)
    {
        if (manifest.winches == null)
            return;

        for (int i = 0;
             i < manifest.winches.Count;
             i++)
        {
            BoatWinchStateSnapshot snapshot =
                manifest.winches[i];

            if (snapshot == null ||
                string.IsNullOrWhiteSpace(
                    snapshot.hardpointId))
            {
                continue;
            }

            Hardpoint hardpoint =
                FindHardpointById(
                    hardpoints,
                    snapshot.hardpointId);

            if (hardpoint == null ||
                !hardpoint.HasInstalledModule ||
                hardpoint.InstalledModule == null)
            {
                LogWarning(
                    $"Could not restore winch state: hardpoint '{snapshot.hardpointId}' " +
                    "is missing or has no installed module.");

                continue;
            }

            WinchModule winch =
                hardpoint.InstalledModule
                    .GetComponent<WinchModule>();

            if (winch == null)
            {
                LogWarning(
                    $"Could not restore winch state: installed module on " +
                    $"'{snapshot.hardpointId}' has no WinchModule.");

                continue;
            }

            if (itemCatalog == null)
            {
                LogWarning(
                    $"Cannot restore winch line inventory on '{snapshot.hardpointId}': " +
                    "ItemDefinitionCatalog is null.");
            }
            else
            {
                winch.RestoreLineContainerSnapshot(
                    snapshot.lineContainer,
                    itemCatalog);
            }

            RestoreWinchLink(
                hardpoint,
                snapshot,
                hardpoints);
        }
    }

    private void RestoreWinchLink(
        Hardpoint winchHardpoint,
        BoatWinchStateSnapshot snapshot,
        Hardpoint[] hardpoints)
    {
        if (winchHardpoint == null ||
            snapshot == null)
        {
            return;
        }

        TetherWinchLink link =
            winchHardpoint.GetComponent<TetherWinchLink>();

        if (link == null)
            link =
                winchHardpoint.gameObject
                    .AddComponent<TetherWinchLink>();

        if (string.IsNullOrWhiteSpace(
                snapshot.linkedDeploymentHardpointId))
        {
            link.Unlink();
            return;
        }

        Hardpoint deploymentHardpoint =
            FindHardpointById(
                hardpoints,
                snapshot.linkedDeploymentHardpointId);

        if (deploymentHardpoint == null)
        {
            LogWarning(
                $"Could not restore tether link: winch '{snapshot.hardpointId}' " +
                $"expects deployment hardpoint '{snapshot.linkedDeploymentHardpointId}'.");

            link.Unlink();
            return;
        }

        if (!link.TryLink(
                deploymentHardpoint))
        {
            LogWarning(
                $"TetherWinchLink rejected saved link " +
                $"'{snapshot.hardpointId}' -> '{snapshot.linkedDeploymentHardpointId}'.");
        }
    }

    private void RestoreDeployments(
        BoatTetherStateManifest manifest,
        Hardpoint[] hardpoints)
    {
        if (manifest.deployments == null ||
            manifest.deployments.Count == 0)
        {
            return;
        }

        if (itemCatalog == null)
        {
            LogWarning(
                "Cannot restore deployed tether payloads: ItemDefinitionCatalog is null.");

            return;
        }

        for (int i = 0;
             i < manifest.deployments.Count;
             i++)
        {
            BoatTetherDeploymentSnapshot snapshot =
                manifest.deployments[i];

            if (snapshot == null ||
                snapshot.payloadItem == null ||
                string.IsNullOrWhiteSpace(
                    snapshot.deploymentHardpointId))
            {
                continue;
            }

            Hardpoint hardpoint =
                FindHardpointById(
                    hardpoints,
                    snapshot.deploymentHardpointId);

            if (hardpoint == null ||
                !hardpoint.HasInstalledModule ||
                hardpoint.InstalledModule == null)
            {
                LogWarning(
                    $"Could not restore tether deployment: hardpoint " +
                    $"'{snapshot.deploymentHardpointId}' is missing or empty.");

                continue;
            }

            TetherDeploymentModule deployment =
                hardpoint.InstalledModule
                    .GetComponent<TetherDeploymentModule>();

            if (deployment == null)
            {
                LogWarning(
                    $"Could not restore tether deployment: installed module on " +
                    $"'{snapshot.deploymentHardpointId}' has no TetherDeploymentModule.");

                continue;
            }

            ItemInstance item =
                ItemInstance.FromSnapshot(
                    snapshot.payloadItem,
                    itemCatalog);

            if (item == null)
            {
                LogWarning(
                    $"Could not restore tether payload itemId='{snapshot.payloadItem.itemId}' " +
                    $"on '{snapshot.deploymentHardpointId}'.");

                continue;
            }

            Vector3 worldPosition =
                boat.transform.TransformPoint(
                    snapshot.localPosition);

            Quaternion worldRotation =
                boat.transform.rotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    snapshot.localRotationZ);

            if (!deployment.TryRestoreDeployedPayload(
                    item,
                    worldPosition,
                    worldRotation,
                    snapshot.deployedLengthMeters,
                    out TetherPayload restoredPayload) ||
                restoredPayload == null)
            {
                LogWarning(
                    $"TetherDeploymentModule failed to restore deployed payload on " +
                    $"'{snapshot.deploymentHardpointId}'.");

                continue;
            }

            RestoreDivingBellAirState(
                snapshot,
                restoredPayload);

            RestoreLinkedWinchRuntime(
                hardpoints,
                snapshot.deploymentHardpointId,
                snapshot.deployedLengthMeters);
        }
    }

    private void RestoreDivingBellAirState(
        BoatTetherDeploymentSnapshot snapshot,
        TetherPayload restoredPayload)
    {
        if (snapshot == null ||
            restoredPayload == null ||
            !snapshot.hasDivingBellAirState)
        {
            return;
        }

        DivingBellAirVolume bellAir =
            restoredPayload.GetComponent<DivingBellAirVolume>() ??
            restoredPayload.GetComponentInChildren<DivingBellAirVolume>(true);

        if (bellAir == null)
        {
            LogWarning(
                $"Saved deployment '{snapshot.deploymentHardpointId}' carries diving-bell air state, " +
                "but the restored payload has no DivingBellAirVolume. Leaving prefab/runtime defaults intact.");

            return;
        }

        bellAir.RestoreRuntimeState(
            snapshot.divingBellTrappedAirMoles01,
            snapshot.divingBellAirQuality01);

        Log(
            $"Restored diving-bell air state on '{snapshot.deploymentHardpointId}' | " +
            $"moles={snapshot.divingBellTrappedAirMoles01:F3} " +
            $"quality={snapshot.divingBellAirQuality01:F3}");
    }

    private void RestoreLinkedWinchRuntime(
        Hardpoint[] hardpoints,
        string deploymentHardpointId,
        float deployedLengthMeters)
    {
        if (hardpoints == null ||
            string.IsNullOrWhiteSpace(
                deploymentHardpointId))
        {
            return;
        }

        for (int i = 0;
             i < hardpoints.Length;
             i++)
        {
            Hardpoint candidate =
                hardpoints[i];

            if (candidate == null ||
                !candidate.HasInstalledModule ||
                candidate.InstalledModule == null)
            {
                continue;
            }

            WinchModule winch =
                candidate.InstalledModule
                    .GetComponent<WinchModule>();

            if (winch == null)
                continue;

            TetherWinchLink link =
                candidate.GetComponent<TetherWinchLink>();

            if (link == null ||
                link.LinkedDeploymentHardpoint == null ||
                !string.Equals(
                    link.LinkedDeploymentHardpoint.HardpointId,
                    deploymentHardpointId,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            winch.RestorePersistentTetherRuntime(
                deployedLengthMeters);

            return;
        }
    }

    private void CacheRefs()
    {
        if (boat == null)
            boat =
                GetComponent<Boat>();

        if (itemCatalog != null)
            return;

        BoatModuleStatePersistence modulePersistence =
            GetComponent<BoatModuleStatePersistence>();

        if (modulePersistence != null)
            itemCatalog =
                modulePersistence.ItemCatalog;
    }

    private static Hardpoint FindHardpointById(
        Hardpoint[] hardpoints,
        string hardpointId)
    {
        if (hardpoints == null ||
            string.IsNullOrWhiteSpace(
                hardpointId))
        {
            return null;
        }

        for (int i = 0;
             i < hardpoints.Length;
             i++)
        {
            Hardpoint hardpoint =
                hardpoints[i];

            if (hardpoint != null &&
                string.Equals(
                    hardpoint.HardpointId,
                    hardpointId,
                    System.StringComparison.Ordinal))
            {
                return hardpoint;
            }
        }

        return null;
    }

    private void Log(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[BoatTetherStatePersistence:{name}] {message}",
            this);
    }

    private void LogWarning(
        string message)
    {
        Debug.LogWarning(
            $"[BoatTetherStatePersistence:{name}] {message}",
            this);
    }
}
