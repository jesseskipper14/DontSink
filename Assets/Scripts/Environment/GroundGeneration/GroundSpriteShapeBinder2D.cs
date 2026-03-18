using System;
using System.Collections;
using UnityEngine;
using UnityEngine.U2D;

[RequireComponent(typeof(SpriteShapeController))]
public sealed class GroundSpriteShapeBinder2D : MonoBehaviour, IGroundFillBottomSource
{
    [Header("Refs")]
    [SerializeField] private MonoBehaviour generatorSource; // must implement IGroundGeneratedNotifier
    [SerializeField] private EdgeCollider2D edge;

    [Header("Fill")]
    public float fillBottomY = -30f;

    [Tooltip("Minimum visual ground thickness below the deepest generated terrain point.")]
    [Min(0f)] public float extraFillDepth = 25f;

    public bool decimate = true;
    [Min(1)] public int decimateStep = 2;

    [Header("Spline Tangents")]
    [Min(0f)] public float tangentStrength = 0.35f;

    private SpriteShapeController _ssc;
    private Coroutine _pending;
    private IGroundGeneratedNotifier _generator;

    public float LastUsedBottomY { get; private set; }

    public event Action<float> OnBottomYChanged;

    private void Awake()
    {
        _ssc = GetComponent<SpriteShapeController>();

        if (generatorSource == null)
            generatorSource = FindGeneratorSource();

        _generator = generatorSource as IGroundGeneratedNotifier;

        if (edge == null && generatorSource != null)
            edge = generatorSource.GetComponent<EdgeCollider2D>();

        if (edge == null)
            edge = GetComponentInParent<EdgeCollider2D>();
    }

    private void OnEnable()
    {
        if (_generator != null)
            _generator.OnGenerated += HandleGenerated;

        ScheduleRebuild();
    }

    private void OnDisable()
    {
        if (_generator != null)
            _generator.OnGenerated -= HandleGenerated;

        if (_pending != null)
        {
            StopCoroutine(_pending);
            _pending = null;
        }
    }

    private MonoBehaviour FindGeneratorSource()
    {
        MonoBehaviour[] sources = GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] is IGroundGeneratedNotifier)
                return sources[i];
        }

        return null;
    }

    private void HandleGenerated()
    {
        ScheduleRebuild();
    }

    private void ScheduleRebuild()
    {
        if (_pending != null) StopCoroutine(_pending);
        _pending = StartCoroutine(RebuildNextFrame());
    }

    private IEnumerator RebuildNextFrame()
    {
        yield return null;
        Rebuild();
        _pending = null;
    }

    [ContextMenu("Rebuild SpriteShape From Ground")]
    public void Rebuild()
    {
        if (_ssc == null) _ssc = GetComponent<SpriteShapeController>();
        if (edge == null) return;

        var pts = edge.points;
        if (pts == null || pts.Length < 2) return;

        float minY = pts[0].y;
        for (int i = 1; i < pts.Length; i++)
            minY = Mathf.Min(minY, pts[i].y);

        float safeBottomY = Mathf.Min(fillBottomY, minY - extraFillDepth);

        LastUsedBottomY = safeBottomY;
        OnBottomYChanged?.Invoke(LastUsedBottomY);

        var spline = _ssc.spline;
        spline.Clear();
        spline.isOpenEnded = false;

        for (int i = 0; i < pts.Length; i++)
        {
            if (decimate && i % decimateStep != 0 && i != pts.Length - 1)
                continue;

            int idx = spline.GetPointCount();
            spline.InsertPointAt(idx, pts[i]);
            spline.SetTangentMode(idx, ShapeTangentMode.Continuous);
            spline.SetLeftTangent(idx, Vector3.left * tangentStrength);
            spline.SetRightTangent(idx, Vector3.right * tangentStrength);
        }

        Vector3 last = spline.GetPosition(spline.GetPointCount() - 1);
        Vector3 first = spline.GetPosition(0);

        int br = spline.GetPointCount();
        spline.InsertPointAt(br, new Vector3(last.x, safeBottomY, 0f));
        spline.SetTangentMode(br, ShapeTangentMode.Linear);

        int bl = spline.GetPointCount();
        spline.InsertPointAt(bl, new Vector3(first.x, safeBottomY, 0f));
        spline.SetTangentMode(bl, ShapeTangentMode.Linear);

        _ssc.RefreshSpriteShape();
    }
}