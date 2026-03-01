using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CharacterMotor2D))]
public sealed class PlayerBoardingState : MonoBehaviour
{
    [Header("Layer Names")]
    [SerializeField] private string hullLayerName = "Hull";
    [SerializeField] private string groundLayerName = "Ground";
    [SerializeField] private string nodeGroundLayerName = "NodeGround";

    public bool IsBoarded { get; private set; }
    public Transform CurrentBoatRoot { get; private set; }

    private Rigidbody2D _rb;
    private CharacterMotor2D _motor;

    private int _hullLayer;

    private LayerMask _boardedMask;
    private LayerMask _unboardedMask;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _motor = GetComponent<CharacterMotor2D>();

        _hullLayer = LayerMask.NameToLayer(hullLayerName);
        int groundLayer = LayerMask.NameToLayer(groundLayerName);
        int nodeGroundLayer = LayerMask.NameToLayer(nodeGroundLayerName);

        if (_hullLayer < 0) Debug.LogError($"Layer '{hullLayerName}' not found.");
        if (groundLayer < 0) Debug.LogError($"Layer '{groundLayerName}' not found.");
        if (nodeGroundLayer < 0) Debug.LogError($"Layer '{nodeGroundLayerName}' not found.");

        _boardedMask = 1 << _hullLayer;
        _unboardedMask = (1 << groundLayer) | (1 << nodeGroundLayer);

        ApplyMask();
    }

    public void Board(Transform boatRoot)
    {
        IsBoarded = true;
        CurrentBoatRoot = boatRoot;
        ApplyMask();
    }

    public void Unboard()
    {
        IsBoarded = false;
        CurrentBoatRoot = null;
        ApplyMask();
    }

    private void ApplyMask()
    {
        // ---- Collision Mask (existing behavior) ----
        int mask = _rb.excludeLayers;

        if (IsBoarded)
            mask &= ~(1 << _hullLayer); // allow hull collisions
        else
            mask |= (1 << _hullLayer);  // exclude hull collisions

        _rb.excludeLayers = mask;

        // ---- Ground Detection Mask (NEW) ----
        _motor.groundMask = IsBoarded ? _boardedMask : _unboardedMask;
    }
}