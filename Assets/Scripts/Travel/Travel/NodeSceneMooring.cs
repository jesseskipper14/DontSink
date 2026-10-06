using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One visual rope between authored quay/boat markers. Never moves or constrains the hull.</summary>
[DisallowMultipleComponent]
public sealed class NodeSceneMooring : MonoBehaviour
{
    public string nodeSceneName = "NodeScene";
    [Tooltip("Discover the quay marker under this root. Empty uses this object's children.")]
    public Transform dockRoot;
    public Material ropeMaterial;
    [Min(0f)] public float slack = .5f;
    [Min(.01f)] public float ropeWidth = .075f;
    [Min(.1f)] public float maximumAttachDistance = 60f;
    public string sortingLayer = "WorldDock";
    public int sortingOrder = 5;
    [SerializeField] private string status = "Waiting for authored moors";
    public string Status => status;
    public bool IsMoored => _boat != null && _dock != null && _hull != null &&
        _dock.isActiveAndEnabled && _hull.isActiveAndEnabled && _line != null && !_departing;
    private Boat _boat;
    private MooringPoint2D _dock, _hull;
    private LineRenderer _line;
    private float _nextSearch, _nextMessage;
    private bool _departing;
    private static readonly List<NodeSceneMooring> Active = new();

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); _departing = false; _nextSearch = 0f; }
    private void OnDisable() { Active.Remove(this); Release(); }
    private void Update()
    {
        if (_departing || gameObject.scene.name != nodeSceneName) return;
        if (IsMoored) return;
        if (_boat != null || _line != null) Release();
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
        if (!WorldTopology.IsFinite(slack) || slack < 0f || !WorldTopology.IsFinite(ropeWidth) || ropeWidth <= 0f ||
            !WorldTopology.IsFinite(maximumAttachDistance) || maximumAttachDistance <= 0f)
        { status = "Use finite, nonnegative slack and positive width/attach distance."; return false; }
        var root = dockRoot != null ? dockRoot : transform;
        if (root.gameObject.scene != gameObject.scene) { status = "Quay root must be in the same scene."; return false; }
        foreach (var point in root.GetComponentsInChildren<MooringPoint2D>())
        {
            if (!point.isActiveAndEnabled || point.Boat != null) continue;
            if (_dock != null) { _dock = null; status = "Multiple quay moors: assign Dock Root to the single marker."; return false; }
            _dock = point;
        }
        if (_dock == null) { status = "Waiting for one quay moor."; return false; }
        float closest = maximumAttachDistance;
        foreach (var point in FindObjectsByType<MooringPoint2D>(FindObjectsSortMode.None))
        {
            Boat candidate = point.Boat;
            if (!point.isActiveAndEnabled || candidate == null || candidate.gameObject.scene != gameObject.scene || IsLocked(candidate)) continue;
            float distance = Vector2.Distance(_dock.transform.position, point.transform.position);
            if (!WorldTopology.IsFinite(distance) || distance > closest) continue;
            closest = distance; _boat = candidate; _hull = point;
        }
        if (_boat == null) { _dock = null; status = "Waiting for a nearby boat moor."; return false; }
        var go = new GameObject("Mooring rope");
        go.transform.SetParent(transform, false);
        _line = go.AddComponent<LineRenderer>();
        _line.sharedMaterial = ropeMaterial != null ? ropeMaterial : Resources.Load<Material>("Materials/Rope/Rope_Material");
        _line.useWorldSpace = true; _line.positionCount = 17; _line.textureMode = LineTextureMode.Tile;
        _line.startWidth = _line.endWidth = ropeWidth;
        _line.sortingLayerName = sortingLayer; _line.sortingOrder = sortingOrder;
        status = "Tied: one visual rope";
        LateUpdate();
        return true;
    }

    private void LateUpdate()
    {
        if (!IsMoored) return;
        Vector3 a = _dock.transform.position, b = _hull.transform.position;
        float sag = Mathf.Min(.5f, slack * .25f);
        for (int i = 0; i < _line.positionCount; i++)
        {
            float t = i / (float)(_line.positionCount - 1);
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= 4f * t * (1f - t) * sag;
            _line.SetPosition(i, p);
        }
    }
    private void Release()
    {
        _boat = null; _dock = _hull = null;
        if (_line != null) { _line.gameObject.SetActive(false); Destroy(_line.gameObject); _line = null; }
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
