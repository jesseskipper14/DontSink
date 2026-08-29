using System;
using UnityEngine;

[Serializable]
public sealed class ItemInstance
{
    [SerializeField] private string instanceId;
    [SerializeField] private ItemDefinition definition;
    [SerializeField] private int quantity = 1;
    [SerializeReference] private ItemContainerState containerState;
    [SerializeField] private int currentCharges;

    [NonSerialized] public Action Changed;

    [NonSerialized] private ItemContainerState subscribedContainerState;

    public string InstanceId => instanceId;
    public ItemDefinition Definition => definition;
    public int Quantity => Mathf.Max(1, quantity);

    public float UnitMass =>
        definition != null
            ? definition.UnitMass
            : 0f;

    public float OwnMass =>
        Mathf.Max(0f, UnitMass * Quantity);

    public float TotalMass =>
        Mathf.Max(
            0f,
            OwnMass +
            (containerState != null
                ? containerState.ContentsMass
                : 0f));

    public ItemContainerState ContainerState => containerState;
    public bool HasContainerState => containerState != null;

    public bool IsContainer => definition != null && definition.IsContainer;
    public int MaxStack => definition != null ? Mathf.Max(1, definition.MaxStack) : 1;
    public bool IsStackable => definition != null && !IsContainer && MaxStack > 1;
    public bool CanSplit => IsStackable && quantity > 1;

    public bool HasCharges => definition != null && definition.HasCharges;
    public int MaxCharges => definition != null ? definition.MaxCharges : 0;
    public int CurrentCharges => HasCharges ? Mathf.Clamp(currentCharges, 0, MaxCharges) : 0;
    public bool HasAnyCharges => !HasCharges || CurrentCharges > 0;

    public int RemainingStackSpace => IsStackable ? Mathf.Max(0, MaxStack - quantity) : 0;

    public static ItemInstance Create(ItemDefinition definition, int quantity = 1)
    {
        ItemInstance instance = new ItemInstance();
        instance.InitializeRuntime(definition, quantity);
        return instance;
    }

    public void InitializeRuntime(ItemDefinition newDefinition, int newQuantity)
    {
        definition = newDefinition;
        quantity = Mathf.Clamp(newQuantity, 1, MaxStack);

        if (string.IsNullOrWhiteSpace(instanceId))
            instanceId = Guid.NewGuid().ToString("N");

        InitializeChargesFromDefinition();
        EnsureContainerStateMatchesDefinition();
    }

    private void InitializeChargesFromDefinition()
    {
        if (definition == null || !definition.HasCharges)
        {
            currentCharges = 0;
            return;
        }

        currentCharges = definition.MaxCharges;
    }

    public void EnsureContainerStateMatchesDefinition()
    {
        if (definition == null || !definition.IsContainer)
        {
            SetContainerStateInternal(null);
            return;
        }

        if (containerState == null)
            SetContainerStateInternal(
                new ItemContainerState(
                    definition.ContainerSlotCount,
                    definition.ContainerColumnCount));
        else
            containerState.EnsureLayout(
                definition.ContainerSlotCount,
                definition.ContainerColumnCount);

        BindContainerState();
    }

    private void SetContainerStateInternal(ItemContainerState newState)
    {
        UnbindContainerState();
        containerState = newState;
        BindContainerState();
    }

    private void BindContainerState()
    {
        if (ReferenceEquals(subscribedContainerState, containerState))
            return;

        UnbindContainerState();

        subscribedContainerState = containerState;

        if (subscribedContainerState != null)
            subscribedContainerState.Changed += HandleContainerStateChanged;
    }

    private void UnbindContainerState()
    {
        if (subscribedContainerState != null)
            subscribedContainerState.Changed -= HandleContainerStateChanged;

        subscribedContainerState = null;
    }

    private void HandleContainerStateChanged()
    {
        Changed?.Invoke();
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }

    public bool CanStackWith(ItemInstance other)
    {
        if (other == null || other.definition == null || definition == null)
            return false;

        if (!IsStackable || !other.IsStackable)
            return false;

        if (HasCharges || other.HasCharges)
            return false;

        return definition == other.definition;
    }

    public int AddQuantity(int amount)
    {
        if (!IsStackable || amount <= 0)
            return 0;

        int added = Mathf.Min(RemainingStackSpace, amount);
        quantity += added;

        if (added > 0)
            NotifyChanged();

        return added;
    }

    public int RemoveQuantity(int amount)
    {
        if (amount <= 0)
            return 0;

        int removed = Mathf.Min(quantity, amount);
        quantity -= removed;

        if (removed > 0)
            NotifyChanged();

        return removed;
    }

