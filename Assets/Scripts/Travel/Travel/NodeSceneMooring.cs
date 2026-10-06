using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Connects two pairs of authored moors. Owns ropes/joints only, never harbor geometry or boat pose.</summary>
[DisallowMultipleComponent]
public sealed class NodeSceneMooring : MonoBehaviour
{
    public string nodeSceneName = "NodeScene";
    [Tooltip("Discover dock moors under this root. Empty uses this object's children.")]
    public Transform dockRoot;
    public Material ropeMaterial;
    [Min(0f)] public float slack = .5f;
    [Min(.01f)] public float ropeWidth = .075f;
    [Min(.1f)] public float maximumAttachDistance = 60f;
    public string sortingLayer = "WorldDock";
    public int sortingOrder = 5;
    [SerializeField] private string status = "Waiting for authored moors";
    public string Status => status;
    public bool IsMoored => _boat != null && _dock[0] != null && _dock[1] != null && _hull[0] != null && _hull[1] != null;
    private Boat _boat;
    private readonly MooringPoint2D[] _dock = new MooringPoint2D[2], _hull = new MooringPoint2D[2];
    private readonly DistanceJoint2D[] _joints = new DistanceJoint2D[2];
    private readonly LineRenderer[] _lines = new LineRenderer[2];
    private readonly float[] _lengths = new float[2];
    private float _nextSearch, _nextMessage;
    private bool _departing;
    private static readonly List<NodeSceneMooring> Active = new();

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); _departing = false; _nextSearch = 0f; }
    private void OnDisable() { Active.Remove(this); Release(); }
    private void Update()
    {
        if (_departing || gameObject.scene.name != nodeSceneName) return;
        if (IsMoored) return;
        if (Time.unscaledTime < _nextSearch) return;
        _nextSearch = Time.unscaledTime + .5f;
        TryTie();
    }

    [ContextMenu("Try tie authored moors (Play Mode)")]
    public bool TryTie()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _departing || gameObject.scene.name != nodeSceneName ||
            GameState.I != null && GameState.I.activeTravel != null) return false;
        if (IsMoored) return true;
        Release();
        if (!WorldTopology.IsFinite(slack) || slack < 0f || !WorldTopology.IsFinite(maximumAttachDistance) || maximumAttachDistance <= 0f)
        { status = "Use finite, nonnegative slack and a positive attach distance."; return false; }
        var root = dockRoot != null ? dockRoot : transform;
        if (root.gameObject.scene != gameObject.scene) return false;
        var dockPoints = root.GetComponentsInChildren<MooringPoint2D>();
        for (int i = 0; i < 2; i++)
        {
            foreach (var point in dockPoints)
            {
                if (!point.isActiveAndEnabled || point.Boat != null || (int)point.role != i) continue;
                if (_dock[i] != null) { status = "Duplicate dock moor role; use one Aft and one Forward."; return false; }
                _dock[i] = point;
            }
            if (_dock[i] == null) { status = "Waiting for dock Aft and Forward moors."; return false; }
        }
        var points = FindObjectsByType<MooringPoint2D>(FindObjectsSortMode.None);
        float closest = float.PositiveInfinity;
        foreach (var point in points)
        {
            Boat candidate = point.Boat;
            if (!point.isActiveAndEnabled || candidate == null || candidate.rb == null ||
                candidate.gameObject.scene != gameObject.scene || IsLocked(candidate)) continue;
            MooringPoint2D aft = null, forward = null; bool duplicate = false;
            foreach (var other in candidate.GetComponentsInChildren<MooringPoint2D>())
            {
                if (!other.isActiveAndEnabled || other.Boat != candidate) continue;
                if (other.role == MooringPoint2D.Role.Aft) { if (aft != null) duplicate = true; aft = other; }
                else { if (forward != null) duplicate = true; forward = other; }
            }
            if (duplicate || aft == null || forward == null) continue;
            float a = Vector2.Distance(_dock[0].transform.position, aft.transform.position);
            float b = Vector2.Distance(_dock[1].transform.position, forward.transform.position);
            if (Mathf.Max(a, b) > maximumAttachDistance || a + b >= closest) continue;
            closest = a + b; _boat = candidate; _hull[0] = aft; _hull[1] = forward;
        }
        if (_boat == null) { status = "Waiting for a nearby boat with Aft and Forward moors."; return false; }
        for (int i = 0; i < 2; i++)
        {
            float separation = Vector2.Distance(_dock[i].transform.position, _hull[i].transform.position);
            if (!WorldTopology.IsFinite(separation) || !WorldTopology.IsFinite(_dock[i].ropeLength) || _dock[i].ropeLength < 0f)
            { Release(); status = "Moor position or rope length is invalid."; return false; }
            _lengths[i] = _dock[i].ropeLength > 0f ? _dock[i].ropeLength : separation + Mathf.Max(0f, slack);
            // A short authored line must not violently pull the boat into the quay on attachment.
            if (_lengths[i] < separation - .01f)
            { Release(); status = "Authored rope is shorter than moor separation. Reposition moors or increase length."; return false; }
        }
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "Mooring rope Aft" : "Mooring rope Forward");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); _lines[i] = line;
            line.sharedMaterial = ropeMaterial != null ? ropeMaterial : Resources.Load<Material>("Materials/Rope/Rope_Material");
            line.useWorldSpace = true; line.positionCount = 3; line.textureMode = LineTextureMode.Tile;
            line.startWidth = line.endWidth = ropeWidth;
            line.sortingLayerName = sortingLayer; line.sortingOrder = sortingOrder;
        }
        SyncAuthority();
        status = "Tied: Aft and Forward";
        return true;
    }

    private void FixedUpdate()
    {
        if (!IsMoored) { if (_boat != null || _lines[0] != null) Release(); return; }
        if (!_dock[0].isActiveAndEnabled || !_dock[1].isActiveAndEnabled ||
            !_hull[0].isActiveAndEnabled || !_hull[1].isActiveAndEnabled) { Release(); return; }
        SyncAuthority();
    }
    private void SyncAuthority()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!GameplayAuthority.IsAuthoritative)
            {
                if (_joints[i] != null) { _joints[i].enabled = false; Destroy(_joints[i]); _joints[i] = null; }
                continue;
            }
            if (_joints[i] != null) { _joints[i].connectedAnchor = _dock[i].transform.position; continue; }
            // Same max-distance constraint used by TetherConstraint2D; no winch payload/inventory behavior.
            var joint = _boat.gameObject.AddComponent<DistanceJoint2D>(); _joints[i] = joint;
            joint.autoConfigureConnectedAnchor = joint.autoConfigureDistance = false;
            joint.maxDistanceOnly = true; joint.enableCollision = false;
            joint.anchor = _boat.transform.InverseTransformPoint(_hull[i].transform.position);
            joint.connectedAnchor = _dock[i].transform.position; joint.distance = _lengths[i];
            joint.breakForce = joint.breakTorque = float.PositiveInfinity;
        }
    }
    private void LateUpdate()
    {
        if (!IsMoored) return;
        for (int i = 0; i < 2; i++)
        {
            Vector3 a = _dock[i].transform.position, b = _hull[i].transform.position;
            Vector3 middle = (a + b) * .5f;
            middle.y -= Mathf.Min(.5f, Mathf.Max(0f, _lengths[i] - Vector2.Distance(a, b)) * .05f);
            _lines[i].SetPosition(0, a); _lines[i].SetPosition(1, middle); _lines[i].SetPosition(2, b);
        }
    }
    private void Release()
    {
        _boat = null;
        for (int i = 0; i < 2; i++)
        {
            if (_joints[i] != null) { _joints[i].enabled = false; Destroy(_joints[i]); _joints[i] = null; }
            if (_lines[i] != null) { _lines[i].gameObject.SetActive(false); Destroy(_lines[i].gameObject); _lines[i] = null; }
            _dock[i] = _hull[i] = null;
        }
        status = "Untied";
    }
    public static bool IsLocked(Boat boat) => boat != null && Active.Exists(m => m != null && m.IsMoored && m._boat == boat);
    public static void ReportBlocked(Boat boat)
    {
        var mooring = Active.Find(m => m != null && m.IsMoored && m._boat == boat);
        if (mooring == null || Time.unscaledTime < mooring._nextMessage) return;
        mooring._nextMessage = Time.unscaledTime + 2f;
        GameMessageService.PostInfo("Cannot throttle while docked.");
    }
    public static void ReleaseForEmbark(Scene scene)
    {
        foreach (var mooring in Active)
            if (mooring != null && mooring.gameObject.scene == scene) { mooring._departing = true; mooring.Release(); }
    }
}
