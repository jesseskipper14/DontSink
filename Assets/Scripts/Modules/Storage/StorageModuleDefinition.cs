using System;
using UnityEngine;

[Serializable]
public sealed class StorageModuleDefinition
{
    [Header("Storage Mode")]
    [SerializeField] private StorageModuleMode mode = StorageModuleMode.None;

    [Header("Layout")]
    [Min(1)]
    [SerializeField] private int slotCount = 12;

    [Min(1)]
    [SerializeField] private int columnCount = 4;

    [Header("Container Rack Rules")]
    [SerializeField] private bool acceptsPortableContainers = false;
    [SerializeField] private bool acceptsCargoCrates = false;

    [Header("Future Securing Rules")]
    [Tooltip("If true, contents are treated as intentionally secured to the boat.")]
    [SerializeField] private bool countsAsSecuredStorage = true;

    [Tooltip("Future hook. Keep at 0 for now. Later storms/combat may use this.")]
    [Range(0f, 1f)]
    [SerializeField] private float looseFailureChance = 0f;

    public StorageModuleMode Mode => mode;

    public int SlotCount => Mathf.Max(1, slotCount);
    public int ColumnCount => Mathf.Max(1, columnCount);

    public bool AcceptsPortableContainers => acceptsPortableContainers;
    public bool AcceptsCargoCrates => acceptsCargoCrates;

    public bool CountsAsSecuredStorage => countsAsSecuredStorage;
    public float LooseFailureChance => Mathf.Clamp01(looseFailureChance);

    public bool HasStorage => mode != StorageModuleMode.None;
    public bool IsFixedStorage => mode == StorageModuleMode.FixedStorage;
    public bool IsContainerRack => mode == StorageModuleMode.ContainerRack;

#if UNITY_EDITOR
    public void Editor_SetDefaultsForMode()
    {
        switch (mode)
        {
            case StorageModuleMode.None:
                slotCount = 1;
                columnCount = 1;
                acceptsPortableContainers = false;
                acceptsCargoCrates = false;
                countsAsSecuredStorage = true;
                looseFailureChance = 0f;
                break;

            case StorageModuleMode.FixedStorage:
                slotCount = 12;
                columnCount = 4;
                acceptsPortableContainers = false;
                acceptsCargoCrates = false;
                countsAsSecuredStorage = true;
                looseFailureChance = 0f;
                break;

            case StorageModuleMode.ContainerRack:
                slotCount = 4;
                columnCount = 2;
                acceptsPortableContainers = true;
                acceptsCargoCrates = true;
                countsAsSecuredStorage = true;
                looseFailureChance = 0f;
                break;
        }
    }
#endif
}