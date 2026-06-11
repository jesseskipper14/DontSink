using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWearableVisualController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private SpriteRenderer wearableRenderer;

    [Header("Visual Priority")]
    [Tooltip("First equipped item in this list with a wearable visual sprite wins.")]
    [SerializeField]
    private BottomBarSlotType[] wearableVisualPriority =
    {
        BottomBarSlotType.Body,
        BottomBarSlotType.Head,
        BottomBarSlotType.Backpack,
        BottomBarSlotType.Toolbelt,
        BottomBarSlotType.Feet
    };

    private void Reset()
    {
        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>();

        if (wearableRenderer == null)
            wearableRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void Awake()
    {
        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>();

        Refresh();
    }

    private void OnEnable()
    {
        if (equipment != null)
            equipment.EquipmentChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (equipment != null)
            equipment.EquipmentChanged -= Refresh;
    }

    public void Refresh()
    {
        if (wearableRenderer == null)
            return;

        Sprite sprite = FindWearableSprite();

        wearableRenderer.sprite = sprite;
        wearableRenderer.enabled = sprite != null;
    }

    private Sprite FindWearableSprite()
    {
        if (equipment == null || wearableVisualPriority == null)
            return null;

        for (int i = 0; i < wearableVisualPriority.Length; i++)
        {
            ItemInstance item = equipment.Get(wearableVisualPriority[i]);
            if (item == null || item.Definition == null)
                continue;

            if (item.Definition.WearableVisualSprite != null)
                return item.Definition.WearableVisualSprite;
        }

        return null;
    }
}