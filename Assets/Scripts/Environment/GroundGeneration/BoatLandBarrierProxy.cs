using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Replaceable boat-only physics enforcement. Owns only its collider/material, never boat state.</summary>
public sealed class BoatLandBarrierProxy : IDisposable
{
    private readonly Rigidbody2D _boat;
    private readonly GameObject _root;
    private readonly BoxCollider2D _wall;
    private readonly PhysicsMaterial2D _material;
    private readonly LayerMask _hullMask;
    private readonly Collider2D[] _nearby = new Collider2D[1024];
    private readonly List<Collider2D> _hulls = new();
    private int _direction;
    private float _plane;
    public bool Active => _wall != null && _wall.enabled;

    public BoatLandBarrierProxy(Transform owner, Rigidbody2D boat, int layer, LayerMask hullMask)
    {
        _boat = boat; _hullMask = hullMask;
        _root = new GameObject("BoatGeographicBarrierRuntime"); _root.layer = layer;
        _root.transform.SetParent(owner, false);
        var body = _root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        _wall = _root.AddComponent<BoxCollider2D>(); _wall.enabled = false;
        _wall.includeLayers = hullMask; _wall.excludeLayers = ~hullMask.value;
        _wall.layerOverridePriority = 100;
        _material = new PhysicsMaterial2D("Boat geographic barrier") { friction = 0, bounciness = 0 };
        _wall.sharedMaterial = _material;
    }

    public bool Place(Vector2 axis, int direction, float allowedTravel, float thickness, float extraHeight, float clearance)
    {
        bool found = false; Bounds hull = default;
        _boat.GetComponentsInChildren(false, _hulls);
        foreach (var collider in _hulls)
        {
            if (collider.isTrigger || !collider.enabled || collider.attachedRigidbody != _boat ||
                (_hullMask.value & (1 << collider.gameObject.layer)) == 0) continue;
            if (!found) { hull = collider.bounds; found = true; } else hull.Encapsulate(collider.bounds);
        }
        if (!found) { Release(); return false; }
        axis.Normalize();
        float extent = Mathf.Abs(axis.x) * hull.extents.x + Mathf.Abs(axis.y) * hull.extents.y;
        float candidate = Vector2.Dot(hull.center, axis) + direction * (extent + Mathf.Max(clearance, allowedTravel));
        if (!Active || direction != _direction) _plane = candidate;
        else _plane = direction > 0 ? Mathf.Min(_plane, candidate) : Mathf.Max(_plane, candidate);
        _direction = direction;
        Vector2 tangent = new Vector2(-axis.y, axis.x);
        float height = 2 * (Mathf.Abs(tangent.x) * hull.extents.x + Mathf.Abs(tangent.y) * hull.extents.y) + extraHeight;
        Vector2 center = axis * (_plane + direction * thickness * .5f) + tangent * Vector2.Dot(hull.center, tangent);
        float angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        _root.transform.SetPositionAndRotation(center, Quaternion.Euler(0, 0, angle));
        _wall.size = new Vector2(thickness, height); _wall.enabled = true;
        Physics2D.SyncTransforms(); // Include just-spawned/teleported non-boat hulls before the next solver step.
        var filter = new ContactFilter2D(); filter.NoFilter();
        int count = Physics2D.OverlapBox(center, _wall.size, angle, filter, _nearby);
        if (count == _nearby.Length) { Release(); return false; } // Never enable an incompletely filtered barrier.
        for (int i = 0; i < count; i++)
        {
            var other = _nearby[i];
            if (other != null && other != _wall)
                Physics2D.IgnoreCollision(_wall, other, other.attachedRigidbody != _boat ||
                    (_hullMask.value & (1 << other.gameObject.layer)) == 0);
        }
        return true;
    }

    public void Release() { if (_wall != null) _wall.enabled = false; _direction = 0; }
    public void Dispose()
    {
        Release();
        if (Application.isPlaying) { UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_material); }
        else { UnityEngine.Object.DestroyImmediate(_root); UnityEngine.Object.DestroyImmediate(_material); }
    }
}
