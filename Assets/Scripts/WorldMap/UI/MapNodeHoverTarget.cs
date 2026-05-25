using System;
using UnityEngine;
using UnityEngine.EventSystems;

[Obsolete("Old UI map view - use cartridge view instead")]
public sealed class MapNodeHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public int NodeId { get; set; }

    [SerializeField] private WorldMapHoverController hover;

    private void Awake()
    {
        if (hover == null)
            hover = FindAnyObjectByType<WorldMapHoverController>();
    }

    public void OnPointerEnter(PointerEventData eventData) => hover?.SetHoverFromUI(NodeId);
    public void OnPointerExit(PointerEventData eventData) => hover?.ClearHoverFromUI(NodeId);
}
