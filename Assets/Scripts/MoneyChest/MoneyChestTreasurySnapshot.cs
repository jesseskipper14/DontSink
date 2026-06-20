using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class MoneyChestTreasurySnapshot
{
    public int version = 1;

    public string activeChestInstanceId;

    public List<MoneyChestSnapshot> chests = new();

    public void EnsureDefaults()
    {
        chests ??= new List<MoneyChestSnapshot>();
    }
}

[System.Serializable]
public sealed class MoneyChestSnapshot
{
    public int version = 1;

    [Header("Identity")]
    public string chestInstanceId;
    public string itemId;

    [Header("Money")]
    public int balance;

    [Header("Lifecycle")]
    public MoneyChestLifecycleState lifecycleState = MoneyChestLifecycleState.Active;

    [Header("Last Known Location")]
    public string lastSceneName;
    public string boatInstanceId;

    public string nodeStableId;

    public string routeFromNodeId;
    public string routeToNodeId;

    public Vector2 lastWorldPosition;
    public Vector2 lastBoatLocalPosition;

    [Header("Flags")]
    public bool isReplacement;

    public bool IsActive => lifecycleState == MoneyChestLifecycleState.Active;
    public bool IsLost => lifecycleState == MoneyChestLifecycleState.Lost;
    public bool IsRetired => lifecycleState == MoneyChestLifecycleState.Retired;
}