using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Temporary diagnostic component for finding where boat velocity is being lost.
/// Add this to the Boat root. It does not change physics.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoatPhysicsContactDiagnostics : MonoBehaviour
{
    [SerializeField] private Boat boat;

    [Header("Logging")]
    [SerializeField] private bool logBodyInventoryOnStart = true;
    [SerializeField] private bool logContacts = true;
    [SerializeField, Min(0.1f)] private float interval = 0.5f;
    [SerializeField, Min(1)] private int maxContactsPerBody = 12;

    private float _nextLogTime;
    private readonly ContactPoint2D[] _contacts = new ContactPoint2D[64];

    private void Awake()
    {
        ResolveBoat();

        if (logBodyInventoryOnStart)
            LogBodyInventory();
    }

    private void FixedUpdate()
    {
        if (!logContacts)
            return;

        if (Time.unscaledTime < _nextLogTime)
            return;

        _nextLogTime =
            Time.unscaledTime +
            Mathf.Max(0.1f, interval);

        ResolveBoat();

        if (boat == null)
        {
            Debug.LogWarning(
                "[BoatPhysicsContactDiagnostics] Boat not found.",
                this);
            return;
        }

        LogPhysicsSnapshot();
    }

    [ContextMenu("Log Rigidbody Inventory")]
    public void LogBodyInventory()
    {
        ResolveBoat();

        if (boat == null)
            return;

        Rigidbody2D[] bodies =
            boat.GetComponentsInChildren<Rigidbody2D>(true);

        Collider2D[] allCols =
            boat.GetComponentsInChildren<Collider2D>(true);

        var sb = new StringBuilder();

        sb.AppendLine(
            $"[BoatPhysicsContactDiagnostics] RIGIDBODY INVENTORY boat='{boat.name}' count={bodies.Length}");

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null)
                continue;

            int attachedColliderCount = 0;

            for (int c = 0; c < allCols.Length; c++)
            {
                if (allCols[c] != null &&
                    allCols[c].attachedRigidbody == rb)
                {
                    attachedColliderCount++;
                }
            }

            sb.AppendLine(
                $"  [{i}] path='{GetPath(boat.transform, rb.transform)}' " +
                $"type={rb.bodyType} simulated={rb.simulated} " +
                $"mass={rb.mass:0.###} gravity={rb.gravityScale:0.###} " +
                $"linearDamping={rb.linearDamping:0.###} angularDamping={rb.angularDamping:0.###} " +
                $"constraints={rb.constraints} attachedColliders={attachedColliderCount}");
        }

        Debug.Log(sb.ToString(), this);
    }

    private void LogPhysicsSnapshot()
    {
        Rigidbody2D rootRb = boat.rb;

        var sb = new StringBuilder();

        sb.Append(
            $"[BoatPhysicsContactDiagnostics] SNAP " +
            $"rootPos=({rootRb.position.x:0.000},{rootRb.position.y:0.000}) " +
            $"rootVel=({rootRb.linearVelocity.x:+0.000;-0.000;0.000}," +
                     $"{rootRb.linearVelocity.y:+0.000;-0.000;0.000}) " +
            $"rot={rootRb.rotation:+0.0;-0.0;0.0} " +
            $"angVel={rootRb.angularVelocity:+0.00;-0.00;0.00} " +
            $"mass={rootRb.mass:0.###} awake={rootRb.IsAwake()}");

        AppendContactsForBody(
            sb,
            rootRb,
            "ROOT");

        Rigidbody2D[] bodies =
            boat.GetComponentsInChildren<Rigidbody2D>(true);

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];

            if (rb == null ||
                rb == rootRb ||
                !rb.simulated)
            {
                continue;
            }

            int count =
                rb.GetContacts(_contacts);

            if (count <= 0)
                continue;

            AppendContactsForBody(
                sb,
                rb,
                $"CHILD '{GetPath(boat.transform, rb.transform)}'");
        }

        Debug.Log(sb.ToString(), this);
    }

    private void AppendContactsForBody(
        StringBuilder sb,
        Rigidbody2D rb,
        string label)
    {
        int count =
            rb.GetContacts(_contacts);

        sb.Append(
            $" | {label} contacts={count}");

        int limit =
            Mathf.Min(
                count,
                Mathf.Min(
                    maxContactsPerBody,
                    _contacts.Length));

        for (int i = 0; i < limit; i++)
        {
            ContactPoint2D cp =
                _contacts[i];

            Collider2D other =
                cp.otherCollider;

            string otherName =
                other != null
                    ? other.name
                    : "NULL";

            string otherLayer =
                other != null
                    ? LayerMask.LayerToName(
                        other.gameObject.layer)
                    : "?";

            Rigidbody2D otherRb =
                other != null
                    ? other.attachedRigidbody
                    : null;

            string otherBody =
                otherRb != null
                    ? $"{otherRb.bodyType}:{otherRb.name}"
                    : "Static";

            sb.Append(
                $" || #{i} other='{otherName}' " +
                $"layer={otherLayer} body={otherBody} " +
                $"point=({cp.point.x:0.00},{cp.point.y:0.00}) " +
                $"normal=({cp.normal.x:+0.00;-0.00;0.00}," +
                        $"{cp.normal.y:+0.00;-0.00;0.00}) " +
                $"normalImpulse={cp.normalImpulse:0.00} " +
                $"tangentImpulse={cp.tangentImpulse:0.00}");
        }
    }

    private void ResolveBoat()
    {
        if (boat == null)
            boat =
                GetComponent<Boat>() ??
                GetComponentInParent<Boat>();
    }

    private static string GetPath(
        Transform root,
        Transform target)
    {
        if (target == null)
            return "(null)";

        if (root == null ||
            target == root)
        {
            return target.name;
        }

        var names =
            new List<string>();

        Transform t = target;

        while (t != null &&
               t != root)
        {
            names.Add(t.name);
            t = t.parent;
        }

        names.Reverse();

        return root.name +
               "/" +
               string.Join("/", names);
    }
}