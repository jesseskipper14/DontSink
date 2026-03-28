using UnityEngine;

public sealed class EngineModule : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isOn;

    [Header("Fuel")]
    [SerializeField] private ItemDefinition fuelContainerDefinition;

    private ItemInstance fuelContainerItem;

    public bool IsOn => isOn;
    public ItemInstance FuelContainerItem => fuelContainerItem;

    public void InitializeFuel()
    {
        if (fuelContainerItem != null)
            return;

        if (fuelContainerDefinition == null)
        {
            Debug.LogWarning("[EngineModule] No fuel container definition assigned.", this);
            return;
        }

        fuelContainerItem = ItemInstance.Create(fuelContainerDefinition, 1);
    }

    public void SetOn(bool value)
    {
        isOn = value;
    }

    public void Toggle()
    {
        isOn = !isOn;
    }
}