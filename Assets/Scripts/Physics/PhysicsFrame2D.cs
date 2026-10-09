using UnityEngine;

/// <summary>Converts interpolated render points to the matching 2D physics pose.</summary>
public static class PhysicsFrame2D
{
    public static Vector2 ToPhysics(Rigidbody2D body, Vector2 renderedPoint)
    {
        if (body == null) return renderedPoint;
        Vector2 offset = renderedPoint - (Vector2)body.transform.position;
        float angle = (body.rotation - body.transform.eulerAngles.z) * Mathf.Deg2Rad;
        return body.position + Rotate(offset, angle);
    }

    public static Vector2 ToRender(Rigidbody2D body, Vector2 physicsPoint)
    {
        if (body == null) return physicsPoint;
        float angle = (body.transform.eulerAngles.z - body.rotation) * Mathf.Deg2Rad;
        return (Vector2)body.transform.position + Rotate(physicsPoint - body.position, angle);
    }

    public static Vector2 Point(Transform point) =>
        ToPhysics(point.GetComponentInParent<Rigidbody2D>(), point.position);

    public static Vector2 TransformPoint(Transform frame, Vector3 localPoint) =>
        ToPhysics(frame.GetComponentInParent<Rigidbody2D>(), frame.TransformPoint(localPoint));

    public static Vector3 InverseTransformPoint(Transform frame, Vector2 physicsPoint) =>
        frame.InverseTransformPoint(ToRender(frame.GetComponentInParent<Rigidbody2D>(), physicsPoint));

    public static Vector2 ClosestRenderedPoint(Collider2D collider, Vector2 renderedPoint) =>
        ToRender(collider.attachedRigidbody, collider.ClosestPoint(ToPhysics(collider.attachedRigidbody, renderedPoint)));

    private static Vector2 Rotate(Vector2 point, float angle) => new Vector2(
        point.x * Mathf.Cos(angle) - point.y * Mathf.Sin(angle),
        point.x * Mathf.Sin(angle) + point.y * Mathf.Cos(angle));
}
