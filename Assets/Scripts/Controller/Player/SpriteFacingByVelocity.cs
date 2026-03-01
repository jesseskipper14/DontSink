using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpriteFacingByVelocity : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform visualRoot;

    [Tooltip("Minimum horizontal speed before flipping.")]
    public float flipThreshold = 0.1f;

    void Awake()
    {
        if (!rb) rb = GetComponentInParent<Rigidbody2D>();
        if (!visualRoot) visualRoot = transform;
    }

    void LateUpdate()
    {
        if (!rb) return;

        float vx = rb.linearVelocity.x;

        if (Mathf.Abs(vx) < flipThreshold) return;

        Vector3 scale = visualRoot.localScale;
        scale.x = Mathf.Sign(vx) * Mathf.Abs(scale.x);
        visualRoot.localScale = scale;
    }
}