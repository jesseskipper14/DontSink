using UnityEngine;

[System.Flags]
public enum CompartmentBoundaryRole
{
    None = 0,
    Floor = 1 << 0,
    Wall = 1 << 1,
    Roof = 1 << 2,
    Ledge = 1 << 3,
    Hatch = 1 << 4,
    Door = 1 << 5,
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class CompartmentBoundaryAuthoring : MonoBehaviour
{
    [SerializeField]
    private CompartmentBoundaryRole roles =
        CompartmentBoundaryRole.Wall;

    [SerializeField] private bool countsAsBoundary = true;

    [Header("Optional")]
    [SerializeField] private bool isOpeningCarrier = false;
    [SerializeField] private string openingId = "";

    private Collider2D _collider;

    public CompartmentBoundaryRole Roles => roles;
    public bool CountsAsBoundary => countsAsBoundary;
    public bool IsOpeningCarrier => isOpeningCarrier;
    public string OpeningId => openingId;

    public Collider2D Collider
    {
        get
        {
            if (_collider == null)
                _collider = GetComponent<Collider2D>();
            return _collider;
        }
    }

    public Bounds WorldBounds => Collider != null ? Collider.bounds : default;

    public bool HasRole(CompartmentBoundaryRole role) => (roles & role) != 0;

    public bool IsHorizontalBlocker =>
        HasRole(CompartmentBoundaryRole.Floor) ||
        HasRole(CompartmentBoundaryRole.Roof) ||
        HasRole(CompartmentBoundaryRole.Ledge) ||
        HasRole(CompartmentBoundaryRole.Hatch);

    public bool IsVerticalBlocker =>
        HasRole(CompartmentBoundaryRole.Wall) ||
        HasRole(CompartmentBoundaryRole.Door);

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _collider = GetComponent<Collider2D>();
    }
#endif
}