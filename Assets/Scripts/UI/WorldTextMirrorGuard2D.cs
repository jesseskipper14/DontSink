using UnityEngine;

/// <summary>
/// Keeps this transform readable when an ancestor is mirrored on X.
///
/// Typical use:
/// - player/held-item visual roots flip by setting localScale.x negative;
/// - world-space TMP text under that hierarchy would otherwise render mirrored;
/// - this component counter-flips only the text transform.
///
/// The component intentionally knows nothing about TMP. It can protect any
/// child visual that must remain unmirrored.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class WorldTextMirrorGuard2D : MonoBehaviour
{
    [Header("Axes")]
    [Tooltip("Counter-flip this transform when its parent hierarchy is mirrored on X.")]
    [SerializeField] private bool keepWorldXReadable = true;

    [Tooltip("Usually leave this off. Enable only if some parent hierarchy can also mirror Y.")]
    [SerializeField] private bool keepWorldYReadable = false;

    private void OnEnable()
    {
        ApplyMirrorCorrection();
    }

    private void LateUpdate()
    {
        ApplyMirrorCorrection();
    }

    private void ApplyMirrorCorrection()
    {
        Transform parent = transform.parent;
        if (parent == null)
            return;

        Vector3 localScale = transform.localScale;

        if (keepWorldXReadable)
        {
            float magnitudeX = Mathf.Abs(localScale.x);

            // If the entire parent hierarchy is mirrored on X, mirror this child
            // locally as well. Two negatives produce a readable positive world X.
            localScale.x =
                parent.lossyScale.x < 0f
                    ? -magnitudeX
                    : magnitudeX;
        }

        if (keepWorldYReadable)
        {
            float magnitudeY = Mathf.Abs(localScale.y);

            localScale.y =
                parent.lossyScale.y < 0f
                    ? -magnitudeY
                    : magnitudeY;
        }

        transform.localScale = localScale;
    }
}
