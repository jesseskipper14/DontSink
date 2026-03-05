using UnityEngine;
using Survival.Afflictions;

namespace Survival.Vitals
{
    public interface IConsciousnessRead
    {
        bool IsUnconscious { get; }
        float Unconscious01 { get; } // 0..1 (fade)
    }

    /// <summary>
    /// Affliction-driven unconsciousness.
    /// This class does NOT read oxygenation directly.
    /// It reacts to presence of an affliction (e.g. "aff.unconscious") from the affliction system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerUnconsciousnessState : MonoBehaviour, IConsciousnessRead
    {
        public enum State
        {
            Awake,
            FadingOut,
            Unconscious,
            Waking
        }

        [Header("Refs")]
        [Tooltip("Component that implements IAfflictionRead (usually your AfflictionSystem).")]
        [SerializeField] private MonoBehaviour afflictionsReadBehaviour; // IAfflictionRead

        [Tooltip("Intent source to disable while unconscious (your LocalCharacterIntentSource, etc).")]
        [SerializeField] private Behaviour intentSourceBehaviour;

        [Tooltip("Optional: body to damp while unconscious.")]
        [SerializeField] private Rigidbody2D body;

        [Header("Affliction IDs")]
        [SerializeField] private string unconsciousId = "aff.unconscious";

        [Header("Timing")]
        [Tooltip("Minimum time spent unconscious once triggered (prevents instant flicker).")]
        [Min(0f)] public float minUnconsciousSeconds = 2.5f;

        [Tooltip("Fade speed for blacking out / waking up (higher = faster).")]
        [Min(0.1f)] public float fadeRate = 3.0f;

        [Header("Unconscious Physics")]
        [Tooltip("If >0, exponentially damps linear velocity while unconscious.")]
        [Min(0f)] public float linearDamp = 2.5f;

        [Tooltip("If >0, exponentially damps angular velocity while unconscious.")]
        [Min(0f)] public float angularDamp = 4.0f;

        [Tooltip("If enabled, snaps near-zero velocities to exactly zero.")]
        public bool snapToStop = true;

        [Min(0f)] public float stopEpsilon = 0.03f;

        public State Current { get; private set; } = State.Awake;

        public bool IsUnconscious => Current == State.FadingOut || Current == State.Unconscious;
        public float Unconscious01 { get; private set; } // 0..1

        public System.Action<bool> OnUnconsciousChanged;

        private IAfflictionRead Aff => afflictionsReadBehaviour as IAfflictionRead;

        private float _unconsciousTimer;
        private bool _intentWasEnabled;

        private void Reset()
        {
            body = GetComponentInChildren<Rigidbody2D>();
        }

        private void Awake()
        {
            // Best-effort auto-find. Still recommend explicit assignment for sanity.
            if (afflictionsReadBehaviour == null)
                afflictionsReadBehaviour = GetComponentInChildren<MonoBehaviour>(); // assign properly in inspector

            if (intentSourceBehaviour == null)
                intentSourceBehaviour = GetComponentInChildren<Behaviour>();

            if (body == null)
                body = GetComponentInChildren<Rigidbody2D>();
        }

        private void OnEnable()
        {
            // Ensure we don't “resume” in a weird half-state.
            if (Aff != null && Aff.Has(unconsciousId))
                ForceUnconsciousImmediate();
            else
                ForceAwakeImmediate();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool shouldBeUnconscious = (Aff != null) && Aff.Has(unconsciousId);
            Tick(dt, shouldBeUnconscious);
        }

        private void Tick(float dt, bool shouldBeUnconscious)
        {
            switch (Current)
            {
                case State.Awake:
                    {
                        Unconscious01 = MoveTowardExp(Unconscious01, 0f, fadeRate, dt);

                        if (shouldBeUnconscious)
                            EnterFadingOut();
                        break;
                    }

                case State.FadingOut:
                    {
                        Unconscious01 = MoveTowardExp(Unconscious01, 1f, fadeRate, dt);

                        if (Unconscious01 >= 0.99f)
                            EnterUnconscious();
                        break;
                    }

                case State.Unconscious:
                    {
                        _unconsciousTimer += dt;

                        ApplyUnconsciousDamping(dt);

                        // If affliction cleared, allow wake, but honor minimum unconscious time.
                        if (!shouldBeUnconscious && _unconsciousTimer >= minUnconsciousSeconds)
                            EnterWaking();
                        break;
                    }

                case State.Waking:
                    {
                        // If affliction returns while waking, go back out.
                        if (shouldBeUnconscious)
                        {
                            EnterFadingOut();
                            break;
                        }

                        Unconscious01 = MoveTowardExp(Unconscious01, 0f, fadeRate, dt);

                        if (Unconscious01 <= 0.01f)
                            EnterAwake();
                        break;
                    }
            }
        }

        private void EnterFadingOut()
        {
            if (Current == State.FadingOut || Current == State.Unconscious) return;

            Current = State.FadingOut;
            SetIntentEnabled(false);
            OnUnconsciousChanged?.Invoke(true);
        }

        private void EnterUnconscious()
        {
            Current = State.Unconscious;
            _unconsciousTimer = 0f;

            // Optional: kill “active” motion at the moment of passing out.
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void EnterWaking()
        {
            Current = State.Waking;
        }

        private void EnterAwake()
        {
            Current = State.Awake;
            SetIntentEnabled(_intentWasEnabled);
            OnUnconsciousChanged?.Invoke(false);
        }

        private void ForceUnconsciousImmediate()
        {
            Current = State.Unconscious;
            Unconscious01 = 1f;
            _unconsciousTimer = 999f; // already satisfied min time
            SetIntentEnabled(false);
            OnUnconsciousChanged?.Invoke(true);
        }

        private void ForceAwakeImmediate()
        {
            Current = State.Awake;
            Unconscious01 = 0f;
            _unconsciousTimer = 0f;
            SetIntentEnabled(true);
            OnUnconsciousChanged?.Invoke(false);
        }

        private void SetIntentEnabled(bool enabled)
        {
            if (intentSourceBehaviour == null) return;

            if (!enabled)
                _intentWasEnabled = intentSourceBehaviour.enabled;

            intentSourceBehaviour.enabled = enabled;
        }

        private void ApplyUnconsciousDamping(float dt)
        {
            if (body == null) return;

            if (linearDamp > 0f)
                body.linearVelocity *= Mathf.Exp(-linearDamp * dt);

            if (angularDamp > 0f)
                body.angularVelocity *= Mathf.Exp(-angularDamp * dt);

            if (snapToStop)
            {
                if (body.linearVelocity.sqrMagnitude < stopEpsilon * stopEpsilon)
                    body.linearVelocity = Vector2.zero;

                if (Mathf.Abs(body.angularVelocity) < stopEpsilon)
                    body.angularVelocity = 0f;
            }
        }

        private static float MoveTowardExp(float current, float target, float ratePerSecond, float dt)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, ratePerSecond) * dt);
            return Mathf.Lerp(current, target, t);
        }
    }
}