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
    [SerializeField] private ChartingInstrumentState chartingInstrument;
    public ChartingInstrumentState ChartingInstrument => chartingInstrument;
    [SerializeField] private CartographicChartState cartographicChart;
    public bool HasCartographicChart => cartographicChart?.HasState == true;
    public bool HasStarReference => HasCartographicChart && StarReferenceItems.HasStars(cartographicChart.starReference);
    public bool HasSoundingEvidence => HasCartographicChart && cartographicChart.kind == CartographicChartKind.SoundingEvidence;
    public CartographicChartState CartographicChart => HasCartographicChart ? cartographicChart.Copy() : null;
    public Sprite VisualIcon => definition != null ? definition.ResolveVisualIcon(cartographicChart) : null;
    public void SetCartographicChart(CartographicChartState state)
    {
        if (state != null && (Quantity != 1 || IsContainer))
            throw new InvalidOperationException("Charts require one non-container item.");
        cartographicChart = state?.Copy();
        NotifyChanged();
    }

    public void SetChartingInstrumentState(ChartingInstrumentState state)
    {
        chartingInstrument = state != null ? state.Copy() : null;
        NotifyChanged();
    }

    // Physical environmental state that must survive WorldItem destruction,
    // inventory/Hands storage, scene persistence, and later re-instantiation.
    // Zero for ordinary/dry items. Waterlogging-capable world prefabs decide
    // whether and how this value changes.
    [SerializeField, Min(0f)] private float retainedWaterVolume;

    [NonSerialized] public Action Changed;

    [NonSerialized] private ItemContainerState subscribedContainerState;

    public string InstanceId => instanceId;
    public ItemDefinition Definition => definition;
    public int Quantity => Mathf.Max(1, quantity);

    public float UnitMass =>
        definition != null
            ? definition.UnitMass
            : 0f;

    public float UnitExposedVolumeContribution =>
        definition != null
            ? definition.UnitExposedVolumeContribution
            : 0f;

    /// <summary>
    /// Extra player displacement represented by this stack while it is physically
    /// exposed (held/equipped). Storage location decides whether this value is used.
    /// </summary>
    public float ExposedVolumeContribution =>
        Mathf.Max(0f, UnitExposedVolumeContribution * Quantity);

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
    public bool IsStackable => definition != null && !IsContainer && !HasCartographicChart && MaxStack > 1;
    public bool CanSplit => IsStackable && quantity > 1;

    public bool HasCharges => definition != null && definition.HasCharges;
    public int MaxCharges => definition != null ? definition.MaxCharges : 0;
    public int CurrentCharges => HasCharges ? Mathf.Clamp(currentCharges, 0, MaxCharges) : 0;
    public bool HasAnyCharges => !HasCharges || CurrentCharges > 0;

    public float RetainedWaterVolume =>
        Mathf.Max(0f, retainedWaterVolume);

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
        int removed = RemoveQuantityForTransaction(amount);
        if (removed > 0) NotifyChanged();
        return removed;
    }

    // Host inventory transactions publish notifications after all corresponding writes are complete.
    internal int RemoveQuantityForTransaction(int amount)
    {
        if (amount <= 0)
            return 0;

        int removed = Mathf.Min(quantity, amount);
        quantity -= removed;

        return removed;
    }

    internal void PublishTransactionChange() => NotifyChanged();

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

    /// <summary>
    /// Stores retained flood-water volume on the ItemInstance itself so the
    /// physical state survives pickup into Hands/inventory and normal save/load.
    /// The world-side WaterloggingMass2D component remains responsible for
    /// deciding capacity, leak/drain rates, and converting this volume to mass.
    /// </summary>
    public void SetRetainedWaterVolume(float volume)
    {
        float next =
            float.IsNaN(volume) ||
            float.IsInfinity(volume)
                ? 0f
                : Mathf.Max(0f, volume);

        if (Mathf.Abs(retainedWaterVolume - next) <= 0.000001f)
            return;

        retainedWaterVolume = next;
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
            retainedWaterVolume = RetainedWaterVolume,
            chartingInstrument = chartingInstrument != null ? chartingInstrument.Copy() : null,
            cartographicChart = CartographicChart,
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
        instance.cartographicChart = snapshot.cartographicChart?.HasState == true ? snapshot.cartographicChart.Copy() : null;
        instance.chartingInstrument = snapshot.chartingInstrument?.HasState == true ? snapshot.chartingInstrument.Copy() : null;
        instance.instanceId = string.IsNullOrWhiteSpace(snapshot.instanceId)
            ? Guid.NewGuid().ToString("N")
            : snapshot.instanceId;

        if (def.HasCharges)
            instance.currentCharges = Mathf.Clamp(snapshot.currentCharges, 0, def.MaxCharges);
        else
            instance.currentCharges = 0;

        instance.retainedWaterVolume =
            float.IsNaN(snapshot.retainedWaterVolume) ||
            float.IsInfinity(snapshot.retainedWaterVolume)
                ? 0f
                : Mathf.Max(0f, snapshot.retainedWaterVolume);

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
        // Keep one authoritative portable-container placement path.
        // This ensures per-slot quantity caps, stacking, partial insertion,
        // and future container rules cannot diverge between callers.
        return ContainerPlacementUtility.TryAutoInsert(
            this,
            incoming,
            out remainder);
    }

    public void ForceSetInstanceIdForRestore(string newInstanceId)
    {
        if (string.IsNullOrWhiteSpace(newInstanceId))
            return;

        instanceId = newInstanceId;
    }
}
