using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime registry for external physical relationships attached to one
/// WorldItem. It participates in WorldItem's pickup transaction so attachments
/// can either veto pickup or detach only after acquisition succeeds.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldItemAttachmentRegistry :
    MonoBehaviour,
    IWorldItemPickupParticipant
{
    [Header("Runtime Debug")]
    [SerializeField] private int activeAttachmentCount;
    [SerializeField] private int blockingAttachmentCount;

    private readonly List<IWorldItemAttachment> _attachments =
        new List<IWorldItemAttachment>();

    private readonly List<IWorldItemAttachment> _detachScratch =
        new List<IWorldItemAttachment>();

    public int ActiveAttachmentCount => activeAttachmentCount;
    public int BlockingAttachmentCount => blockingAttachmentCount;

    public void Register(IWorldItemAttachment attachment)
    {
        if (!IsLive(attachment))
            return;

        PruneDeadEntries();

        if (!_attachments.Contains(attachment))
            _attachments.Add(attachment);

        RefreshDebugCounts();
    }

    public void Unregister(IWorldItemAttachment attachment)
    {
        if (attachment == null)
            return;

        while (_attachments.Remove(attachment))
        {
        }

        RefreshDebugCounts();
    }

    public bool AllowsWorldItemPickup(in InteractContext context)
    {
        PruneDeadEntries();

        for (int i = 0; i < _attachments.Count; i++)
        {
            IWorldItemAttachment attachment = _attachments[i];

            if (!IsLive(attachment) ||
                !attachment.IsWorldItemAttachmentActive)
            {
                continue;
            }

            if (attachment.PickupPolicy ==
                WorldItemAttachmentPickupPolicy.BlockPickup)
            {
                RefreshDebugCounts();
                return false;
            }
        }

        RefreshDebugCounts();
        return true;
    }

    public void OnWorldItemPickupCommitted()
    {
        PruneDeadEntries();
        _detachScratch.Clear();

        for (int i = 0; i < _attachments.Count; i++)
        {
            IWorldItemAttachment attachment = _attachments[i];

            if (!IsLive(attachment) ||
                !attachment.IsWorldItemAttachmentActive)
            {
                continue;
            }

            if (attachment.PickupPolicy ==
                WorldItemAttachmentPickupPolicy.DetachOnPickup)
            {
                _detachScratch.Add(attachment);
            }
        }

        // Work from a snapshot. DetachForWorldItemPickup normally unregisters
        // itself, so iterating the backing list directly would be the sort of
        // tiny mutation bug that spends an afternoon pretending to be physics.
        for (int i = 0; i < _detachScratch.Count; i++)
        {
            IWorldItemAttachment attachment = _detachScratch[i];

            if (IsLive(attachment) &&
                attachment.IsWorldItemAttachmentActive)
            {
                attachment.DetachForWorldItemPickup();
            }
        }

        _detachScratch.Clear();
        PruneDeadEntries();
        RefreshDebugCounts();
    }

    private void PruneDeadEntries()
    {
        for (int i = _attachments.Count - 1; i >= 0; i--)
        {
            if (!IsLive(_attachments[i]))
                _attachments.RemoveAt(i);
        }
    }

    private void RefreshDebugCounts()
    {
        int active = 0;
        int blocking = 0;

        for (int i = 0; i < _attachments.Count; i++)
        {
            IWorldItemAttachment attachment = _attachments[i];

            if (!IsLive(attachment) ||
                !attachment.IsWorldItemAttachmentActive)
            {
                continue;
            }

            active++;

            if (attachment.PickupPolicy ==
                WorldItemAttachmentPickupPolicy.BlockPickup)
            {
                blocking++;
            }
        }

        activeAttachmentCount = active;
        blockingAttachmentCount = blocking;
    }

    private static bool IsLive(IWorldItemAttachment attachment)
    {
        if (attachment == null)
            return false;

        if (attachment is Object unityObject &&
            unityObject == null)
        {
            return false;
        }

        return true;
    }
}
