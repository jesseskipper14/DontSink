using System;
using UnityEngine;

namespace MiniGames
{
    // StarObs-only debug hooks so the overlay can draw correctly.
    public interface IStarObsDebugDrawable
    {
        Vector2 GetTargetNorm01();
        Vector2 GetMouseDotNorm01();
        Vector2 GetMoveDotNorm01();

        // Visual-only overlay motion (shift + tilt)
        Vector2 GetOverlayShiftNorm01();
        float GetOverlayTiltDeg();

        float GetProgress01();
        float GetAverageQuality01();
    }

    /// <summary>
    /// Star observation with:
    /// - Mouse target follower (spring, velocity)
    /// - WASD controlled dot (accel + velocity)
    /// - Random impulses that push only player-controlled dots
    /// - Target star stays fixed except for its own drift
    /// </summary>
    public sealed class StarObservationCartridge : IMiniGameCartridge, IStarObsDebugDrawable, IOverlayDebugDrawable
    {
        private readonly Action<float, float> _onProgress; // (deltaProgress01, instantaneousQuality01)

        private MiniGameContext _ctx;

        private float _accumulated01;
        private float _qualitySum;
        private float _qualityWeightSum;

        private Vector2 _target01; // fixed object (star target), drifts slowly
        private float _noiseT;
        private int _seed;

        // Player-controlled dots (positions + velocities)
        private Vector2 _mouseDot01;
        private Vector2 _mouseVel01;

        private Vector2 _moveDot01;
        private Vector2 _moveVel01;

        // Overlay visual motion (does not affect target)
        private Vector2 _overlayShift01;
        private float _overlayShiftVel;
        private float _overlayTiltDeg;
        private float _overlayTiltVel;

        // Impulse scheduling
        private float _nextImpulseT;

        // Tuning
        private const float CompleteAt01 = 1.0f;
        private const float BaseProgressPerSecond = 0.16f;

        private const float TargetDriftSpeed = 0.25f;
        private const float TargetDriftStrength = 0.35f;

        private const float AcceptRadius = 0.35f;
        private const float PerfectRadius = 0.10f;

        // Mouse dot follows pointer with spring (no teleport)
        private const float MouseSpring = 18f;    // higher = snappier
        private const float MouseDamping = 7f;    // higher = less oscillation

        // WASD dot accel/drag
        private const float MoveAccel = 2.6f;
        private const float MoveDrag = 5.0f;

        // Impulses
        private const float ImpulseStrength = 1.25f; // velocity kick
        private const float OverlayShiftStrength = 0.20f;
        private const float OverlayTiltStrengthDeg = 9f;
        private const float OverlayDamp = 6f;

        public StarObservationCartridge()
        {

        }

        public void Begin(MiniGameContext context)
        {
            _ctx = context ?? new MiniGameContext();

            _accumulated01 = 0f;
            _qualitySum = 0f;
            _qualityWeightSum = 0f;

            _noiseT = 0f;

            _seed = _ctx.seed != 0 ? _ctx.seed : Environment.TickCount;
            var prng = new System.Random(_seed);

            _target01 = new Vector2(
                Mathf.Lerp(-0.4f, 0.4f, (float)prng.NextDouble()),
                Mathf.Lerp(-0.3f, 0.3f, (float)prng.NextDouble())
            );

            // Start player dots near center
            _mouseDot01 = Vector2.zero;
            _mouseVel01 = Vector2.zero;

            _moveDot01 = new Vector2(-0.15f, -0.10f);
            _moveVel01 = Vector2.zero;

            _overlayShift01 = Vector2.zero;
            _overlayShiftVel = 0f;
            _overlayTiltDeg = 0f;
            _overlayTiltVel = 0f;

            // first impulse in a couple seconds
            _nextImpulseT = Time.time + 2.0f;
        }

        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            // Drift target (fixed object in impulse sense)
            _noiseT += dt * TargetDriftSpeed * Mathf.Max(0.2f, _ctx.difficulty);

            var drift = new Vector2(
                Mathf.PerlinNoise(_seed * 0.001f, _noiseT) - 0.5f,
                Mathf.PerlinNoise(_seed * 0.002f, _noiseT + 17.3f) - 0.5f
            );

            _target01 += drift * (dt * TargetDriftStrength);
            _target01 = ClampNorm(_target01);

            // Apply scheduled random impulses (debug stand-in for waves/explosions)
            MaybeImpulse(dt);

            // Update mouse dot physics towards pointer
            Vector2 desired = ScreenToNorm01(input.pointer);
            // Spring force
            Vector2 toDesired = (desired - _mouseDot01);
            Vector2 accel = toDesired * MouseSpring - _mouseVel01 * MouseDamping;
            _mouseVel01 += accel * dt;
            _mouseDot01 += _mouseVel01 * dt;
            _mouseDot01 = ClampNorm(_mouseDot01);

            // Update WASD dot physics
            Vector2 move = input.move;
            Vector2 moveAccel = move * MoveAccel;
            _moveVel01 += moveAccel * dt;
            // drag
            _moveVel01 = Vector2.Lerp(_moveVel01, Vector2.zero, 1f - Mathf.Exp(-MoveDrag * dt));
            _moveDot01 += _moveVel01 * dt;
            _moveDot01 = ClampNorm(_moveDot01);

