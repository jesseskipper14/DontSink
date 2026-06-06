using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ItemChargeBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text valueText;

    [Header("Display")]
    [SerializeField] private bool showValueText = false;

    private ItemInstance boundItem;
    private ItemContainerState subscribedContainerState;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        Refresh();
    }

    private void OnEnable()
    {
        SubscribeToBoundItemContainer();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromContainer();
    }

    public void Bind(ItemInstance item)
    {
        if (ReferenceEquals(boundItem, item))
        {
            Refresh();
            return;
        }

        UnsubscribeFromContainer();

        boundItem = item;

        SubscribeToBoundItemContainer();
        Refresh();
    }

    public void Clear()
    {
        UnsubscribeFromContainer();

        boundItem = null;
        Refresh();
    }

    public void Refresh()
    {
        if (!ItemChargeDisplayUtility.TryGetChargeDisplay(boundItem, out ItemChargeDisplayInfo info) ||
            !info.ShouldShow)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        if (fillImage != null)
            fillImage.fillAmount = info.Normalized;

        if (valueText != null)
            valueText.text = showValueText ? $"{info.Current}/{info.Max}" : "";
    }

    private void SubscribeToBoundItemContainer()
    {
        if (boundItem == null || boundItem.ContainerState == null)
            return;

        subscribedContainerState = boundItem.ContainerState;
        subscribedContainerState.Changed -= HandleContainedItemChanged;
        subscribedContainerState.Changed += HandleContainedItemChanged;
    }

    private void UnsubscribeFromContainer()
    {
        if (subscribedContainerState == null)
            return;

        subscribedContainerState.Changed -= HandleContainedItemChanged;
        subscribedContainerState = null;
    }

    private void HandleContainedItemChanged()
    {
        Refresh();
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }
}