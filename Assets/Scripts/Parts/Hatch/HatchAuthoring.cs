using UnityEngine;

[DisallowMultipleComponent]
public sealed class HatchAuthoring : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer frameRenderer;
    [SerializeField] private SpriteRenderer closedRenderer;
    [SerializeField] private SpriteRenderer openRenderer;

    [Header("Collision")]
    [SerializeField] private Collider2D blockingCollider;

    [Tooltip("Optional one-way ledge/platform collider used while hatch is open.")]
    [SerializeField] private Collider2D ledgeCollider;

    [Header("Dimensions")]
    [Min(0.1f)]
    [SerializeField] private float openingWidth = 1f;

    [Min(0.1f)]
    [SerializeField] private float frameWidth = 1f;

    [Header("State")]
    [SerializeField] private bool startsOpen = false;

    [Header("Identity")]
    [SerializeField] private string hatchId = "";

    public SpriteRenderer FrameRenderer => frameRenderer;
    public SpriteRenderer ClosedRenderer => closedRenderer;
    public SpriteRenderer OpenRenderer => openRenderer;

    public Collider2D BlockingCollider => blockingCollider;
    public Collider2D LedgeCollider => ledgeCollider;

    public float OpeningWidth => openingWidth;
    public float FrameWidth => frameWidth;
    public bool StartsOpen => startsOpen;

    public string HatchId
    {
        get => hatchId;
        set => hatchId = value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (openingWidth < 0.1f)
            openingWidth = 0.1f;

        if (frameWidth < openingWidth)
            frameWidth = openingWidth;
    }
#endif
}