    public ItemInstance SplitOff(int amount)
    {
        if (!IsStackable || amount <= 0 || amount >= quantity)
            return null;

        quantity -= amount;
        NotifyChanged();
        return Create(definition, amount);
    }

    public bool TryConsumeCharges(int amount)
    {
        if (!HasCharges || amount <= 0)
            return false;

        if (CurrentCharges < amount)
            return false;

        currentCharges -= amount;
        NotifyChanged();
        return true;
    }

    public int ConsumeChargesUpTo(int amount)
    {
        if (!HasCharges || amount <= 0)
            return 0;

        int consumed = Mathf.Min(CurrentCharges, amount);
        currentCharges -= consumed;

        if (consumed > 0)
            NotifyChanged();

        return consumed;
    }

    public void SetCharges(int value)
    {
        if (!HasCharges)
        {
            currentCharges = 0;
            return;
        }

        int next = Mathf.Clamp(value, 0, MaxCharges);

        if (currentCharges == next)
            return;

        currentCharges = next;
        NotifyChanged();
    }

    public void RefillCharges()
    {
        if (!HasCharges)
        {
            currentCharges = 0;
            return;
        }

        if (currentCharges == MaxCharges)
            return;

        currentCharges = MaxCharges;
        NotifyChanged();
    }

    public bool IsDepleted()
    {
        return definition == null || quantity <= 0;
    }

    public ItemInstanceSnapshot ToSnapshot()
    {
        if (definition == null || quantity <= 0)
            return null;

        return new ItemInstanceSnapshot
        {
            version = 1,
            instanceId = instanceId,
            itemId = definition.ItemId,
            quantity = quantity,
            currentCharges = HasCharges ? CurrentCharges : 0,
            container = containerState != null ? containerState.ToSnapshot() : null
        };
    }

    public static ItemInstance FromSnapshot(ItemInstanceSnapshot snapshot, IItemDefinitionResolver resolver)
    {
        if (snapshot == null || resolver == null)
            return null;

        ItemDefinition def = resolver.Resolve(snapshot.itemId);
        if (def == null)
        {
            Debug.LogWarning($"[ItemInstance] Failed to resolve item definition for itemId='{snapshot.itemId}'.");
            return null;
        }

        ItemInstance instance = new ItemInstance();
        instance.InitializeRuntime(def, snapshot.quantity);
        instance.instanceId = string.IsNullOrWhiteSpace(snapshot.instanceId)
            ? Guid.NewGuid().ToString("N")
            : snapshot.instanceId;

        if (def.HasCharges)
            instance.currentCharges = Mathf.Clamp(snapshot.currentCharges, 0, def.MaxCharges);
        else
            instance.currentCharges = 0;

        if (def.IsContainer)
        {
            if (snapshot.container != null)
            {
                instance.SetContainerStateInternal(
                    ItemContainerState.FromSnapshot(
                        snapshot.container,
                        resolver));
            }
            else
            {
                instance.EnsureContainerStateMatchesDefinition();
            }
        }

        return instance;
    }

    public bool CanAcceptIntoContainer(ItemInstance incoming)
    {
        if (incoming == null || incoming.Definition == null)
            return false;

        if (!IsContainer || definition == null || containerState == null)
            return false;

        return definition.CanContainerAccept(incoming.Definition);
    }

    public bool TryInsertIntoContainer(ItemInstance incoming, out ItemInstance remainder)
    {
        remainder = incoming;

        if (!CanAcceptIntoContainer(incoming))
            return false;

        if (incoming == null || containerState == null)
            return false;

        bool changed = false;

        for (int i = 0; i < containerState.SlotCount; i++)
        {
            InventorySlot slot = containerState.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            if (!slot.Instance.CanStackWith(incoming))
                continue;

            int moved = slot.Instance.AddQuantity(incoming.Quantity);
            if (moved > 0)
            {
                incoming.RemoveQuantity(moved);
                changed = true;
            }

            if (incoming.IsDepleted())
            {
                remainder = null;
                containerState.NotifyChanged();
                return true;
            }
        }

        for (int i = 0; i < containerState.SlotCount; i++)
        {
            InventorySlot slot = containerState.GetSlot(i);
            if (slot == null || !slot.IsEmpty)
                continue;

            slot.Set(incoming);
            remainder = null;
            changed = true;
            containerState.NotifyChanged();
            return true;
        }

        if (changed)
            containerState.NotifyChanged();

        remainder = incoming;
        return false;
    }

    public void ForceSetInstanceIdForRestore(string newInstanceId)
    {
        if (string.IsNullOrWhiteSpace(newInstanceId))
            return;

        instanceId = newInstanceId;
    }
}