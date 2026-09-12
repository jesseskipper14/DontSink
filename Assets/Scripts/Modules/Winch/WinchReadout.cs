using UnityEngine;

/// <summary>
/// Read-only snapshot consumed by WinchCartridge.
/// Runtime systems remain authoritative; this only gathers their current state.
/// </summary>
public struct WinchReadoutSnapshot
{
    public bool HasWinch;
    public bool HasLink;
    public bool HasDeployment;

    public WinchCommand Command;
    public TetherDeploymentState DeploymentState;

    public bool HasStoredPayload;
    public bool HasDeployedPayload;
    public string PayloadName;

    public float AvailableLineMeters;
    public float DeployedLengthMeters;
    public float CurrentDistanceMeters;
    public float SlackMeters;

    public float WorkingLoadNewtons;
    public float BreakingLoadNewtons;

    public int LineSlotCount;
    public int LoadedLineSlotCount;
    public string LoadedLineSummary;
}

/// <summary>
/// Collects live WinchModule / linked deployment state for the cartridge.
/// The cartridge does not need to rummage through the boat hierarchy itself.
/// </summary>
public sealed class WinchReadoutSource
{
    private readonly Hardpoint _hardpoint;
    private readonly WinchModule _winch;

    public WinchReadoutSource(
        Hardpoint hardpoint,
        WinchModule winch)
    {
        _hardpoint = hardpoint;
        _winch = winch;
    }

    public WinchReadoutSnapshot Capture()
    {
        WinchReadoutSnapshot snapshot =
            new WinchReadoutSnapshot
            {
                HasWinch = _winch != null
            };

        if (_winch == null)
            return snapshot;

        snapshot.Command =
            _winch.Command;

        snapshot.AvailableLineMeters =
            _winch.AvailableLineMeters;

        snapshot.DeployedLengthMeters =
            _winch.DeployedLength;

        snapshot.CurrentDistanceMeters =
            _winch.CurrentDistance;

        snapshot.WorkingLoadNewtons =
            _winch.WorkingLoadNewtons;

        snapshot.BreakingLoadNewtons =
            _winch.BreakingLoadNewtons;

        CaptureLineSummary(
            ref snapshot);

        TetherWinchLink link =
            _hardpoint != null
                ? _hardpoint.GetComponent<TetherWinchLink>()
                : null;

        snapshot.HasLink =
            link != null &&
            link.HasLinkedDeployment;

        if (link == null ||
            !link.TryGetDeploymentModule(
                out TetherDeploymentModule deployment) ||
            deployment == null)
        {
            snapshot.DeploymentState =
                TetherDeploymentState.Stowed;

            return snapshot;
        }

        snapshot.HasDeployment =
            true;

        snapshot.DeploymentState =
            deployment.DeploymentState;

        snapshot.HasStoredPayload =
            deployment.HasStoredPayload;

        snapshot.HasDeployedPayload =
            deployment.HasDeployedPayload;

        TetherConstraint2D constraint =
            deployment.TetherConstraint;

        if (constraint != null)
        {
            snapshot.CurrentDistanceMeters =
                constraint.CurrentDistance;

            snapshot.SlackMeters =
                constraint.Slack;
        }

        ItemInstance payloadItem =
            deployment.HasDeployedPayload
                ? deployment.ReservedPayloadItem
                : deployment.StoredPayload;

        if (payloadItem != null &&
            payloadItem.Definition != null)
        {
            snapshot.PayloadName =
                payloadItem.Definition.DisplayName;
        }

        return snapshot;
    }

    private void CaptureLineSummary(
        ref WinchReadoutSnapshot snapshot)
    {
        if (_winch == null)
            return;

        snapshot.LineSlotCount =
            _winch.LineSlotCount;

        ItemContainerState container =
            _winch.LineContainer;

        if (container == null)
            return;

        int loaded =
            0;

        string summary =
            string.Empty;

        for (int i = 0;
             i < container.SlotCount;
             i++)
        {
            InventorySlot slot =
                container.GetSlot(i);

            if (slot == null ||
                slot.IsEmpty ||
                slot.Instance == null)
            {
                continue;
            }

            loaded++;

            ItemInstance item =
                slot.Instance;

            string itemName =
                item.Definition != null
                    ? item.Definition.DisplayName
                    : "Unknown Line";

            string entry =
                item.Quantity > 1
                    ? $"{itemName} x{item.Quantity}"
                    : itemName;

            if (!string.IsNullOrWhiteSpace(
                    summary))
            {
                summary += " | ";
            }

            summary += entry;
        }

        snapshot.LoadedLineSlotCount =
            loaded;

        snapshot.LoadedLineSummary =
            summary;
    }
}