            // Compute quality based on aligning BOTH dots to target.
            float distMouse = Vector2.Distance(_mouseDot01, _target01);
            float distMove = Vector2.Distance(_moveDot01, _target01);

            float qMouse = QualityFromDist(distMouse);
            float qMove = QualityFromDist(distMove);

            // Combine (multiplicative is harsher; min is simplest; average is forgiving)
            // We'll use MIN: the worse one dominates, encourages aligning both without being cruel.
            float instQuality01 = Mathf.Min(qMouse, qMove);

            // Progress only while "observing"
            if (input.actionHeld && instQuality01 > 0f)
            {
                float delta = BaseProgressPerSecond * instQuality01 * dt;
                _accumulated01 = Mathf.Clamp01(_accumulated01 + delta);

                _qualitySum += instQuality01 * delta;
                _qualityWeightSum += delta;

                _ctx.emitEffect?.Invoke(
                    MiniGameEffect.Progress(
                        system: "StarMap",
                        targetId: _ctx.targetId,
                        delta01: delta,
                        quality01: instQuality01
                    )
                );
            }

            if (_accumulated01 >= CompleteAt01)
            {
                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.Completed,
                    quality01 = GetAverageQuality(),
                    note = $"Observed avgQ:{GetAverageQuality():0.00}",
                    hasMeaningfulProgress = true
                };
            }

            return new MiniGameResult
            {
                outcome = MiniGameOutcome.None,
                quality01 = GetAverageQuality(),
                note = null,
                hasMeaningfulProgress = _accumulated01 > 0.001f
            };
        }

        public MiniGameResult Cancel()
        {
            bool partial = _accumulated01 > 0.001f;

            return new MiniGameResult
            {
                outcome = partial ? MiniGameOutcome.Partial : MiniGameOutcome.Cancelled,
                quality01 = GetAverageQuality(),
                note = partial ? $"Partial: {_accumulated01:0.00}" : "Cancelled",
                hasMeaningfulProgress = partial
            };
        }

        public MiniGameResult Interrupt(string reason)
        {
            bool partial = _accumulated01 > 0.001f;

            return new MiniGameResult
            {
                outcome = partial ? MiniGameOutcome.Partial : MiniGameOutcome.Cancelled,
                quality01 = GetAverageQuality(),
                note = $"Interrupted: {reason}",
                hasMeaningfulProgress = partial
            };
        }

        public void End()
        {
            _ctx = null;
        }

        private void MaybeImpulse(float dt)
        {
            // Use Time.time because it’s fine for debug.
            if (Time.time < _nextImpulseT) return;

            // schedule next impulse: every ~2–5 seconds, scaled by difficulty/pressure later
            float interval = UnityEngine.Random.Range(2.0f, 5.0f);
            _nextImpulseT = Time.time + interval;

            // impulse direction
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            float strength = ImpulseStrength * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(_ctx.difficulty));

            // Player-controlled objects get velocity kick
            _mouseVel01 += dir * strength;
            _moveVel01 += dir * strength * 0.9f;

            // Overlay visual tilt/shift
            _overlayShift01 += dir * OverlayShiftStrength;
            _overlayTiltVel += UnityEngine.Random.Range(-1f, 1f) * OverlayTiltStrengthDeg;

            // Dampen overlay motion gradually (in Tick we’ll apply damping)
            _overlayShift01 = ClampNorm(_overlayShift01);

            // Apply damping continuously here too (so impulses don't stack forever)
            float d = 1f - Mathf.Exp(-OverlayDamp * dt);
            _overlayShift01 = Vector2.Lerp(_overlayShift01, Vector2.zero, d);
            _overlayTiltDeg = Mathf.Lerp(_overlayTiltDeg, 0f, d);
        }

        private static Vector2 ClampNorm(Vector2 p)
        {
            p.x = Mathf.Clamp(p.x, -0.85f, 0.85f);
            p.y = Mathf.Clamp(p.y, -0.85f, 0.85f);
            return p;
        }

        private static float QualityFromDist(float dist)
        {
            if (dist > AcceptRadius) return 0f;
            if (dist <= PerfectRadius) return 1f;

            float t = Mathf.InverseLerp(AcceptRadius, PerfectRadius, dist);
            return Mathf.Clamp01(t);
        }

        private float GetAverageQuality()
        {
            if (_qualityWeightSum <= 0f) return 0f;
            return Mathf.Clamp01(_qualitySum / _qualityWeightSum);
        }

        private static Vector2 ScreenToNorm01(Vector2 screenPx)
        {
            float x = (screenPx.x / Mathf.Max(1f, Screen.width)) * 2f - 1f;
            float y = (1f - (screenPx.y / Mathf.Max(1f, Screen.height))) * 2f - 1f; // top-down -> -1..1
            return new Vector2(x, y);
        }

        // Debug draw interface
        public Vector2 GetTargetNorm01() => _target01;
        public Vector2 GetMouseDotNorm01() => _mouseDot01;
        public Vector2 GetMoveDotNorm01() => _moveDot01;
        public Vector2 GetOverlayShiftNorm01() => _overlayShift01;
        public float GetOverlayTiltDeg() => _overlayTiltDeg;
        public float GetProgress01() => _accumulated01;
        public float GetAverageQuality01() => GetAverageQuality();
    }
}
