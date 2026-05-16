using UnityEngine;

[CreateAssetMenu(menuName = "Cargo/Sorting Policy", fileName = "CargoSortingPolicy")]
public sealed class CargoSortingPolicy : ScriptableObject
{
    [Header("Ground")]
    [SerializeField] private string groundSortingLayer = "NodeCargo";
    [SerializeField] private int groundSortingOrder = 0;

    [Header("Held")]
    [SerializeField] private string heldSortingLayer = "NodeHeldCargo";
    [SerializeField] private int heldSortingOrder = 10;

    public string GroundSortingLayer => groundSortingLayer;
    public int GroundSortingOrder => groundSortingOrder;

    public string HeldSortingLayer => heldSortingLayer;
    public int HeldSortingOrder => heldSortingOrder;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Mild guardrails. Unity doesn't give a great API for validating layer names here,
        // so we just protect against blank strings.
        if (string.IsNullOrWhiteSpace(groundSortingLayer)) groundSortingLayer = "Default";
        if (string.IsNullOrWhiteSpace(heldSortingLayer)) heldSortingLayer = "Default";
    }
#endif
}