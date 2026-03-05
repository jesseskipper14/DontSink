//using System;
//using System.Collections.Generic;
//using UnityEngine;

//// DEPRECATED

//namespace Survival.Vitals
//{
//    [DisallowMultipleComponent]
//    public sealed class PlayerVitalModel : MonoBehaviour
//    {
//        [Header("Tick")]
//        [SerializeField] private bool useFixedUpdate = true;

//        [Header("Brain Viability")]
//        [Tooltip("Below this delivery, we accumulate oxygen debt.")]
//        [Range(0f, 1f)]
//        [SerializeField] private float deliveryThreshold = 0.35f;

//        [Tooltip("How fast debt increases when delivery is at 0. Scales down as delivery approaches threshold.")]
//        [SerializeField] private float debtGainPerSecond = 0.35f;

//        [Tooltip("How fast debt recovers when delivery is above threshold. Intentionally slower than gain.")]
//        [SerializeField] private float debtRecoveryPerSecond = 0.10f;

//        [Tooltip("Optional: require sustained good delivery before meaningful recovery starts.")]
//        [SerializeField] private float recoveryWarmupSeconds = 1.0f;

//        [Header("State Thresholds (Debt)")]
//        [Range(0f, 1f)]
//        [SerializeField] private float hypoxiaDebt = 0.15f;
//        [Range(0f, 1f)]
//        [SerializeField] private float unconsciousDebt = 0.65f;
//        [Range(0f, 1f)]
//        [SerializeField] private float deathDebt = 1.0f;

//        [Header("Optional symptom shaping")]
//        [SerializeField] private AnimationCurve hypoxiaSeverityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

//        public VitalSignals Signals { get; private set; }

//        public event Action BecameHypoxic;
//        public event Action BecameUnconscious;
//        public event Action Died;

//        private readonly List<IVitalContributor> _contributors = new();
//        private VitalContext _ctx = new VitalContext();

//        private float _debt01;
//        private float _goodDeliveryTimer;

//        private bool _prevHypoxic;
//        private bool _prevUnconscious;
//        private bool _prevDead;

//        private void Awake()
//        {
//            GetComponentsInChildren(true, _contributors);
//        }

//        private void OnEnable()
//        {
//            // refresh in case mods add/remove at runtime
//            _contributors.Clear();
//            GetComponentsInChildren(true, _contributors);
//        }

//        private void FixedUpdate()
//        {
//            if (!useFixedUpdate) return;
//            Tick(Time.fixedDeltaTime);
//        }

//        private void Update()
//        {
//            if (useFixedUpdate) return;
//            Tick(Time.deltaTime);
//        }

//        private void Tick(float dt)
//        {
//            if (dt <= 0f) return;

//            // Grab current snapshot so we can safely mutate it.
//            var s = Signals;

//            // 1) Build context
//            _ctx.oxygenSupply01 = 1f;
//            _ctx.oxygenTransport01 = 1f;
//            _ctx.perfusion01 = 1f;
//            _ctx.brainDemandMul = 1f;
//            _ctx.debtRecoveryMul = 1f;

//            for (int i = 0; i < _contributors.Count; i++)
//                _contributors[i].Contribute(ref _ctx, dt);

//            _ctx.Clamp01();

//            // 2) Compute delivery (simple multiplicative pipeline)
//            float delivery01 = (_ctx.oxygenSupply01 * _ctx.oxygenTransport01 * _ctx.perfusion01) / Mathf.Max(0.1f, _ctx.brainDemandMul);
//            delivery01 = Mathf.Clamp01(delivery01);

//            // 3) Debt evolution with inertia (non-gameable)
//            bool goodDelivery = delivery01 >= deliveryThreshold;

//            if (!goodDelivery)
//            {
//                _goodDeliveryTimer = 0f;
//                float deficit01 = 1f - Mathf.InverseLerp(0f, deliveryThreshold, delivery01);
//                float gain = debtGainPerSecond * deficit01 * dt;
//                _debt01 = Mathf.Clamp01(_debt01 + gain);

//                s.timeHypoxic += dt;        // ✅ mutate local copy
//            }
//            else
//            {
//                _goodDeliveryTimer += dt;
//                float warmup01 = Mathf.Clamp01(_goodDeliveryTimer / Mathf.Max(0.001f, recoveryWarmupSeconds));
//                float recoveryRate = debtRecoveryPerSecond * _ctx.debtRecoveryMul * warmup01;
//                _debt01 = Mathf.Clamp01(_debt01 - recoveryRate * dt);
//            }

//            // 4) State flags
//            bool hypoxic = _debt01 >= hypoxiaDebt;
//            bool unconscious = _debt01 >= unconsciousDebt;
//            bool dead = _debt01 >= deathDebt;

//            if (unconscious) s.timeUnconscious += dt;

//            // 5) Fill signals snapshot
//            s.brainOxygenDelivery01 = delivery01;
//            s.brainOxygenDebt01 = _debt01;

//            float sev = hypoxiaSeverityCurve != null ? hypoxiaSeverityCurve.Evaluate(_debt01) : _debt01;
//            s.hypoxiaSeverity01 = Mathf.Clamp01(sev);

//            s.isHypoxic = hypoxic;
//            s.isUnconscious = unconscious;
//            s.isDead = dead;

//            // 6) Edge events
//            if (!_prevHypoxic && hypoxic) BecameHypoxic?.Invoke();
//            if (!_prevUnconscious && unconscious) BecameUnconscious?.Invoke();
//            if (!_prevDead && dead) Died?.Invoke();

//            _prevHypoxic = hypoxic;
//            _prevUnconscious = unconscious;
//            _prevDead = dead;

//            // ✅ commit snapshot back to property
//            Signals = s;
//        }

//        /// <summary>
//        /// Use for debug/devtools only. Don't let gameplay call this casually.
//        /// </summary>
//        public void Debug_SetDebt01(float v) => _debt01 = Mathf.Clamp01(v);
//    }
//}