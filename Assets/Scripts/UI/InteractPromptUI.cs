using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractPromptUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform rowRoot;
    [SerializeField] private InteractPromptActionRowUI rowPrefab;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Vector2 screenOffset = new(0f, 64f);

    private readonly List<InteractPromptActionRowUI> _rows = new();

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        Hide();
    }

    public void Show(Vector3 worldPos, IReadOnlyList<PromptAction> actions)
    {
        if (actions == null || actions.Count == 0)
        {
            Hide();
            return;
        }

        Rebuild(actions);
        Position(worldPos);
        SetVisible(true);
    }

    public void Hide()
    {
        ClearRows();
        SetVisible(false);
    }

    private void Rebuild(IReadOnlyList<PromptAction> actions)
    {
        ClearRows();

        if (rowRoot == null || rowPrefab == null)
            return;

        for (int i = 0; i < actions.Count; i++)
        {
            InteractPromptActionRowUI row = Instantiate(rowPrefab, rowRoot);
            row.Bind(actions[i]);
            _rows.Add(row);
        }
    }

    private void ClearRows()
    {
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (_rows[i] != null)
                Destroy(_rows[i].gameObject);
        }

        _rows.Clear();

        if (rowRoot == null)
            return;

        for (int i = rowRoot.childCount - 1; i >= 0; i--)
            Destroy(rowRoot.GetChild(i).gameObject);
    }

    private void Position(Vector3 worldPos)
    {
        if (root == null || canvas == null || worldCamera == null)
            return;

        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera,
            out Vector2 localPos
        );

        root.anchoredPosition = localPos + screenOffset;
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
            root.gameObject.SetActive(visible);
        else
            gameObject.SetActive(visible);
    }
}