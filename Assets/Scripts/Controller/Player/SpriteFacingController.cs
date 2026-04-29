using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpriteFacingController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private MonoBehaviour intentSourceComponent; // assign LocalCharacterIntentSource

    private ICharacterIntentSource _intentSource;

    [Tooltip("Minimum horizontal speed before flipping when NOT focusing.")]
    public float flipThreshold = 0.1f;

    void Awake()
    {
        if (!rb) rb = GetComponentInParent<Rigidbody2D>();
        if (!visualRoot) visualRoot = transform;

        _intentSource = intentSourceComponent as ICharacterIntentSource;

        if (_intentSource == null && intentSourceComponent != null)
        {
            Debug.LogWarning("[Facing] Intent source does not implement ICharacterIntentSource", this);
        }
    }

    void LateUpdate()
    {
        if (!rb || _intentSource == null)
            return;

        var intent = _intentSource.Current;

        float facingX = 0f;

        if (intent.FocusHeld)
        {
            Vector2 pos = rb.position;
            Vector2 dir = intent.FocusWorldPoint - pos;

            if (Mathf.Abs(dir.x) > 0.01f)
                facingX = dir.x;
        }
        else
        {
            float vx = rb.linearVelocity.x;

            if (Mathf.Abs(vx) > flipThreshold)
                facingX = vx;
        }

        if (Mathf.Abs(facingX) < 0.01f)
            return;

        Vector3 scale = visualRoot.localScale;
        scale.x = Mathf.Sign(facingX) * Mathf.Abs(scale.x);
        visualRoot.localScale = scale;
    }
}