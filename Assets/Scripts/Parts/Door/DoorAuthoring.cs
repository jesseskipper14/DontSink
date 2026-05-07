using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorAuthoring : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer closedRenderer;
    [SerializeField] private SpriteRenderer openRenderer;

    [Header("Collision")]
    [Tooltip("Collider that blocks passage while the door is closed.")]
    [SerializeField] private Collider2D blockingCollider;

    [Header("Opening")]
    [Tooltip("Fallback opening height if no blocking collider can provide one.")]
    [Min(0.1f)]
    [SerializeField] private float openingHeight = 2f;

    [Tooltip("If true, the builder uses the blocking collider height as the wall opening height.")]
    [SerializeField] private bool deriveOpeningHeightFromBlockingCollider = true;

    [Tooltip("Extra vertical gap added above and below the door blocker when cutting the wall. Prevents door blocker/floor/wall collider overlap.")]
    [Min(0f)]
    [SerializeField] private float builderOpeningClearance = 0.03f;

    [Header("State")]
    [SerializeField] private bool startsOpen = false;

    [Header("Identity")]
    [SerializeField] private string doorId = "";

    public SpriteRenderer ClosedRenderer => closedRenderer;
    public SpriteRenderer OpenRenderer => openRenderer;
    public Collider2D BlockingCollider => blockingCollider;
    public bool StartsOpen => startsOpen;

    public float OpeningHeight => Mathf.Max(0.1f, openingHeight);
    public float BuilderOpeningClearance => Mathf.Max(0f, builderOpeningClearance);

    public string DoorId
    {
        get => doorId;
        set => doorId = value;
    }

    public float GetOpeningHeightWorld()
    {
        if (deriveOpeningHeightFromBlockingCollider && blockingCollider != null)
        {
            if (blockingCollider is BoxCollider2D box)
                return Mathf.Max(0.1f, Mathf.Abs(box.size.y * box.transform.lossyScale.y));

            Bounds b = blockingCollider.bounds;
            if (b.size.y > 0.0001f)
                return Mathf.Max(0.1f, b.size.y);
        }

        return Mathf.Max(0.1f, openingHeight);
    }

    public float GetBuilderOpeningClearanceWorld()
    {
        return Mathf.Max(0f, builderOpeningClearance);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            string n = renderers[i].name.ToLowerInvariant();

            if (closedRenderer == null && n.Contains("closed"))
                closedRenderer = renderers[i];

            if (openRenderer == null && n.Contains("open"))
                openRenderer = renderers[i];
        }

        if (blockingCollider == null)
            blockingCollider = GetComponentInChildren<Collider2D>(true);

        if (blockingCollider is BoxCollider2D box)
            openingHeight = Mathf.Max(0.1f, Mathf.Abs(box.size.y * box.transform.lossyScale.y));
    }

    private void OnValidate()
    {
        openingHeight = Mathf.Max(0.1f, openingHeight);
        builderOpeningClearance = Mathf.Max(0f, builderOpeningClearance);
    }
#endif
}