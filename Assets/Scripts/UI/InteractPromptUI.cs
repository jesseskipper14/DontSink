using UnityEngine;
using UnityEngine.UI;

public sealed class InteractPromptUI : MonoBehaviour
{
    [SerializeField] private GameObject rootObject;
    [SerializeField] private TMPro.TMP_Text tmpLabel;
    [SerializeField] private Text legacyLabel;

    [Header("Follow (optional)")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 40f);

    private void Reset()
    {
        rootObject = gameObject;
        tmpLabel = GetComponentInChildren<TMPro.TMP_Text>(true);
        legacyLabel = GetComponentInChildren<Text>(true);
        canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        worldCamera = Camera.main;
    }

    private void Awake() => Hide();

    public void Show(string verb, Vector3 worldPos)
    {
        SetText($"Press E to {verb}");
        Position(worldPos);
        SetVisible(true);
    }

    public void Hide() => SetVisible(false);

    public void Position(Vector3 worldPos)
    {
        if (canvasRect == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Vector3 sp = worldCamera.WorldToScreenPoint(worldPos);
        sp += (Vector3)screenOffset;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, sp, null, out var local);

        (transform as RectTransform).anchoredPosition = local;
    }

    private void SetText(string text)
    {
        if (tmpLabel != null) tmpLabel.text = text;
        if (legacyLabel != null) legacyLabel.text = text;
    }

    private void SetVisible(bool visible)
    {
        if (rootObject == null) rootObject = gameObject;
        rootObject.SetActive(visible);
    }
}