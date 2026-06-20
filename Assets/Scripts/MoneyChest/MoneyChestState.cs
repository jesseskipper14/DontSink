using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MoneyChestState : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string chestInstanceId;

    [Header("Money")]
    [Min(0)]
    [SerializeField] private int balance;

    [Header("Lifecycle")]
    [SerializeField] private MoneyChestLifecycleState lifecycleState = MoneyChestLifecycleState.Active;

    [Header("Identity Source")]
    [SerializeField] private bool preferWorldItemInstanceId = true;

    public string ChestInstanceId => chestInstanceId;
    public int Balance => Mathf.Max(0, balance);
    public MoneyChestLifecycleState LifecycleState => lifecycleState;

    public bool IsActive => lifecycleState == MoneyChestLifecycleState.Active;
    public bool IsLost => lifecycleState == MoneyChestLifecycleState.Lost;
    public bool IsRetired => lifecycleState == MoneyChestLifecycleState.Retired;

    public event Action<MoneyChestState> Changed;
    public event Action<MoneyChestState, int> BalanceChanged;
    public event Action<MoneyChestState, MoneyChestLifecycleState> LifecycleChanged;

    private bool _started;

    private void Awake()
    {
        SyncInstanceIdFromWorldItem();
        EnsureInstanceId();
        balance = Mathf.Max(0, balance);
    }

    private void Start()
    {
        _started = true;

        // WorldItem.Initialize(...) may happen after Awake on restored items.
        SyncInstanceIdFromWorldItem();
        EnsureInstanceId();

        RegisterWithTreasury();
    }

    private void OnEnable()
    {
        if (_started)
            RegisterWithTreasury();
    }

    private void OnDisable()
    {
        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury != null)
            treasury.UnregisterChest(this);
    }

    public void SyncInstanceIdFromWorldItem()
    {
        if (!preferWorldItemInstanceId)
            return;

        WorldItem worldItem = GetComponent<WorldItem>();
        if (worldItem == null)
            worldItem = GetComponentInParent<WorldItem>();

        ItemInstance instance = worldItem != null ? worldItem.Instance : null;
        string itemInstanceId = instance != null ? instance.InstanceId : null;

        if (string.IsNullOrWhiteSpace(itemInstanceId))
            return;

        if (chestInstanceId == itemInstanceId)
            return;

        chestInstanceId = itemInstanceId;
    }

    public void EnsureInstanceId()
    {
        SyncInstanceIdFromWorldItem();

        if (!string.IsNullOrWhiteSpace(chestInstanceId))
            return;

        // Fallback for scene-placed/debug chests that do not have a WorldItem instance yet.
        chestInstanceId = $"money_chest_{Guid.NewGuid():N}";
    }

    public string GetItemId()
    {
        WorldItem worldItem = GetComponent<WorldItem>();
        if (worldItem == null)
            worldItem = GetComponentInParent<WorldItem>();

        ItemDefinition def = worldItem != null && worldItem.Instance != null
            ? worldItem.Instance.Definition
            : null;

        return def != null ? def.ItemId : null;
    }

    public void RestoreState(
        string restoredChestInstanceId,
        int restoredBalance,
        MoneyChestLifecycleState restoredLifecycleState)
    {
        if (!string.IsNullOrWhiteSpace(restoredChestInstanceId))
            chestInstanceId = restoredChestInstanceId;
        else
            EnsureInstanceId();

        balance = Mathf.Max(0, restoredBalance);
        lifecycleState = restoredLifecycleState;

        BalanceChanged?.Invoke(this, balance);
        LifecycleChanged?.Invoke(this, lifecycleState);
        RaiseChanged();
    }

    public void SetBalance(int value)
    {
        int nextBalance = Mathf.Max(0, value);

        if (balance == nextBalance)
            return;

        balance = nextBalance;

        BalanceChanged?.Invoke(this, balance);
        RaiseChanged();
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        SetBalance(balance + amount);
    }

    public bool CanSpend(int amount)
    {
        if (amount <= 0)
            return true;

        return balance >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        if (balance < amount)
            return false;

        SetBalance(balance - amount);
        return true;
    }

    public int DrainAllMoney()
    {
        int drained = Balance;
        SetBalance(0);
        return drained;
    }

    public void MarkActive()
    {
        SetLifecycle(MoneyChestLifecycleState.Active);
    }

    public void MarkLost()
    {
        SetLifecycle(MoneyChestLifecycleState.Lost);
    }

    public void MarkRetired()
    {
        SetBalance(0);
        SetLifecycle(MoneyChestLifecycleState.Retired);
    }

    private void SetLifecycle(MoneyChestLifecycleState nextState)
    {
        if (lifecycleState == nextState)
            return;

        lifecycleState = nextState;

        LifecycleChanged?.Invoke(this, lifecycleState);
        RaiseChanged();
    }

    private void RegisterWithTreasury()
    {
        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury != null)
            treasury.RegisterChest(this);
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        balance = Mathf.Max(0, balance);
    }

    [ContextMenu("Debug/Sync Instance Id From WorldItem")]
    private void DebugSyncInstanceIdFromWorldItem()
    {
        SyncInstanceIdFromWorldItem();
        EnsureInstanceId();
        Debug.Log($"Money chest instance id: {chestInstanceId}", this);
    }

    [ContextMenu("Debug/Set This Chest Active In Treasury")]
    private void DebugSetThisChestActiveInTreasury()
    {
        EnsureInstanceId();

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury == null)
        {
            Debug.LogWarning("Cannot set this money chest active because no MoneyChestTreasuryService exists.", this);
            return;
        }

        treasury.SetActiveChest(this);

        Debug.Log(
            $"Set this money chest active in treasury. id='{ChestInstanceId}', balance={Balance}",
            this);
    }

    [ContextMenu("Debug/Add 100 Directly To This Chest")]
    private void DebugAdd100Directly()
    {
        AddMoney(100);
        Debug.Log($"Added 100 directly to this chest. Balance={Balance}", this);
    }

    [ContextMenu("Debug/Mark This Chest Lost")]
    private void DebugMarkThisChestLost()
    {
        MarkLost();
        Debug.Log($"Marked this money chest Lost. id='{ChestInstanceId}', balance={Balance}", this);
    }

    [ContextMenu("Debug/Mark This Chest Active Local Only")]
    private void DebugMarkThisChestActiveLocalOnly()
    {
        MarkActive();
        Debug.Log($"Marked this money chest Active locally. id='{ChestInstanceId}', balance={Balance}", this);
    }
#endif
}