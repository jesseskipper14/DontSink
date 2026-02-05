using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorldMapClickController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private WorldMapNodeSelection selection;

    [Header("Raycast")]
    [Tooltip("Only objects on these layers are clickable. Put NodeView objects on this layer.")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    private void Reset()
    {
        cam = Camera.main;
        selection = FindAnyObjectByType<WorldMapNodeSelection>();
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) == false) return;

        // If you have UI, don't click-through
        if (PointerOverInteractiveUI())
        {
            return;
        }

        if (cam == null || selection == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = new Vector2(world.x, world.y);

        RaycastHit2D hit = Physics2D.Raycast(p, Vector2.zero, 0f, clickableLayers);
        if (hit.collider == null) return;

        var view = hit.collider.GetComponent<MapNodeView>();
        if (view == null) return;

        selection.Select(view.NodeId);
    }

    private static bool PointerOverInteractiveUI()
    {
        if (EventSystem.current == null) return false;

        var results = new List<RaycastResult>();
        var data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        EventSystem.current.RaycastAll(data, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.GetComponent<Selectable>() != null)
                return true; // Button/Slider/etc.
        }

        return false;
    }
}
