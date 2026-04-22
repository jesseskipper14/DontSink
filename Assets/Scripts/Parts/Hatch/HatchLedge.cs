using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class HatchLedge : MonoBehaviour
{
    [Header("One-Way Platform")]
    [SerializeField] private bool configureOneWayPlatformOnReset = true;

    [SerializeField, Range(1f, 360f)]
    private float surfaceArc = 160f;

    private Collider2D _collider;

    public Collider2D Collider
    {
        get
        {
            if (_collider == null)
                _collider = GetComponent<Collider2D>();

            return _collider;
        }
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _collider = GetComponent<Collider2D>();

        if (!configureOneWayPlatformOnReset)
            return;

        _collider.isTrigger = false;
        _collider.usedByEffector = true;

        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
        if (effector == null)
            effector = gameObject.AddComponent<PlatformEffector2D>();

        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        effector.surfaceArc = surfaceArc;
    }

    private void OnValidate()
    {
        _collider = GetComponent<Collider2D>();

        if (_collider != null)
            _collider.isTrigger = false;
    }
#endif
}