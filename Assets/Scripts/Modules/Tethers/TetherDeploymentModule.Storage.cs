using UnityEngine;

// Behavior-preserving partial-class extraction.
// Responsibility: Storage.
public sealed partial class TetherDeploymentModule
{
    public bool TryGetStoredPayload(out ItemInstance item)
    {
        item = null;

        CacheRefs();

        if (!TryGetPayloadSlot(out InventorySlot slot))
            return false;

        if (slot.IsEmpty || slot.Instance == null)
            return false;

        item = slot.Instance;
        return true;
    }
    private bool TryGetPayloadSlot(out InventorySlot slot)
    {
        slot = null;

        CacheRefs();

        if (storageModule == null)
            return false;

        storageModule.EnsureContainer();

        ItemContainerState container =
            storageModule.ContainerState;

        if (container == null)
            return false;

        int index = PayloadSlotIndex;

        if (index < 0 || index >= container.SlotCount)
            return false;

        slot = container.GetSlot(index);
        return slot != null;
    }
    private void BindContainer()
    {
        CacheRefs();

        if (storageModule == null)
            return;

        storageModule.EnsureContainer();

        ItemContainerState container =
            storageModule.ContainerState;

        if (ReferenceEquals(
                _subscribedContainer,
                container))
        {
            return;
        }

        UnbindContainer();

        _subscribedContainer =
            container;

        if (_subscribedContainer != null)
            _subscribedContainer.Changed += HandleContainerChanged;
    }
    private void UnbindContainer()
    {
        if (_subscribedContainer != null)
            _subscribedContainer.Changed -= HandleContainerChanged;

        _subscribedContainer = null;
    }
    private void HandleContainerChanged()
    {
        if (!_syncingStoredPhysicalPayload)
            SyncStoredPhysicalPayload();

        RefreshStoredPayloadVisual();
        RefreshDeploymentState();
        PayloadChanged?.Invoke(this);
    }
}
