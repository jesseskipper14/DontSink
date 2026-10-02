using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniGames
{
    /// <summary>
    /// Phase 4 celestial observation iteration 2.
    ///
    /// The instrument is intentionally fallible. The real deterministic sky is viewed through a
    /// circular aperture with a hidden rotation offset. Optical calibration is a separate,
    /// cross-coupled registration puzzle: three controls move several calibration indicators at
    /// once, so there is no scalar "maximize this bar" solution. Once the optics are seated, the
    /// decoder reveals a canonical (rotation-free) shape made only from landmark stars. The player
    /// keeps full control of telescope rotation and must rotate/search the real sky, then transcribe
    /// that landmark pattern by connecting the corresponding stars.
    ///
    /// Completing the puzzle builds CelestialObservation evidence, then asks the runner/authority
    /// boundary to commit it. The cartridge only marks itself complete after that commit succeeds.
    /// </summary>
    public sealed partial class CelestialObservationCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private enum ObservationStage
        {
            Calibration = 0,
            TracePattern = 1,
            ReadyToRecord = 2
        }

        private sealed class ProjectedObject
        {
            public CelestialObject celestialObject;
            // Shape-preserving metric viewport used for display/decoder/geometry.
            public Vector2 baseViewport01;
            // Legacy authored query normalization used ONLY to preserve the Iteration-9 target-safe
            // region and therefore keep the already-tuned survey difficulty/candidate envelope.
            public Vector2 selectionViewport01;
            public Vector2 instrumentViewport01;
            public Vector2 screenPoint;
            public bool insideAperture;
        }

        private readonly CelestialField _field;
        private readonly Vector2 _observerWorldPosition;
        private readonly CelestialSkyProjectionSettings _projectionSettings;
        private readonly CelestialObservationSettings _observationSettings;
        private readonly Func<float> _starVisibilityProvider;
        private readonly Func<TimeOfDayManager> _timeManagerProvider;
        private readonly Func<CelestialObservation, string> _tryCommitObservation;
        private readonly int _surveySequence;

        private readonly List<CelestialObject> _queriedObjects = new List<CelestialObject>();
        private readonly List<ProjectedObject> _projectedObjects = new List<ProjectedObject>();
        private readonly List<ProjectedObject> _patternObjects = new List<ProjectedObject>();
        private readonly List<string> _patternIds = new List<string>();
        private readonly List<string> _traceIds = new List<string>();
        private readonly List<Vector2> _decoderBasePoints = new List<Vector2>();
        private readonly List<Vector2> _decoderCurrentPoints = new List<Vector2>();

        // Evidence swept through the calibrated aperture during this observation. Context stars are
        // accumulated across telescope pans instead of being sampled only from the final frame.
        // The stored coordinate is where the object was FIRST actually observed inside the aperture.
        private readonly Dictionary<string, Vector2> _observedInstrumentPositions =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);

        private Vector2 _decoderReferenceBasePoint;
        private Vector2 _decoderReferenceCurrentPoint;
        private bool _hasDecoderReferencePoint;

        private MiniGameContext _context;
        private Rect _baseVisibleWorldRect;
        private Vector2 _instrumentCenter01 = new Vector2(0.5f, 0.5f);
        private float _zoom;

        // Hidden instrument state.
        private float _initialRotationErrorDegrees;
        private float _plateSolution;
        private float _lensSolution;
        private float _prismSolution;

        // Player controls.
        private float _bearingControlDegrees;
        private float _plateControl;
        private float _lensControl;
        private float _prismControl;
        private bool _draggingBearingDial;

        private ObservationStage _stage;
        private bool _completed;
        private CelestialObservation _completedObservation;
        private string _setupError;
        private string _statusMessage;
        private float _statusMessageUntil;
        private string _hoveredStarId;
        private string _wrongFlashStarId;
        private float _wrongFlashUntil;
        private bool _debugHighlightExpectedStars;

        private Texture2D _softDisc;
        private Texture2D _outsideCircleMask;
        private Texture2D _white;

        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallLabelStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _statusStyle;

        private static readonly Color PanelColor = new Color(0.045f, 0.055f, 0.070f, 1f);
        private static readonly Color CardColor = new Color(0.070f, 0.085f, 0.105f, 0.98f);
        private static readonly Color LensColor = new Color(0.005f, 0.012f, 0.027f, 1f);
        private static readonly Color Cyan = new Color(0.30f, 0.88f, 1f, 1f);
        private static readonly Color Warm = new Color(1f, 0.78f, 0.32f, 1f);
        private static readonly Color Success = new Color(0.44f, 1f, 0.62f, 1f);
        private static readonly Color Error = new Color(1f, 0.35f, 0.30f, 1f);
        private static readonly Color DebugTarget = new Color(1f, 0.24f, 0.82f, 1f);

        public CelestialObservation CompletedObservation => _completedObservation;

        private float EffectiveRotationDegrees => Mathf.DeltaAngle(0f, _initialRotationErrorDegrees + _bearingControlDegrees);
        private float PlateError => _plateControl - _plateSolution;
        private float LensError => _lensControl - _lensSolution;
        private float PrismError => _prismControl - _prismSolution;

        public CelestialObservationCartridge(
            CelestialField field,
            Vector2 observerWorldPosition,
            CelestialSkyProjectionSettings projectionSettings,
            CelestialObservationSettings observationSettings,
            Func<float> starVisibilityProvider,
            Func<TimeOfDayManager> timeManagerProvider,
            int surveySequence,
            Func<CelestialObservation, string> tryCommitObservation,
            Func<bool> debugShowAllConstellations = null)
        {
            _field = field;
            _observerWorldPosition = observerWorldPosition;
            _projectionSettings = projectionSettings;
            _observationSettings = observationSettings;
            _starVisibilityProvider = starVisibilityProvider;
            _timeManagerProvider = timeManagerProvider;
            _surveySequence = Mathf.Max(0, surveySequence);
            _tryCommitObservation = tryCommitObservation;
            _debugShowAllConstellations = debugShowAllConstellations;
        }

        public void Begin(MiniGameContext context)
        {
            _context = context ?? new MiniGameContext();
            _white = Texture2D.whiteTexture;
            _softDisc = BuildSoftDiscTexture(64);
            _outsideCircleMask = BuildOutsideCircleMaskTexture(256, _observationSettings != null ? _observationSettings.apertureRadius01 : 0.47f);

            float minZoom = _observationSettings != null ? Mathf.Max(1f, _observationSettings.minimumZoom) : 1f;
            float maxZoom = _observationSettings != null ? Mathf.Max(minZoom, _observationSettings.maximumZoom) : 6f;
            _zoom = _observationSettings != null
                ? Mathf.Clamp(_observationSettings.initialZoom, minZoom, maxZoom)
                : 1.6f;

            _instrumentCenter01 = new Vector2(0.5f, 0.5f);
            _stage = ObservationStage.Calibration;
            _traceIds.Clear();
            _observedInstrumentPositions.Clear();
            _completed = false;
            _completedObservation = null;
            _setupError = null;
            _statusMessage = null;
            _hoveredStarId = null;
            _wrongFlashStarId = null;
            _debugHighlightExpectedStars = false;

            BuildProjectedObjectCache();
            InitializePuzzleState();
            BuildPattern();
        }

        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            if (_completed)
            {
                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.Completed,
                    quality01 = _completedObservation != null ? _completedObservation.quality01 : 0f,
                    note = _completedObservation != null
                        ? $"Celestial observation: {_completedObservation.objects.Count} objects / {_completedObservation.AnchorCount} pattern stars"
                        : "Celestial observation completed",
                    hasMeaningfulProgress = true
                };
            }

            bool meaningful = _stage != ObservationStage.Calibration ||
                              _traceIds.Count > 0 ||
                              Mathf.Abs(PlateError) < 0.5f ||
                              Mathf.Abs(LensError) < 0.5f ||
                              Mathf.Abs(PrismError) < 0.5f;

            return new MiniGameResult
            {
                outcome = MiniGameOutcome.None,
                quality01 = 0f,
                note = null,
                hasMeaningfulProgress = meaningful
            };
        }

        public MiniGameResult Cancel()
        {
            bool partial = _stage != ObservationStage.Calibration || _traceIds.Count > 0;
            return new MiniGameResult
            {
                outcome = partial ? MiniGameOutcome.Partial : MiniGameOutcome.Cancelled,
                quality01 = 0f,
                note = partial ? "Celestial observation cancelled after calibration progress." : "Celestial observation cancelled.",
                hasMeaningfulProgress = partial
            };
        }

        public MiniGameResult Interrupt(string reason)
        {
            bool partial = _stage != ObservationStage.Calibration || _traceIds.Count > 0;
            return new MiniGameResult
            {
                outcome = partial ? MiniGameOutcome.Partial : MiniGameOutcome.Cancelled,
                quality01 = 0f,
                note = $"Observation interrupted: {reason}",
                hasMeaningfulProgress = partial
            };
        }

        public void End()
        {
            if (_softDisc != null)
            {
                UnityEngine.Object.Destroy(_softDisc);
                _softDisc = null;
            }

            if (_outsideCircleMask != null)
            {
                UnityEngine.Object.Destroy(_outsideCircleMask);
                _outsideCircleMask = null;
            }

            _context = null;
        }

        public void DrawOverlayGUI(Rect panel)
        {
            EnsureStyles();

            float visibility = GetStarVisibility01();
            DrawSolidRect(panel, PanelColor);

            Rect header = new Rect(panel.x + 16f, panel.y + 10f, panel.width - 32f, 28f);
            GUI.Label(header, "CELESTIAL OBSERVATION INSTRUMENT", _titleStyle);

            Rect work = new Rect(panel.x + 16f, panel.y + 44f, panel.width - 32f, panel.height - 60f);
            float gap = 12f;
            float leftWidth = Mathf.Clamp(work.width * 0.20f, 175f, 260f);
            float rightWidth = Mathf.Clamp(work.width * 0.25f, 220f, 320f);

            Rect left = new Rect(work.x, work.y, leftWidth, work.height);
            Rect right = new Rect(work.xMax - rightWidth, work.y, rightWidth, work.height);
            Rect centerArea = new Rect(left.xMax + gap, work.y, Mathf.Max(120f, right.xMin - left.xMax - gap * 2f), work.height);

            float lensSize = Mathf.Min(centerArea.width, centerArea.height - 48f);
            Rect lensRect = new Rect(
                centerArea.center.x - lensSize * 0.5f,
                centerArea.y + Mathf.Max(0f, (centerArea.height - lensSize - 36f) * 0.42f),
                lensSize,
                lensSize);

            DrawInfoPanel(left, visibility);
            DrawTelescope(lensRect, visibility);
            DrawCalibrationPanel(right, visibility);

            Rect bottomStatus = new Rect(centerArea.x, lensRect.yMax + 8f, centerArea.width, Mathf.Max(28f, centerArea.yMax - lensRect.yMax - 8f));
            DrawCenterStatus(bottomStatus, visibility);
        }

        private void BuildProjectedObjectCache()
        {
            _projectedObjects.Clear();
            _queriedObjects.Clear();

            if (_field == null || !_field.IsValid || _projectionSettings == null)
                return;

            _baseVisibleWorldRect = CelestialSkyProjection.BuildVisibleWorldRect(
                _field.WorldBounds,
                _observerWorldPosition,
                _projectionSettings);

            _field.Query(_baseVisibleWorldRect, _queriedObjects, clearResults: true);

            for (int i = 0; i < _queriedObjects.Count; i++)
            {
                CelestialObject obj = _queriedObjects[i];
                if (obj == null)
                    continue;

                Vector2 ignoredViewport;
                Vector2 signedWindow;
                if (!CelestialSkyProjection.TryProjectToViewport(
                        _field.WorldBounds,
                        _observerWorldPosition,
                        obj.WorldPosition,
                        _projectionSettings,
                        out ignoredViewport,
                        out signedWindow))
                {
                    continue;
                }

                // signedWindow is now shape-preserving metric celestial space: X and Y share one
                // common world-unit scale and +Y is north/up. This is the coordinate used by the
                // telescope, decoder, trace geometry, and eventual physical chart scraps.
                Vector2 chartViewport = signedWindow * 0.5f + new Vector2(0.5f, 0.5f);

                // Preserve the already-tuned Iteration-9 target selection envelope independently of
                // the display cleanup. This raw query normalization has the same 0..1 candidate-space
                // coverage as before, so fixing geometry does NOT secretly retune puzzle difficulty.
                Vector2 selectionViewport = new Vector2(
                    Mathf.InverseLerp(_baseVisibleWorldRect.xMin, _baseVisibleWorldRect.xMax, obj.WorldPosition.x),
                    Mathf.InverseLerp(_baseVisibleWorldRect.yMin, _baseVisibleWorldRect.yMax, obj.WorldPosition.y));

                if (chartViewport.x < -0.05f || chartViewport.x > 1.05f ||
                    chartViewport.y < -0.05f || chartViewport.y > 1.05f)
                {
                    continue;
                }

                _projectedObjects.Add(new ProjectedObject
                {
                    celestialObject = obj,
                    baseViewport01 = chartViewport,
                    selectionViewport01 = selectionViewport,
                    instrumentViewport01 = Vector2.zero,
                    screenPoint = Vector2.zero,
                    insideAperture = false
                });
            }
        }

        private void InitializePuzzleState()
        {
            int observerHash = Mathf.RoundToInt(_observerWorldPosition.x * 100f) * 73856093 ^
                               Mathf.RoundToInt(_observerWorldPosition.y * 100f) * 19349663;
            int seed = (_context != null ? _context.seed : 0) ^ observerHash ^ (_surveySequence * 83492791) ^ 0x4C11DB7;
            System.Random rng = new System.Random(seed);

            float maxRot = _observationSettings != null
                ? Mathf.Clamp(_observationSettings.maximumInitialRotationErrorDegrees, 30f, 175f)
                : 155f;

            float magnitude = Mathf.Lerp(35f, maxRot, Next01(rng));
            _initialRotationErrorDegrees = magnitude * (rng.Next(0, 2) == 0 ? -1f : 1f);
            _bearingControlDegrees = 0f;

            _plateSolution = Mathf.Lerp(-0.72f, 0.72f, Next01(rng));
            _lensSolution = Mathf.Lerp(-0.72f, 0.72f, Next01(rng));
            _prismSolution = Mathf.Lerp(-0.72f, 0.72f, Next01(rng));
            _plateControl = PickSeparatedControl(rng, _plateSolution, 0.45f);
            _lensControl = PickSeparatedControl(rng, _lensSolution, 0.45f);
            _prismControl = PickSeparatedControl(rng, _prismSolution, 0.45f);
        }

        private void BuildPattern()
        {
            _patternObjects.Clear();
            _patternIds.Clear();
            _decoderBasePoints.Clear();
            _decoderCurrentPoints.Clear();

            if (_observationSettings == null)
            {
                _setupError = "Missing observation settings.";
                return;
            }

            int desired = Mathf.Clamp(
                _observationSettings.patternStarCount,
                Mathf.Max(3, _observationSettings.minimumAnchors),
                Mathf.Max(_observationSettings.minimumAnchors, _observationSettings.maximumAnchors));

            int minimumRequired = Mathf.Max(3, _observationSettings.minimumAnchors);

            // Survey targets come from a deliberately broad but still playable region around the
            // observation/reference datum. Iteration 6 briefly allowed the entire 0..1 cartridge
            // projection, which produced interesting variety but also encouraged targets at extreme
            // edges and forced the telescope to pan into half-empty space. Keep substantially more
            // sky than the original near-center pass, while reserving the outer fringe as non-target
            // context. The telescope may still display those fringe stars; they simply won't be chosen
            // as required pattern anchors.
            float targetRadius = Mathf.Clamp(_observationSettings.surveyTargetRadius01, 0.25f, 0.48f);
            Vector2 reference = new Vector2(0.5f, 0.5f);

            List<ProjectedObject> candidates = new List<ProjectedObject>();
            for (int i = 0; i < _projectedObjects.Count; i++)
            {
                ProjectedObject p = _projectedObjects[i];
                if (p.celestialObject == null || p.celestialObject.Kind != CelestialObjectKind.LandmarkStar)
                    continue;

                Vector2 uv = p.selectionViewport01;
                if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
                    continue;

                if (Vector2.Distance(uv, reference) <= targetRadius)
                    candidates.Add(p);
            }

            if (candidates.Count < minimumRequired)
            {
                _setupError = $"Not enough landmark stars in the cartridge sky window ({candidates.Count}). This patch needs at least {minimumRequired}.";
                return;
            }

            desired = Mathf.Min(desired, candidates.Count);

            // Build one deterministic, spatially spread landmark pool for this observation region.
            // Successive surveys then walk overlapping windows through that pool. This gives the
            // player new pieces while deliberately preserving registration stars between pieces.
            List<ProjectedObject> surveyPool = BuildSurveyPool(candidates);
            if (surveyPool.Count == 0)
            {
                _setupError = "Could not build a landmark survey pool from this sky.";
                return;
            }

            int overlapCount = Mathf.Clamp(
                Mathf.RoundToInt(desired * Mathf.Clamp(_observationSettings.surveyPatternOverlapFraction, 0.15f, 0.85f)),
                1,
                Mathf.Max(1, desired - 1));
            int step = Mathf.Max(1, desired - overlapCount);
            int startIndex = (_surveySequence * step) % surveyPool.Count;

            for (int i = 0; i < desired && i < surveyPool.Count; i++)
            {
                int index = (startIndex + i) % surveyPool.Count;
                ProjectedObject candidate = surveyPool[index];
                if (candidate != null && !_patternObjects.Contains(candidate))
                    _patternObjects.Add(candidate);
            }

            if (_patternObjects.Count < Mathf.Max(3, _observationSettings.minimumAnchors))
            {
                _setupError = "Could not build a sufficiently separated landmark-star pattern from this sky.";
                return;
            }

            OrderPatternAsPath(_patternObjects);

            for (int i = 0; i < _patternObjects.Count; i++)
                _patternIds.Add(_patternObjects[i].celestialObject.StableId);

            BuildDecoderBasePoints();
        }

        private List<ProjectedObject> BuildSurveyPool(List<ProjectedObject> candidates)
        {
            var pool = new List<ProjectedObject>();
            if (candidates == null || candidates.Count == 0)
                return pool;

            var remaining = new List<ProjectedObject>(candidates);
            remaining.Sort((a, b) => string.CompareOrdinal(
                a?.celestialObject?.StableId,
                b?.celestialObject?.StableId));

            // Start from a strong readable landmark, with a tiny deterministic tie-break so different
            // regions do not all build their survey pool around the same score shape.
            int firstIndex = 0;
            float firstScore = float.NegativeInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                float score = PatternCandidateScore(remaining[i]) + SurveyHash01(remaining[i], 17) * 0.18f;
                if (score > firstScore)
                {
                    firstScore = score;
                    firstIndex = i;
                }
            }

            pool.Add(remaining[firstIndex]);
            remaining.RemoveAt(firstIndex);

            // Farthest-point ordering gives us a stable pool where adjacent entries tend to be useful
            // puzzle anchors rather than a clump of nearly coincident stars.
            while (remaining.Count > 0)
            {
                int bestIndex = 0;
                float bestScore = float.NegativeInfinity;

                for (int i = 0; i < remaining.Count; i++)
                {
                    ProjectedObject candidate = remaining[i];
                    float minimumDistance = float.PositiveInfinity;

                    for (int j = 0; j < pool.Count; j++)
                        minimumDistance = Mathf.Min(minimumDistance, Vector2.Distance(candidate.baseViewport01, pool[j].baseViewport01));

                    float score = minimumDistance * 8f +
                                  PatternCandidateScore(candidate) * 0.16f +
                                  SurveyHash01(candidate, pool.Count + 31) * 0.08f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                pool.Add(remaining[bestIndex]);
                remaining.RemoveAt(bestIndex);
            }

            return pool;
        }

        private float SurveyHash01(ProjectedObject p, int salt)
        {
            string id = p?.celestialObject?.StableId ?? string.Empty;
            unchecked
            {
                uint hash = 2166136261u;
                string value = $"{_field?.WorldSeed ?? 0}:{salt}:{id}";
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return (hash & 0x00FFFFFFu) / 16777216f;
            }
        }

        private float PatternCandidateScore(ProjectedObject p)
        {
            if (p == null || p.celestialObject == null)
                return -100f;

            CelestialObject obj = p.celestialObject;
            return obj.Brightness01 * 1.8f + obj.Prominence01 * 2.2f;
        }

        private static void OrderPatternAsPath(List<ProjectedObject> selected)
        {
            if (selected == null || selected.Count <= 2)
                return;

            List<ProjectedObject> remaining = new List<ProjectedObject>(selected);
            selected.Clear();

            // Begin at the left-most member, which gives the decoder a readable endpoint without labels.
            int startIndex = 0;
            for (int i = 1; i < remaining.Count; i++)
            {
                if (remaining[i].baseViewport01.x < remaining[startIndex].baseViewport01.x)
                    startIndex = i;
            }

            ProjectedObject current = remaining[startIndex];
            selected.Add(current);
            remaining.RemoveAt(startIndex);

            while (remaining.Count > 0)
            {
                int nearestIndex = 0;
                float nearestDistance = Vector2.Distance(current.baseViewport01, remaining[0].baseViewport01);

                for (int i = 1; i < remaining.Count; i++)
                {
                    float d = Vector2.Distance(current.baseViewport01, remaining[i].baseViewport01);
                    if (d < nearestDistance)
                    {
                        nearestDistance = d;
                        nearestIndex = i;
                    }
                }

                current = remaining[nearestIndex];
                selected.Add(current);
                remaining.RemoveAt(nearestIndex);
            }
        }

        private void BuildDecoderBasePoints()
        {
            _decoderBasePoints.Clear();
            _hasDecoderReferencePoint = false;
            if (_patternObjects.Count == 0)
                return;

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < _patternObjects.Count; i++)
                centroid += _patternObjects[i].baseViewport01;
            centroid /= _patternObjects.Count;

            float normalizationRadius = 0.0001f;
            for (int i = 0; i < _patternObjects.Count; i++)
                normalizationRadius = Mathf.Max(normalizationRadius, Vector2.Distance(_patternObjects[i].baseViewport01, centroid));

            // Include the observation origin in the decoder's fitted extent. Patterns can now be
            // selected anywhere in the full cartridge field, including near its boundary; without
            // this, an origin marker for a distant pattern could land outside the decoder entirely.
            // Fitting both together preserves their relative distance while still withholding absolute
            // orientation through the canonical rotation below.
            normalizationRadius = Mathf.Max(
                normalizationRadius,
                Vector2.Distance(new Vector2(0.5f, 0.5f), centroid));

            List<Vector2> normalizedPoints = new List<Vector2>(_patternObjects.Count);
            for (int i = 0; i < _patternObjects.Count; i++)
            {
                Vector2 normalized = (_patternObjects[i].baseViewport01 - centroid) / normalizationRadius;
                normalizedPoints.Add(normalized);
            }

            // The decoder intentionally withholds sky rotation. Canonicalize the pattern by placing
            // its endpoint-to-endpoint axis horizontally. The telescope remains freely rotatable, so
            // the player must discover which bearing makes the real landmark stars match this shape.
            float canonicalAngle = 0f;
            if (normalizedPoints.Count >= 2)
            {
                Vector2 axis = normalizedPoints[normalizedPoints.Count - 1] - normalizedPoints[0];
                if (axis.sqrMagnitude > 0.000001f)
                    canonicalAngle = -Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            }

            for (int i = 0; i < normalizedPoints.Count; i++)
                _decoderBasePoints.Add(Rotate(normalizedPoints[i], canonicalAngle) * 0.68f);

            // The observation origin is not a constellation/star node, but it is useful positional
            // evidence. Show it in the decoder as a small orientationless circular blip so the player
            // can judge where the decoded landmark shape sits relative to where the observation began.
            // It goes through the same canonical rotation as the landmark shape, so the decoder still
            // reveals no absolute sky bearing.
            Vector2 normalizedReference = (new Vector2(0.5f, 0.5f) - centroid) / normalizationRadius;
            _decoderReferenceBasePoint = Rotate(normalizedReference, canonicalAngle) * 0.68f;
            _decoderReferenceCurrentPoint = _decoderReferenceBasePoint;
            _hasDecoderReferencePoint = true;
        }

        private void DrawInfoPanel(Rect rect, float visibility)
        {
            DrawCard(rect);
            Rect inner = new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f);
            float y = inner.y;

            GUI.Label(new Rect(inner.x, y, inner.width, 24f), "OBSERVATION", _sectionStyle);
            y += 30f;

            string stageText = _stage == ObservationStage.Calibration
                ? "1 / 3  CALIBRATE"
                : _stage == ObservationStage.TracePattern
                    ? "2 / 3  TRANSCRIBE"
                    : "3 / 3  RECORD";
            GUI.Label(new Rect(inner.x, y, inner.width, 22f), stageText, _labelStyle);
            y += 30f;

            TimeOfDayManager time = _timeManagerProvider != null ? _timeManagerProvider() : null;
            if (time != null)
            {
                GUI.Label(new Rect(inner.x, y, inner.width, 20f), $"Time  {FormatHour(time.CurrentTime)}", _smallLabelStyle);
                y += 20f;
                GUI.Label(new Rect(inner.x, y, inner.width, 20f), $"Date  {time.Year}/{time.Month}/{time.Day}", _smallLabelStyle);
                y += 22f;
            }

            GUI.Label(new Rect(inner.x, y, inner.width, 20f), $"Star visibility  {Mathf.RoundToInt(visibility * 100f)}%", _smallLabelStyle);
            y += 22f;
            DrawMeter(new Rect(inner.x, y, inner.width, 8f), visibility, visibility >= MinimumVisibility ? Cyan : Error);
            y += 22f;

            GUI.Label(new Rect(inner.x, y, inner.width, 20f), $"Pattern stars  {_patternObjects.Count}", _smallLabelStyle);
            y += 28f;

            GUI.Label(new Rect(inner.x, y, inner.width, 20f), "REFERENCE DATUM", _sectionStyle);
            y += 24f;
            GUI.Label(
                new Rect(inner.x, y, inner.width, 70f),
                "The paper boat in the telescope marks your observation origin. The sky is measured relative to that point.",
                _smallLabelStyle);
            y += 80f;

            string instructions;
            if (_stage == ObservationStage.Calibration)
            {
                instructions = "Seat the optics by using the three coupled controls to center every registration pip. You may pan and zoom the telescope before calibration. Telescope rotation is independent and stays free. The decoder will reveal the landmark shape, but not its sky rotation.";
            }
            else if (_stage == ObservationStage.TracePattern)
            {
                instructions = "Rotate and pan the telescope until the landmark stars match the decoder shape, then select the full connected sequence. Either direction is valid. The instrument gives no correctness feedback until every pattern star is selected.";
            }
            else
            {
                instructions = "Pattern acquired. Record the observation to produce Phase 4 evidence. Paper comes next in Phase 5.";
            }

            GUI.Label(new Rect(inner.x, y, inner.width, 104f), instructions, _smallLabelStyle);

            // Keep the debug reveal deliberately explicit and local to this cartridge. It does not
            // change puzzle state or correctness; it only proves which deterministic landmark stars
            // the generated survey expects. This is invaluable while we tune how cruel the puzzle is.
            _showKnownConstellations = GUI.Toggle(new Rect(inner.x, rect.yMax - 154f, inner.width, 24f),
                _showKnownConstellations, "Show Known Constellations");
            float buttonY = rect.yMax - 126f;
            if (_stage != ObservationStage.Calibration)
            {
                if (GUI.Button(new Rect(inner.x, buttonY, inner.width, 28f), "RECALIBRATE"))
                    ReturnToCalibration();
                buttonY += 34f;
            }

            if (GUI.Button(new Rect(inner.x, buttonY, inner.width, 28f), "RECENTER TELESCOPE"))
            {
                _instrumentCenter01 = new Vector2(0.5f, 0.5f);
                float minZoom = Mathf.Max(1f, _observationSettings.minimumZoom);
                float maxZoom = Mathf.Max(minZoom, _observationSettings.maximumZoom);
                _zoom = Mathf.Clamp(_observationSettings.initialZoom, minZoom, maxZoom);
            }
            buttonY += 34f;

            string debugLabel = _debugHighlightExpectedStars
                ? "DEBUG: HIDE EXPECTED STARS"
                : "DEBUG: HIGHLIGHT EXPECTED STARS";
            if (GUI.Button(new Rect(inner.x, buttonY, inner.width, 28f), debugLabel))
                _debugHighlightExpectedStars = !_debugHighlightExpectedStars;
        }

        private void DrawTelescope(Rect lensRect, float visibility)
        {
            DrawSolidRect(lensRect, LensColor);

            if (_setupError != null)
            {
                ApplyCircularMask(lensRect, PanelColor);
                float errorRadius = lensRect.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
                DrawCircleOutline(RectFromCenter(lensRect.center, errorRadius), Error, 3f, 96);
                GUI.Label(new Rect(lensRect.x + 30f, lensRect.center.y - 40f, lensRect.width - 60f, 80f), _setupError, _centerStyle);
                return;
            }

            HandleTelescopeNavigationInput(lensRect);

            UpdateProjectedScreenPoints(lensRect);
            AccumulateVisibleEvidence(visibility);
            DrawTelescopeGuide(lensRect);
            DrawCelestialObjects(lensRect, visibility);
            DrawKnownConstellations(lensRect, visibility);
            DrawDebugExpectedStars(lensRect);
            DrawTrace(lensRect);
            DrawObservationOrigin(lensRect);

            if (_stage == ObservationStage.TracePattern)
                HandleTraceInput(lensRect, visibility);
            else
                _hoveredStarId = null;

            DrawHoverAndErrorFeedback();
            ApplyCircularMask(lensRect, PanelColor);
            float apertureRadius = lensRect.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            DrawCircleOutline(RectFromCenter(lensRect.center, apertureRadius), new Color(0.72f, 0.86f, 0.94f, 0.92f), 3f, 128);
            DrawCircleOutline(RectFromCenter(lensRect.center, Mathf.Max(1f, apertureRadius - 7f)), new Color(0.25f, 0.55f, 0.68f, 0.45f), 1f, 128);
        }

        private void DrawCalibrationPanel(Rect rect, float visibility)
        {
            DrawCard(rect);
            Rect inner = new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f);
            float y = inner.y;

            GUI.Label(new Rect(inner.x, y, inner.width, 22f), "INSTRUMENT CALIBRATION", _sectionStyle);
            y += 28f;

            float dialSize = Mathf.Min(inner.width * 0.62f, 132f);
            Rect dial = new Rect(inner.center.x - dialSize * 0.5f, y, dialSize, dialSize);
            DrawBearingDial(dial, _setupError == null);
            y = dial.yMax + 8f;
            GUI.Label(new Rect(inner.x, y, inner.width, 20f), "TELESCOPE ROTATION", _centerStyle);
            y += 26f;

            float decoderSize = Mathf.Min(inner.width * 0.78f, 164f);
            Rect decoder = new Rect(inner.center.x - decoderSize * 0.5f, y, decoderSize, decoderSize);
            DrawDecoder(decoder);
            y = decoder.yMax + 8f;
            GUI.Label(new Rect(inner.x, y, inner.width, 20f), "OPTICAL DECODER", _centerStyle);
            y += 28f;

            bool controlsEnabled = _stage == ObservationStage.Calibration;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = controlsEnabled;

            GUI.Label(new Rect(inner.x, y, inner.width, 18f), "Plate alignment", _smallLabelStyle);
            y += 16f;
            _plateControl = GUI.HorizontalSlider(new Rect(inner.x + 4f, y, inner.width - 8f, 18f), _plateControl, -1f, 1f);
            y += 25f;

            GUI.Label(new Rect(inner.x, y, inner.width, 18f), "Lens focus", _smallLabelStyle);
            y += 16f;
            _lensControl = GUI.HorizontalSlider(new Rect(inner.x + 4f, y, inner.width - 8f, 18f), _lensControl, -1f, 1f);
            y += 25f;

            GUI.Label(new Rect(inner.x, y, inner.width, 18f), "Prism trim", _smallLabelStyle);
            y += 16f;
            _prismControl = GUI.HorizontalSlider(new Rect(inner.x + 4f, y, inner.width - 8f, 18f), _prismControl, -1f, 1f);
            y += 24f;

            GUI.enabled = previousEnabled;

            Rect registration = new Rect(inner.x, y, inner.width, 58f);
            DrawRegistrationPuzzle(registration);
            y = registration.yMax + 6f;

            if (_stage == ObservationStage.Calibration)
            {
                bool canLock = visibility >= MinimumVisibility && IsOpticsWithinTolerance && _setupError == null;
                bool old = GUI.enabled;
                GUI.enabled = canLock;
                if (GUI.Button(new Rect(inner.x, y, inner.width, 30f), canLock ? "SEAT OPTICS" : "ALIGN REGISTRATION PIPS"))
                    LockCalibration();
                GUI.enabled = old;
            }
            else
            {
                GUI.color = Success;
                GUI.Label(new Rect(inner.x, y + 4f, inner.width, 22f), "OPTICS SEATED · ROTATION FREE", _centerStyle);
                GUI.color = Color.white;
            }
        }

        private void DrawCenterStatus(Rect rect, float visibility)
        {
            string text;
            Color color = Color.white;

            if (_setupError != null)
            {
                text = _setupError;
                color = Error;
            }
            else if (visibility < MinimumVisibility)
            {
                text = "Insufficient stellar visibility. Wait for darker or clearer conditions.";
                color = Error;
            }
            else if (!string.IsNullOrEmpty(_statusMessage) && Time.unscaledTime <= _statusMessageUntil)
            {
                text = _statusMessage;
                color = _statusMessage.Contains("mismatch") ? Error : Cyan;
            }
            else if (_stage == ObservationStage.Calibration)
            {
                text = IsOpticsWithinTolerance
                    ? "Registration pips aligned. Seat the optics."
                    : "Center all registration pips. Each control disturbs more than one channel.";
                color = IsOpticsWithinTolerance ? Success : Color.white;
            }
            else if (_stage == ObservationStage.TracePattern)
            {
                text = "Select the complete landmark sequence. Verification occurs only after the final selection.";
                color = Cyan;
            }
            else
            {
                text = "PATTERN ACQUIRED";
                color = Success;
            }

            GUI.color = color;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 24f), text, _statusStyle);
            GUI.color = Color.white;

            if (_stage == ObservationStage.ReadyToRecord)
            {
                Rect button = new Rect(rect.center.x - 120f, rect.y + 28f, 240f, 32f);
                bool old = GUI.enabled;
                GUI.enabled = visibility >= MinimumVisibility && !_completed;
                if (GUI.Button(button, "RECORD OBSERVATION"))
                    CompleteObservation(visibility);
                GUI.enabled = old;
            }
            else if (_stage == ObservationStage.TracePattern && _traceIds.Count > 0)
            {
                Rect button = new Rect(rect.center.x - 70f, rect.y + 28f, 140f, 28f);
                if (GUI.Button(button, "CLEAR TRACE"))
                    ClearTrace("Trace cleared.");
            }
        }

        private void DrawBearingDial(Rect dial, bool interactive)
        {
            DrawSolidRect(dial, CardColor);
            DrawCircleOutline(ShrinkRect(dial, 2f), new Color(0.66f, 0.75f, 0.80f, 0.9f), 2f, 96);

            Vector2 c = dial.center;
            float r = dial.width * 0.43f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                DrawLine(c + dir * (r - 7f), c + dir * r, new Color(0.55f, 0.68f, 0.75f, 0.8f), i % 3 == 0 ? 2f : 1f);
            }

            float pointerAngle = _bearingControlDegrees * Mathf.Deg2Rad;
            Vector2 pointer = new Vector2(Mathf.Cos(pointerAngle), -Mathf.Sin(pointerAngle));
            Color bearingColor = _stage == ObservationStage.Calibration ? Warm : Cyan;
            DrawLine(c, c + pointer * (r - 10f), bearingColor, 3f);
            DrawDisc(c, 5f, bearingColor, 1f);

            if (!interactive)
                return;

            Event e = Event.current;
            if (e == null)
                return;

            bool inside = Vector2.Distance(e.mousePosition, c) <= dial.width * 0.5f;
            if (e.type == EventType.MouseDown && e.button == 0 && inside)
            {
                _draggingBearingDial = true;
                SetBearingFromMouse(e.mousePosition, c);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && _draggingBearingDial)
            {
                SetBearingFromMouse(e.mousePosition, c);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _draggingBearingDial)
            {
                SetBearingFromMouse(e.mousePosition, c);
                _draggingBearingDial = false;
                e.Use();
            }
        }

        private void SetBearingFromMouse(Vector2 mouse, Vector2 center)
        {
            Vector2 d = mouse - center;
            float degrees = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
            _bearingControlDegrees = Mathf.Repeat(degrees, 360f);
        }

        private void DrawDecoder(Rect rect)
        {
            DrawSolidRect(rect, LensColor);
            UpdateDecoderPoints();

            Vector2 center = rect.center;
            float radius = rect.width * 0.42f;

            for (int i = 0; i < _decoderCurrentPoints.Count - 1; i++)
            {
                Vector2 a = DecoderPointToScreen(_decoderCurrentPoints[i], center, radius);
                Vector2 b = DecoderPointToScreen(_decoderCurrentPoints[i + 1], center, radius);
                DrawLine(a, b, _stage == ObservationStage.Calibration ? Warm : Success, 2.2f);
            }

            for (int i = 0; i < _decoderCurrentPoints.Count; i++)
            {
                Vector2 p = DecoderPointToScreen(_decoderCurrentPoints[i], center, radius);
                DrawDisc(p, 5.5f, Color.white, 1f);
                DrawDisc(p, 2.4f, _stage == ObservationStage.Calibration ? Warm : Success, 1f);
            }

            if (_hasDecoderReferencePoint)
            {
                Vector2 reference = DecoderPointToScreen(_decoderReferenceCurrentPoint, center, radius);
                DrawPaperBoatMarker(reference, 9f, Warm, new Color(0.04f, 0.06f, 0.08f, 0.95f), 1.35f);
            }

            ApplyCircularMask(rect, CardColor);
            float decoderAperture = rect.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            DrawCircleOutline(RectFromCenter(rect.center, decoderAperture), new Color(0.62f, 0.72f, 0.78f, 0.9f), 2f, 96);
        }

        private void UpdateDecoderPoints()
        {
            _decoderCurrentPoints.Clear();

            for (int i = 0; i < _decoderBasePoints.Count; i++)
                _decoderCurrentPoints.Add(ApplyDecoderDistortion(_decoderBasePoints[i], i));

            if (_hasDecoderReferencePoint)
            {
                // Give the origin blip the same family of optical distortion while calibration is
                // unsolved. Once the controls are seated all errors are zero and it lands exactly at
                // its canonical relative position. The synthetic index only affects the temporary
                // jumbled presentation; it carries no semantic ordering.
                _decoderReferenceCurrentPoint = ApplyDecoderDistortion(_decoderReferenceBasePoint, _decoderBasePoints.Count);
            }
        }

        private Vector2 ApplyDecoderDistortion(Vector2 basePoint, int flavorIndex)
        {
            float plateStrength = _observationSettings.decoderPlateDistortion;
            float lensStrength = _observationSettings.decoderLensDistortion;
            float prismStrength = _observationSettings.decoderPrismDistortion;

            Vector2 p = basePoint;

            float alternating = (flavorIndex & 1) == 0 ? 1f : -1f;
            float verticalFlavor = ((flavorIndex % 3) - 1) * 0.72f;
            p += new Vector2(alternating, verticalFlavor) * PlateError * plateStrength;

            float nodeFactor = 0.65f + (flavorIndex % 4) * 0.22f;
            p *= 1f + LensError * lensStrength * nodeFactor;

            float shearFlavor = (flavorIndex % 2 == 0 ? 1f : -0.65f) * (0.55f + flavorIndex * 0.11f);
            p += new Vector2(p.y * PrismError * prismStrength, shearFlavor * PrismError * prismStrength * 0.45f);

            return p;
        }

        private void DrawRegistrationPuzzle(Rect rect)
        {
            DrawSolidRect(rect, new Color(0.035f, 0.045f, 0.060f, 1f));

            float gap = 8f;
            float size = Mathf.Min(52f, (rect.width - gap * 2f) / 3f);
            float total = size * 3f + gap * 2f;
            float startX = rect.center.x - total * 0.5f;

            ComputeRegistrationOffsets(out Vector2 offsetA, out Vector2 offsetB, out Vector2 offsetC);
            float pipTolerance = Mathf.Clamp(_observationSettings.registrationPipTolerance, 0.04f, 0.30f);

            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = i == 0 ? offsetA : (i == 1 ? offsetB : offsetC);
                Rect cell = new Rect(startX + i * (size + gap), rect.center.y - size * 0.5f, size, size);
                Vector2 c = cell.center;
                float r = size * 0.40f;

                DrawCircleOutline(RectFromCenter(c, r), new Color(0.42f, 0.55f, 0.62f, 0.75f), 1.2f, 32);
                DrawLine(c + new Vector2(-r * 0.55f, 0f), c + new Vector2(r * 0.55f, 0f), new Color(0.42f, 0.55f, 0.62f, 0.55f), 1f);
                DrawLine(c + new Vector2(0f, -r * 0.55f), c + new Vector2(0f, r * 0.55f), new Color(0.42f, 0.55f, 0.62f, 0.55f), 1f);

                Vector2 clamped = Vector2.ClampMagnitude(offset, 1f);
                Vector2 pip = c + new Vector2(clamped.x, -clamped.y) * r;
                bool centered = offset.magnitude <= pipTolerance;
                DrawDisc(pip, centered ? 4.8f : 4.2f, centered ? Success : Warm, 1f);
                if (centered)
                    DrawCircleOutline(RectFromCenter(c, r * 0.26f), new Color(Success.r, Success.g, Success.b, 0.55f), 1.2f, 24);
            }

            GUI.Label(new Rect(rect.x, rect.yMax - 15f, rect.width, 16f), "REGISTRATION · CENTER ALL THREE PIPS", _centerStyle);
        }

        private void ComputeRegistrationOffsets(out Vector2 a, out Vector2 b, out Vector2 c)
        {
            // Each readout depends on multiple controls. That cross-coupling is intentional: there
            // is no single slider to maximize, and improving one channel may disturb another.
            a = new Vector2(PlateError + LensError * 0.52f, LensError - PrismError * 0.38f);
            b = new Vector2(LensError + PrismError * 0.48f, PrismError - PlateError * 0.43f);
            c = new Vector2(PrismError + PlateError * 0.41f, PlateError - LensError * 0.36f);
        }

        private void HandleTelescopeNavigationInput(Rect lensRect)
        {
            Event e = Event.current;
            if (e == null || !PointInsideAperture(e.mousePosition, lensRect))
                return;

            if (e.type == EventType.ScrollWheel)
            {
                float minZoom = Mathf.Max(1f, _observationSettings.minimumZoom);
                float maxZoom = Mathf.Max(minZoom, _observationSettings.maximumZoom);
                float factor = Mathf.Pow(1.12f, -e.delta.y);
                _zoom = Mathf.Clamp(_zoom * factor, minZoom, maxZoom);
                _instrumentCenter01 = ClampInstrumentCenter(_instrumentCenter01, _zoom);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag &&
                     (e.button == 1 || (_stage == ObservationStage.Calibration && e.button == 0)))
            {
                // RMB pans at every stage. During calibration LMB also pans because star selection is
                // not active yet; this lets the player inspect/reframe the sky before seating optics.
                // Mouse drag is expressed in telescope/view coordinates. _instrumentCenter01 lives in
                // unrotated celestial/base coordinates, so convert the pan delta back through the
                // inverse instrument rotation before applying it. This keeps drag direction screen-
                // relative: dragging right always moves the star field right, regardless of bearing.
                Vector2 viewCenterDelta = new Vector2(
                    -e.delta.x / Mathf.Max(1f, lensRect.width),
                     e.delta.y / Mathf.Max(1f, lensRect.height));

                Vector2 baseCenterDelta = Rotate(viewCenterDelta, -EffectiveRotationDegrees);
                _instrumentCenter01 = ClampInstrumentCenter(
                    _instrumentCenter01 + baseCenterDelta / Mathf.Max(1f, _zoom),
                    _zoom);
                e.Use();
            }
        }

        private void UpdateProjectedScreenPoints(Rect lensRect)
        {
            float aperture = Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);

            for (int i = 0; i < _projectedObjects.Count; i++)
            {
                ProjectedObject p = _projectedObjects[i];
                Vector2 instrument = BaseToInstrumentViewport(p.baseViewport01, EffectiveRotationDegrees);
                p.instrumentViewport01 = instrument;
                p.screenPoint = InstrumentToScreen(instrument, lensRect);
                p.insideAperture = Vector2.Distance(instrument, new Vector2(0.5f, 0.5f)) <= aperture;
            }
        }

        private void AccumulateVisibleEvidence(float visibility)
        {
            if (_stage == ObservationStage.Calibration ||
                visibility < MinimumVisibility ||
                _observationSettings == null)
            {
                return;
            }

            for (int i = 0; i < _projectedObjects.Count; i++)
            {
                ProjectedObject p = _projectedObjects[i];
                CelestialObject obj = p != null ? p.celestialObject : null;
                if (obj == null || !p.insideAperture || string.IsNullOrWhiteSpace(obj.StableId))
                    continue;

                bool isAnchor = _patternIds.Contains(obj.StableId);
                if (!ShouldCapture(obj, isAnchor))
                    continue;

                // Keep the first legitimate in-aperture sighting. The player may later pan/zoom away;
                // evidence does not evaporate merely because the last telescope frame changed.
                if (!_observedInstrumentPositions.ContainsKey(obj.StableId))
                    _observedInstrumentPositions.Add(obj.StableId, p.instrumentViewport01);
            }
        }

        private Vector2 BaseToInstrumentViewport(Vector2 baseViewport, float rotationDegrees)
        {
            Vector2 local = (baseViewport - _instrumentCenter01) * _zoom;
            local = Rotate(local, rotationDegrees);
            return new Vector2(0.5f, 0.5f) + local;
        }

        private void DrawTelescopeGuide(Rect lensRect)
        {
            Vector2 c = lensRect.center;
            float r = lensRect.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            Color grid = new Color(0.36f, 0.70f, 0.82f, 0.18f);

            DrawLine(new Vector2(c.x - r, c.y), new Vector2(c.x + r, c.y), grid, 1f);
            DrawLine(new Vector2(c.x, c.y - r), new Vector2(c.x, c.y + r), grid, 1f);
            DrawCircleOutline(RectFromCenter(c, r * 1.00f), new Color(0.36f, 0.70f, 0.82f, 0.16f), 1f, 96);
            DrawCircleOutline(RectFromCenter(c, r * 0.58f), new Color(0.36f, 0.70f, 0.82f, 0.11f), 1f, 96);
        }

        private void DrawCelestialObjects(Rect lensRect, float visibility)
        {
            float alphaScale = Mathf.Clamp01(visibility);

            for (int i = 0; i < _projectedObjects.Count; i++)
            {
                ProjectedObject p = _projectedObjects[i];
                CelestialObject obj = p.celestialObject;
                if (obj == null || !p.insideAperture)
                    continue;

                Color color = ResolveObjectColor(obj);
                color.a *= alphaScale;

                switch (obj.Kind)
                {
                    case CelestialObjectKind.AmbientStar:
                    {
                        float size = Mathf.Lerp(1.5f, 3.8f, obj.Brightness01);
                        DrawStarPoint(p.screenPoint, size, color, obj.Brightness01);
                        break;
                    }

                    case CelestialObjectKind.LandmarkStar:
                    {
                        float size = Mathf.Lerp(5f, 11f, obj.Prominence01);
                        DrawStarPoint(p.screenPoint, size, color, 1f);
                        DrawLine(p.screenPoint + new Vector2(-size, 0f), p.screenPoint + new Vector2(size, 0f), new Color(color.r, color.g, color.b, color.a * 0.72f), 1.2f);
                        DrawLine(p.screenPoint + new Vector2(0f, -size), p.screenPoint + new Vector2(0f, size), new Color(color.r, color.g, color.b, color.a * 0.72f), 1.2f);
                        break;
                    }

                    case CelestialObjectKind.Nebula:
                    {
                        float size = Mathf.Lerp(16f, 42f, obj.Prominence01);
                        DrawDisc(p.screenPoint, size, new Color(color.r, color.g, color.b, color.a * 0.16f), 1f);
                        break;
                    }

                    case CelestialObjectKind.DeepSkyObject:
                    {
                        float radius = Mathf.Lerp(7f, 14f, obj.Prominence01);
                        DrawCircleOutline(RectFromCenter(p.screenPoint, radius), new Color(color.r, color.g, color.b, color.a * 0.8f), 1.2f, 36);
                        DrawLine(p.screenPoint + new Vector2(-radius * 0.6f, 0f), p.screenPoint + new Vector2(radius * 0.6f, 0f), new Color(color.r, color.g, color.b, color.a * 0.55f), 1f);
                        break;
                    }
                }
            }
        }

        private void DrawDebugExpectedStars(Rect lensRect)
        {
            if (!_debugHighlightExpectedStars || _patternIds.Count == 0)
                return;

            float apertureRadius = lensRect.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            Vector2 center = lensRect.center;

            for (int i = 0; i < _patternIds.Count; i++)
            {
                ProjectedObject p = FindProjected(_patternIds[i]);
                if (p == null)
                    continue;

                Vector2 delta = p.screenPoint - center;
                float distance = delta.magnitude;

                if (distance <= apertureRadius)
                {
                    // Two rings make the debug reveal unmistakable without replacing the star itself.
                    DrawCircleOutline(RectFromCenter(p.screenPoint, 14f), DebugTarget, 2.2f, 32);
                    DrawCircleOutline(
                        RectFromCenter(p.screenPoint, 19f),
                        new Color(DebugTarget.r, DebugTarget.g, DebugTarget.b, 0.38f),
                        1.2f,
                        36);
                }
                else if (distance > 0.001f)
                {
                    // If the expected landmark is currently outside the aperture, leave a debug pip
                    // on the lens rim in its direction. This proves the target exists and tells us it
                    // is reachable by panning, without teleporting the telescope or altering gameplay.
                    Vector2 direction = delta / distance;
                    Vector2 edgePoint = center + direction * Mathf.Max(0f, apertureRadius - 10f);
                    DrawCircleOutline(RectFromCenter(edgePoint, 6f), DebugTarget, 2f, 20);
                    DrawLine(
                        edgePoint - direction * 5f,
                        edgePoint + direction * 5f,
                        DebugTarget,
                        1.8f);
                }
            }
        }

        private void DrawObservationOrigin(Rect lensRect)
        {
            Vector2 originUv = BaseToInstrumentViewport(new Vector2(0.5f, 0.5f), EffectiveRotationDegrees);
            float aperture = Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            if (Vector2.Distance(originUv, new Vector2(0.5f, 0.5f)) > aperture)
                return;

            Vector2 p = InstrumentToScreen(originUv, lensRect);
            DrawPaperBoatMarker(p, 10f, Warm, new Color(0.05f, 0.08f, 0.10f, 0.95f), 1.8f);
        }

        private void HandleTraceInput(Rect lensRect, float visibility)
        {
            Event e = Event.current;
            if (e == null)
                return;

            _hoveredStarId = null;
            if (PointInsideAperture(e.mousePosition, lensRect))
            {
                ProjectedObject hover = FindNearestStar(e.mousePosition, _observationSettings.notableClickRadiusPixels);
                if (hover != null)
                    _hoveredStarId = hover.celestialObject.StableId;
            }

            if (e.type != EventType.MouseDown || e.button != 0 || !PointInsideAperture(e.mousePosition, lensRect))
                return;

            if (visibility < MinimumVisibility)
            {
                SetStatus("Stars are not visible enough to transcribe.", 1.5f);
                e.Use();
                return;
            }

            ProjectedObject target = FindNearestStar(e.mousePosition, _observationSettings.notableClickRadiusPixels);
            if (target == null)
                return;

            TryAppendTrace(target.celestialObject.StableId);
            e.Use();
        }

        private void TryAppendTrace(string stableId)
        {
            if (string.IsNullOrEmpty(stableId) || _patternIds.Count == 0)
                return;

            // During transcription the instrument is deliberately unhelpful. Selected stars are
            // merely recorded; nothing is judged until the complete pattern-sized sequence exists.
            // Clicking an already-selected star toggles it off so accidental input can be corrected
            // before committing the full attempt.
            int existingIndex = _traceIds.IndexOf(stableId);
            if (existingIndex >= 0)
            {
                _traceIds.RemoveAt(existingIndex);
                return;
            }

            _traceIds.Add(stableId);

            if (_traceIds.Count < _patternIds.Count)
                return;

            bool forward = true;
            bool reverse = true;

            for (int i = 0; i < _patternIds.Count; i++)
            {
                if (_traceIds[i] != _patternIds[i])
                    forward = false;

                if (_traceIds[i] != _patternIds[_patternIds.Count - 1 - i])
                    reverse = false;
            }

            if (forward || reverse)
            {
                _stage = ObservationStage.ReadyToRecord;
                SetStatus("Pattern acquired.", 2f);
                return;
            }

            // No hint about which selection was wrong. The entire attempt is discarded. Future perks
            // can soften this behavior without weakening the baseline charting puzzle.
            _traceIds.Clear();
            SetStatus("Pattern mismatch. Selection cleared.", 1.5f);
        }

        private void DrawTrace(Rect lensRect)
        {
            if (_traceIds.Count < 1)
                return;

            for (int i = 0; i < _traceIds.Count - 1; i++)
            {
                ProjectedObject a = FindProjected(_traceIds[i]);
                ProjectedObject b = FindProjected(_traceIds[i + 1]);
                if (a != null && b != null)
                {
                    // Keep selected connections visible even when one or both selected stars pan
                    // outside the telescope, but clip the segment mathematically to the circular
                    // aperture. The visual mask only covers the lens rectangle; without true segment
                    // clipping a long connection can otherwise escape the telescope and scribble over
                    // adjacent UI panels.
                    float apertureRadius = lensRect.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
                    DrawLineClippedToCircle(
                        a.screenPoint,
                        b.screenPoint,
                        lensRect.center,
                        apertureRadius,
                        _stage == ObservationStage.ReadyToRecord ? Success : Cyan,
                        2.4f);
                }
            }

            for (int i = 0; i < _traceIds.Count; i++)
            {
                ProjectedObject p = FindProjected(_traceIds[i]);
                if (p != null && p.insideAperture)
                    DrawCircleOutline(RectFromCenter(p.screenPoint, 9f), _stage == ObservationStage.ReadyToRecord ? Success : Cyan, 2f, 28);
            }
        }

        private void DrawHoverAndErrorFeedback()
        {
            if (!string.IsNullOrEmpty(_hoveredStarId))
            {
                ProjectedObject p = FindProjected(_hoveredStarId);
                if (p != null && p.insideAperture)
                    DrawCircleOutline(RectFromCenter(p.screenPoint, 8f), new Color(1f, 1f, 1f, 0.75f), 1.4f, 24);
            }

            if (!string.IsNullOrEmpty(_wrongFlashStarId) && Time.unscaledTime <= _wrongFlashUntil)
            {
                ProjectedObject p = FindProjected(_wrongFlashStarId);
                if (p != null && p.insideAperture)
                    DrawCircleOutline(RectFromCenter(p.screenPoint, 11f), Error, 2.4f, 28);
            }
        }

        private void LockCalibration()
        {
            if (!IsOpticsWithinTolerance)
                return;

            // The player got the optical mechanism inside its lock tolerances. Mechanical detents
            // seat only the optics on the exact solution. Bearing remains completely untouched and
            // freely adjustable, because the decoder intentionally withholds the pattern's rotation.
            _plateControl = _plateSolution;
            _lensControl = _lensSolution;
            _prismControl = _prismSolution;

            _stage = ObservationStage.TracePattern;
            _traceIds.Clear();
            _draggingBearingDial = false;
            SetStatus("Optics seated. Find the landmark pattern; rotation remains unknown.", 2.0f);
        }

        private void ReturnToCalibration()
        {
            _stage = ObservationStage.Calibration;
            _traceIds.Clear();
            SetStatus("Calibration released.", 1f);
        }

        private void ClearTrace(string status)
        {
            _traceIds.Clear();
            if (_stage == ObservationStage.ReadyToRecord)
                _stage = ObservationStage.TracePattern;
            SetStatus(status, 0.9f);
        }

        private void CompleteObservation(float visibility)
        {
            if (_completed || _stage != ObservationStage.ReadyToRecord || visibility < MinimumVisibility)
                return;

            CelestialObservation observation = BuildObservation(visibility);
            if (observation == null)
                return;

            string commitError = _tryCommitObservation != null
                ? _tryCommitObservation(observation)
                : null;

            if (!string.IsNullOrWhiteSpace(commitError))
            {
                SetStatus(commitError, 2.5f);
                return;
            }

            _completedObservation = observation;
            _completed = true;
        }

        private CelestialObservation BuildObservation(float visibility)
        {
            if (_field == null || _field.Identity == null)
                return null;

            var result = new CelestialObservation
            {
                observationId = Guid.NewGuid().ToString("N"),
                source = CelestialObservationSource.PlayerCharted,
                worldSeed = _field.WorldSeed,
                celestialGeneratorVersion = _field.Identity.generatorVersion,
                celestialConfigHash = _field.Identity.configHash,
                observerTrueWorldPosition = _observerWorldPosition,
                baseVisibleWorldRect = _baseVisibleWorldRect,
                instrumentCenter01 = _instrumentCenter01,
                instrumentZoom = _zoom,
                instrumentRotationDegrees = EffectiveRotationDegrees,
                circularApertureRadius01 = _observationSettings.apertureRadius01,
                chartReticle01 = new Rect(0f, 0f, 1f, 1f),
                calibrationQuality01 = CalibrationQuality01(),
                bearingControlDegrees = _bearingControlDegrees,
                plateControl = _plateControl,
                lensControl = _lensControl,
                prismControl = _prismControl,
                surveySequence = _surveySequence,
                starVisibility01 = visibility,
                referenceObjectStableId = null
            };

            for (int i = 0; i < _patternIds.Count; i++)
                result.patternObjectStableIds.Add(_patternIds[i]);

            TimeOfDayManager time = _timeManagerProvider != null ? _timeManagerProvider() : null;
            if (time != null)
            {
                result.capturedHour = time.CurrentTime;
                result.capturedYear = time.Year;
                result.capturedMonth = time.Month;
                result.capturedDay = time.Day;
            }

            float aperture = Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            for (int i = 0; i < _projectedObjects.Count; i++)
            {
                ProjectedObject p = _projectedObjects[i];
                CelestialObject obj = p.celestialObject;
                if (obj == null)
                    continue;

                bool isAnchor = _patternIds.Contains(obj.StableId);
                bool wasObserved = _observedInstrumentPositions.TryGetValue(
                    obj.StableId,
                    out Vector2 observedInstrumentPosition);

                // Pattern anchors are always valid solved evidence. Ordinary context objects must have
                // actually entered the calibrated aperture at some point during THIS observation.
                // This turns the fragment into the union of the player's telescope sweep instead of a
                // snapshot of whatever happened to be visible when RECORD OBSERVATION was clicked.
                if (!isAnchor && !wasObserved)
                    continue;

                if (!ShouldCapture(obj, isAnchor))
                    continue;

                result.objects.Add(new CelestialObservationObject
                {
                    stableId = obj.StableId,
                    kind = obj.Kind,
                    colorClass = obj.ColorClass,
                    worldPosition = obj.WorldPosition,
                    brightness01 = obj.Brightness01,
                    prominence01 = obj.Prominence01,
                    instrumentPosition01 = wasObserved
                        ? observedInstrumentPosition
                        : p.instrumentViewport01,
                    isAnchor = isAnchor,
                    isReference = false
                });
            }

            // Trace correctness is binary by this point; quality is observational rather than a hidden
            // minigame score. Near-perfect calibration plus good visibility produces stronger evidence.
            float calibration = CalibrationQuality01();
            float patternSpread = ComputePatternSpreadQuality(aperture);
            result.quality01 = Mathf.Clamp01(visibility * Mathf.Lerp(0.90f, 1f, calibration) * Mathf.Lerp(0.92f, 1f, patternSpread));
            return result;
        }

        private float ComputePatternSpreadQuality(float aperture)
        {
            if (_patternObjects.Count < 2)
                return 0f;

            float largest = 0f;
            for (int i = 0; i < _patternObjects.Count; i++)
            {
                for (int j = i + 1; j < _patternObjects.Count; j++)
                {
                    Vector2 a = BaseToInstrumentViewport(_patternObjects[i].baseViewport01, EffectiveRotationDegrees);
                    Vector2 b = BaseToInstrumentViewport(_patternObjects[j].baseViewport01, EffectiveRotationDegrees);
                    largest = Mathf.Max(largest, Vector2.Distance(a, b));
                }
            }

            return Mathf.InverseLerp(aperture * 0.35f, aperture * 1.5f, largest);
        }

        private bool ShouldCapture(CelestialObject obj, bool isAnchor)
        {
            if (isAnchor)
                return true;

            if (obj.Kind == CelestialObjectKind.AmbientStar)
                return _observationSettings.captureAmbientStars;

            return _observationSettings.captureUnselectedNotableObjects;
        }

        private ProjectedObject FindNearestStar(Vector2 mouse, float clickRadius)
        {
            ProjectedObject best = null;
            float bestDistance = Mathf.Max(4f, clickRadius);

            for (int i = 0; i < _projectedObjects.Count; i++)
            {
                ProjectedObject p = _projectedObjects[i];
                if (!p.insideAperture || p.celestialObject == null || p.celestialObject.Kind != CelestialObjectKind.LandmarkStar)
                    continue;

                float d = Vector2.Distance(mouse, p.screenPoint);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        private ProjectedObject FindProjected(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
                return null;

            for (int i = 0; i < _projectedObjects.Count; i++)
            {
                ProjectedObject p = _projectedObjects[i];
                if (p.celestialObject != null && p.celestialObject.StableId == stableId)
                    return p;
            }

            return null;
        }

        private bool IsOpticsWithinTolerance
        {
            get
            {
                if (_observationSettings == null)
                    return false;

                ComputeRegistrationOffsets(out Vector2 a, out Vector2 b, out Vector2 c);
                float tolerance = Mathf.Clamp(_observationSettings.registrationPipTolerance, 0.04f, 0.30f);
                return a.magnitude <= tolerance && b.magnitude <= tolerance && c.magnitude <= tolerance;
            }
        }

        private float CalibrationQuality01()
        {
            if (_observationSettings == null)
                return 0f;

            ComputeRegistrationOffsets(out Vector2 a, out Vector2 b, out Vector2 c);
            float averageOffset = (a.magnitude + b.magnitude + c.magnitude) / 3f;
            return 1f - Mathf.Clamp01(averageOffset / 1.25f);
        }

        private float MinimumVisibility => _observationSettings != null
            ? Mathf.Clamp01(_observationSettings.minimumStarVisibilityToInteract)
            : 0.2f;

        private float GetStarVisibility01()
        {
            return Mathf.Clamp01(_starVisibilityProvider != null ? _starVisibilityProvider.Invoke() : 1f);
        }

        private void FlashWrong(string stableId, string message)
        {
            _wrongFlashStarId = stableId;
            _wrongFlashUntil = Time.unscaledTime + 0.7f;
            SetStatus(message, 1.4f);
        }

        private void SetStatus(string message, float seconds)
        {
            _statusMessage = message;
            _statusMessageUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }

        private Color ResolveObjectColor(CelestialObject obj)
        {
            if (_projectionSettings == null || obj == null)
                return Color.white;

            float saturation = obj.Kind == CelestialObjectKind.AmbientStar
                ? _projectionSettings.ambientColorSaturation
                : obj.Kind == CelestialObjectKind.LandmarkStar
                    ? _projectionSettings.landmarkColorSaturation
                    : obj.Kind == CelestialObjectKind.Nebula
                        ? _projectionSettings.nebulaColorSaturation
                        : _projectionSettings.deepSkyColorSaturation;

            return _projectionSettings.ResolveColor(obj.ColorClass, saturation);
        }

        private static bool IsStar(CelestialObject obj)
        {
            return obj != null &&
                   (obj.Kind == CelestialObjectKind.AmbientStar || obj.Kind == CelestialObjectKind.LandmarkStar);
        }

        private static Vector2 InstrumentToScreen(Vector2 uv, Rect rect)
        {
            return new Vector2(
                Mathf.LerpUnclamped(rect.xMin, rect.xMax, uv.x),
                Mathf.LerpUnclamped(rect.yMax, rect.yMin, uv.y));
        }

        private bool PointInsideAperture(Vector2 point, Rect lensRect)
        {
            float radius = lensRect.width * Mathf.Clamp(_observationSettings.apertureRadius01, 0.38f, 0.495f);
            return Vector2.Distance(point, lensRect.center) <= radius;
        }

        private Vector2 ClampInstrumentCenter(Vector2 center, float zoom)
        {
            // baseViewport01 is now metric celestial space. The authored query rectangle is generally
            // wider than it is tall, so its shape-preserving Y extent occupies only the middle portion
            // of the 0..1 metric square. Clamp against those REAL source bounds rather than pretending
            // usable celestial data fills a distorted square.
            float safeZoom = Mathf.Max(1f, zoom);
            float halfView = 0.5f / safeZoom;

            float sourceHalfY = 0.5f;
            if (_baseVisibleWorldRect.width > 0.0001f && _baseVisibleWorldRect.height > 0.0001f)
            {
                sourceHalfY = Mathf.Clamp(
                    0.5f * (_baseVisibleWorldRect.height / _baseVisibleWorldRect.width),
                    0.0001f,
                    0.5f);
            }

            center.x = ClampCenterAxis(center.x, 0f, 1f, halfView);
            center.y = ClampCenterAxis(center.y, 0.5f - sourceHalfY, 0.5f + sourceHalfY, halfView);
            return center;
        }

        private static float ClampCenterAxis(float center, float sourceMin, float sourceMax, float halfView)
        {
            float minCenter = sourceMin + halfView;
            float maxCenter = sourceMax - halfView;

            if (minCenter > maxCenter)
                return (sourceMin + sourceMax) * 0.5f;

            return Mathf.Clamp(center, minCenter, maxCenter);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static Vector2 DecoderPointToScreen(Vector2 p, Vector2 center, float radius)
        {
            return center + new Vector2(p.x * radius, -p.y * radius);
        }

        private static float Next01(System.Random rng)
        {
            return (float)rng.NextDouble();
        }

        private static float PickSeparatedControl(System.Random rng, float solution, float minimumDifference)
        {
            for (int i = 0; i < 20; i++)
            {
                float candidate = Mathf.Lerp(-1f, 1f, Next01(rng));
                if (Mathf.Abs(candidate - solution) >= minimumDifference)
                    return candidate;
            }

            return solution > 0f ? -0.9f : 0.9f;
        }

        private static string FormatHour(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            return $"{h:00}:{m:00}";
        }

        private void DrawCard(Rect rect)
        {
            DrawSolidRect(rect, CardColor);
            DrawOutline(rect, new Color(0.26f, 0.34f, 0.40f, 0.85f), 1f);
        }

        private void DrawMeter(Rect rect, float value, Color fill)
        {
            DrawSolidRect(rect, new Color(0.02f, 0.025f, 0.032f, 1f));
            float clamped = Mathf.Clamp01(value);
            if (clamped > 0f)
                DrawSolidRect(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, (rect.width - 2f) * clamped), Mathf.Max(0f, rect.height - 2f)), fill);
            DrawOutline(rect, new Color(0.34f, 0.42f, 0.48f, 0.9f), 1f);
        }

        private void DrawStarPoint(Vector2 center, float radius, Color color, float glow)
        {
            DrawDisc(center, radius * 2.4f, new Color(color.r, color.g, color.b, color.a * 0.16f * Mathf.Lerp(0.5f, 1f, glow)), 1f);
            DrawDisc(center, radius, color, 1f);
            DrawDisc(center, Mathf.Max(0.8f, radius * 0.35f), new Color(1f, 1f, 1f, color.a), 1f);
        }

        private void DrawDisc(Vector2 center, float radius, Color color, float alphaScale)
        {
            if (_softDisc == null)
                return;

            Color old = GUI.color;
            color.a *= alphaScale;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), _softDisc);
            GUI.color = old;
        }

        private void ApplyCircularMask(Rect rect, Color maskColor)
        {
            if (_outsideCircleMask == null)
                return;

            Color old = GUI.color;
            GUI.color = maskColor;
            GUI.DrawTexture(rect, _outsideCircleMask);
            GUI.color = old;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length <= 0.001f)
                return;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static void DrawLineClippedToCircle(
            Vector2 a,
            Vector2 b,
            Vector2 center,
            float radius,
            Color color,
            float thickness)
        {
            Vector2 d = b - a;
            float aa = Vector2.Dot(d, d);
            if (aa <= 0.000001f || radius <= 0f)
                return;

            Vector2 f = a - center;
            float bb = 2f * Vector2.Dot(f, d);
            float cc = Vector2.Dot(f, f) - radius * radius;
            float discriminant = bb * bb - 4f * aa * cc;

            if (discriminant < 0f)
            {
                // No boundary crossing. The whole segment is drawable only if it lives inside.
                if (Vector2.SqrMagnitude(a - center) <= radius * radius &&
                    Vector2.SqrMagnitude(b - center) <= radius * radius)
                {
                    DrawLine(a, b, color, thickness);
                }
                return;
            }

            float root = Mathf.Sqrt(discriminant);
            float inv = 1f / (2f * aa);
            float t0 = (-bb - root) * inv;
            float t1 = (-bb + root) * inv;
            if (t0 > t1)
            {
                float tmp = t0;
                t0 = t1;
                t1 = tmp;
            }

            float enter = Mathf.Max(0f, t0);
            float exit = Mathf.Min(1f, t1);
            if (exit < enter)
                return;

            DrawLine(a + d * enter, a + d * exit, color, thickness);
        }

        private static void DrawCircleOutline(Rect rect, Color color, float thickness, int segments)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 center = rect.center;
            Vector2 previous = center + new Vector2(radius, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector2 next = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                DrawLine(previous, next, color, thickness);
                previous = next;
            }
        }


        private void DrawPaperBoatMarker(Vector2 center, float size, Color lineColor, Color fillColor, float lineThickness)
        {
            float s = Mathf.Max(4f, size);
            Vector2 leftDeck = center + new Vector2(-s * 0.92f, s * 0.10f);
            Vector2 rightDeck = center + new Vector2(s * 0.92f, s * 0.10f);
            Vector2 hullLeft = center + new Vector2(-s * 0.55f, s * 0.78f);
            Vector2 hullRight = center + new Vector2(s * 0.55f, s * 0.78f);
            Vector2 mastTop = center + new Vector2(0f, -s * 0.92f);
            Vector2 sailLeftBase = center + new Vector2(-s * 0.22f, s * 0.10f);

            DrawLine(leftDeck, rightDeck, lineColor, lineThickness);
            DrawLine(leftDeck, hullLeft, lineColor, lineThickness);
            DrawLine(hullLeft, hullRight, lineColor, lineThickness);
            DrawLine(hullRight, rightDeck, lineColor, lineThickness);
            DrawLine(sailLeftBase, mastTop, lineColor, lineThickness);
            DrawLine(mastTop, rightDeck, lineColor, lineThickness);

            DrawDisc(center + new Vector2(0f, s * 0.28f), 1.8f, fillColor, 1f);
        }

        private static Rect ShrinkRect(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, Mathf.Max(1f, rect.width - amount * 2f), Mathf.Max(1f, rect.height - amount * 2f));
        }

        private static Rect RectFromCenter(Vector2 center, float radius)
        {
            return new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        }

        private static Texture2D BuildSoftDiscTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "CelestialObservation_SoftDisc",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildOutsideCircleMaskTexture(int size, float radius01)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "CelestialObservation_OutsideCircleMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * Mathf.Clamp(radius01, 0.38f, 0.495f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius - 1.5f, radius + 1.5f, d));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.86f, 0.94f, 1f, 1f) }
            };

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.64f, 0.84f, 0.92f, 1f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _smallLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.87f, 0.90f, 1f) }
            };

            _centerStyle = new GUIStyle(_smallLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }
    }
}
