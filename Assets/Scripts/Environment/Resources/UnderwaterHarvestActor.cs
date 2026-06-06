using System;
using UnityEngine;

public sealed class UnderwaterHarvestActor : MonoBehaviour
{
    [Header("Debug Tool Flags")]
    [Tooltip("Temporary v1 hook. Later this should check equipped/held inventory tools.")]
    [SerializeField] private bool hasSimpleDrill = true;

    [Tooltip("Temporary v1 hook. Craneables should stay blocked for now.")]
    [SerializeField] private bool hasCrane = false;

    [Header("Debug Water State")]
    [Tooltip("Temporary v1 hook. Replace with PlayerSubmersionState / BoatWaterContextResolver when wiring into the real player.")]
    [SerializeField] private bool debugTreatAsUnderwater = true;

    [Header("Oxygen")]
    [Tooltip("Simple dive suit v1 sets this to 2.")]
    [Min(1f)]
    [SerializeField] private float oxygenCapacityMultiplier = 1f;

    public event Action<UnderwaterResourceDefinition, UnderwaterResourceYield, int> YieldReceived;

    public bool IsUnderwater => debugTreatAsUnderwater;

    public float OxygenCapacityMultiplier => Mathf.Max(1f, oxygenCapacityMultiplier);

    public bool HasTool(UnderwaterResourceToolKind toolKind)
    {
        return toolKind switch
        {
            UnderwaterResourceToolKind.None => true,
            UnderwaterResourceToolKind.SimpleDrill => hasSimpleDrill,
            UnderwaterResourceToolKind.Crane => hasCrane,
            _ => false
        };
    }

    public void SetSimpleDrillAvailable(bool available)
    {
        hasSimpleDrill = available;
    }

    public void SetCraneAvailable(bool available)
    {
        hasCrane = available;
    }

    public void SetDebugUnderwater(bool underwater)
    {
        debugTreatAsUnderwater = underwater;
    }

    public void SetOxygenCapacityMultiplier(float multiplier)
    {
        oxygenCapacityMultiplier = Mathf.Max(1f, multiplier);
    }

    public void ReceiveYield(
        UnderwaterResourceDefinition definition,
        UnderwaterResourceYield yield,
        int quantity)
    {
        if (definition == null || yield == null || yield.itemDefinition == null || quantity <= 0)
            return;

        YieldReceived?.Invoke(definition, yield, quantity);

        Debug.Log(
            $"Harvested {quantity}x {yield.itemDefinition.name} from {definition.displayName}. " +
            $"TODO: wire this into real inventory add path.",
            this);
    }
}