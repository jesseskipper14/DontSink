using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnderwaterResourceFrozenBody2D : MonoBehaviour
{
    [Header("Freeze")]
    [SerializeField] private bool freezeOnAwake = false;

    [Tooltip("Resources usually want trigger colliders so the player can overlap/interact without physics shoving.")]
    [SerializeField] private bool forceCollidersToTriggers = true;

    private Rigidbody2D rb;

    private bool capturedOriginal;
    private RigidbodyType2D originalBodyType;
    private RigidbodyConstraints2D originalConstraints;
    private float originalGravityScale;
    private bool originalSimulated;
    private CollisionDetectionMode2D originalCollisionDetectionMode;
    private RigidbodySleepMode2D originalSleepMode;
    private RigidbodyInterpolation2D originalInterpolation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (freezeOnAwake)
            FreezeNow();
    }

    public void FreezeNow()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            if (forceCollidersToTriggers)
                ForceTriggerColliders();

            return;
        }

        CaptureOriginalIfNeeded();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        rb.simulated = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        rb.sleepMode = RigidbodySleepMode2D.StartAsleep;
        rb.interpolation = RigidbodyInterpolation2D.None;

        rb.Sleep();

        if (forceCollidersToTriggers)
            ForceTriggerColliders();
    }

    public void RestoreOriginalPhysics()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb == null || !capturedOriginal)
            return;

        rb.bodyType = originalBodyType;
        rb.constraints = originalConstraints;
        rb.gravityScale = originalGravityScale;
        rb.simulated = originalSimulated;
        rb.collisionDetectionMode = originalCollisionDetectionMode;
        rb.sleepMode = originalSleepMode;
        rb.interpolation = originalInterpolation;

        rb.WakeUp();
    }

    private void CaptureOriginalIfNeeded()
    {
        if (capturedOriginal || rb == null)
            return;

        capturedOriginal = true;

        originalBodyType = rb.bodyType;
        originalConstraints = rb.constraints;
        originalGravityScale = rb.gravityScale;
        originalSimulated = rb.simulated;
        originalCollisionDetectionMode = rb.collisionDetectionMode;
        originalSleepMode = rb.sleepMode;
        originalInterpolation = rb.interpolation;
    }

    private void ForceTriggerColliders()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].isTrigger = true;
    }
}