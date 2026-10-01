using MiniGames;
using UnityEngine;

/// <summary>
/// Scene-level celestial observation entry point.
/// F6 remains a temporary debug opener. Future physical instruments should call
/// OpenObservationFor(requester) so the exact interacting player is preserved.
/// </summary>
[DisallowMultipleComponent]
public sealed class CelestialObservationOverlayRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MiniGameOverlayHost overlay;
    [SerializeField] private CelestialFieldSource fieldSource;
    [SerializeField] private CelestialSkyProjectionSettings projectionSettings;
    [SerializeField] private CelestialObservationSettings observationSettings;
    [SerializeField] private SkyVisualManager skyVisualManager;

    [Header("Phase 5 Charting")]
    [Tooltip("Normal inventory item consumed when a successful observation is committed to paper.")]
    [SerializeField] private ItemDefinition chartingPaperDefinition;

    [Header("Phase 5B Fragment Visuals")]
    [Tooltip("Optional tuning asset. Leave blank to use the built-in Phase 5B defaults.")]
    [SerializeField] private CelestialChartFragmentVisualSettings fragmentVisualSettings;

    [Tooltip("Temporary debug preview for generated physical scraps. Phase 6 replaces this with the real chart table.")]
    [SerializeField] private bool enableFragmentPreviewHotkey = true;
    [SerializeField] private KeyCode fragmentPreviewKey = KeyCode.F7;

    [Header("Debug Entry")]
    [SerializeField] private bool enableDebugHotkey = true;
    [SerializeField] private KeyCode debugOpenKey = KeyCode.F6;

    [Header("Runtime Debug")]
    [SerializeField] private CelestialObservation lastObservation;
    [SerializeField] private CelestialChartFragmentSnapshot lastFragment;
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private int currentSurveySequence;

    public CelestialObservation LastObservation => lastObservation;
    public CelestialChartFragmentSnapshot LastFragment => lastFragment;

    private bool _warnedMissingSkyVisualManager;
    private GameObject _activeRequester;

    private bool _showFragmentPreview;
    private int _previewFragmentIndex = -1;
    private string _previewFragmentId;
    private CelestialChartFragmentVisual _previewFragmentVisual;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
    }

    private void Update()
    {
        if (enableFragmentPreviewHotkey &&
            fragmentPreviewKey != KeyCode.None &&
            Input.GetKeyDown(fragmentPreviewKey))
        {
            ToggleFragmentPreview();
        }

        if (!enableDebugHotkey || debugOpenKey == KeyCode.None)
            return;

        if (Input.GetKeyDown(debugOpenKey))
        {
            if (overlay != null && overlay.IsOpen)
                return;

            OpenObservation();
        }
    }

    private void OnDestroy()
    {
        ReleasePreviewVisual();
    }

    private void OnGUI()
    {
        if (!_showFragmentPreview)
            return;

        DrawFragmentPreviewGUI();
    }

    [ContextMenu("Celestial Observation / Open (Debug Requester)")]
    public void OpenObservation()
    {
        GameObject requester = ResolveUniqueDebugRequester();
        if (requester == null)
            return;

        OpenObservationFor(requester);
    }

    /// <summary>
    /// Requester-preserving entry point for future telescope/astrolabe interactions.
    /// A future network transport should authenticate the sender and resolve that sender
    /// to the authoritative requester before invoking the charting authority transaction.
    /// </summary>
    public void OpenObservationFor(GameObject requester)
    {
        AutoWire();

        if (requester == null)
        {
            Debug.LogError("[CelestialObservationOverlayRunner] OpenObservationFor requires an exact requester GameObject.", this);
            return;
        }

        if (overlay == null)
        {
            Debug.LogError("[CelestialObservationOverlayRunner] Missing MiniGameOverlayHost.", this);
            return;
        }

        if (fieldSource == null || !fieldSource.EnsureField() || fieldSource.Field == null)
        {
            Debug.LogError("[CelestialObservationOverlayRunner] CelestialFieldSource is missing or could not build the field.", this);
            return;
        }

        if (projectionSettings == null)
        {
            Debug.LogError("[CelestialObservationOverlayRunner] Missing CelestialSkyProjectionSettings. Use the SAME asset as CelestialSkyRenderer.", this);
            return;
        }

        if (observationSettings == null)
        {
            Debug.LogError("[CelestialObservationOverlayRunner] Missing CelestialObservationSettings.", this);
            return;
        }

        if (chartingPaperDefinition == null)
        {
            Debug.LogError("[CelestialObservationOverlayRunner] Missing Charting Paper ItemDefinition.", this);
            return;
        }

        if (!WorldNavigationService.TryGetTrueWorldPosition(out Vector2 observerWorldPosition))
        {
            Debug.LogWarning("[CelestialObservationOverlayRunner] No authoritative true world position is available yet.", this);
            return;
        }

        _activeRequester = requester;

        currentSurveySequence = CelestialSurveySequenceTracker.GetCurrentSequence(
            fieldSource.Field,
            observerWorldPosition,
            observationSettings.surveyRegionSizeWorld);

        var context = new MiniGameContext
        {
            targetId = "celestial-observation",
            difficulty = 1f,
            pressure = 0f,
            seed = fieldSource.Field.WorldSeed ^ (currentSurveySequence * 83492791)
        };

        var cartridge = new CelestialObservationCartridge(
            fieldSource.Field,
            observerWorldPosition,
            projectionSettings,
            observationSettings,
            ResolveStarVisibility,
            ResolveTimeManager,
            currentSurveySequence,
            TryCommitObservation);

        overlay.Open(cartridge, context);

        if (verboseLogging)
        {
            Debug.Log(
                $"[CelestialObservationOverlayRunner] Opened at true world position " +
                $"({observerWorldPosition.x:0.000}, {observerWorldPosition.y:0.000}), " +
                $"Requester='{requester.name}', SurveySequence={currentSurveySequence}, " +
                $"StarVisibility={ResolveStarVisibility():0.00}.",
                this);
        }
    }

    private string TryCommitObservation(CelestialObservation observation)
    {
        CelestialChartCommitResult result = CelestialChartingAuthority.TryCommitObservation(
            _activeRequester,
            fieldSource != null ? fieldSource.Field : null,
            observationSettings,
            chartingPaperDefinition,
            observation);

        if (!result.success)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    $"[CelestialObservationOverlayRunner] Chart commit rejected: {result.message}",
                    this);
            }

            return result.message;
        }

        lastObservation = observation;
        lastFragment = result.fragment;
        currentSurveySequence = observation != null ? observation.surveySequence + 1 : currentSurveySequence;

        if (lastFragment != null)
        {
            _previewFragmentIndex = ResolveFragmentIndex(lastFragment.fragmentId);
            ReleasePreviewVisual();

            // Explicit player-facing handoff. The fragment is persistent chart evidence,
            // not a normal hotbar ItemInstance, so make its destination unambiguous.
            GameMessageService.PostInfo("Chart fragment added to map table.");
        }

        if (verboseLogging && observation != null)
        {
            Debug.Log(
                $"[CelestialObservationOverlayRunner] RECORDED fragment={lastFragment?.fragmentId ?? "NULL"} " +
                $"observation={observation.observationId} objects={observation.objects?.Count ?? 0} " +
                $"anchors={observation.AnchorCount} surveySequence={observation.surveySequence} " +
                $"quality={observation.quality01:0.00}.",
                this);
        }

        return null;
    }

    [ContextMenu("Celestial Observation / Log Last Observation")]
    private void LogLastObservation()
    {
        if (lastObservation == null)
        {
            Debug.Log("[CelestialObservationOverlayRunner] No completed observation yet.", this);
            return;
        }

        Debug.Log(
            $"[CelestialObservationOverlayRunner] Observation={lastObservation.observationId} " +
            $"Origin=({lastObservation.observerTrueWorldPosition.x:0.000}, {lastObservation.observerTrueWorldPosition.y:0.000}) " +
            $"Date={lastObservation.capturedYear}/{lastObservation.capturedMonth}/{lastObservation.capturedDay} " +
            $"Hour={lastObservation.capturedHour:0.00} Visibility={lastObservation.starVisibility01:0.00} " +
            $"Quality={lastObservation.quality01:0.00} Calibration={lastObservation.calibrationQuality01:0.00} " +
            $"SurveySequence={lastObservation.surveySequence} Objects={lastObservation.objects?.Count ?? 0} " +
            $"Anchors={lastObservation.AnchorCount}",
            this);
    }

    [ContextMenu("Celestial Observation / Log Last Fragment")]
    private void LogLastFragment()
    {
        if (lastFragment == null)
        {
            Debug.Log("[CelestialObservationOverlayRunner] No committed chart fragment yet.", this);
            return;
        }

        Debug.Log(
            $"[CelestialObservationOverlayRunner] Fragment={lastFragment.fragmentId} " +
            $"Observation={lastFragment.observationId} Creator='{lastFragment.createdByPlayerKey}' " +
            $"Region='{lastFragment.surveyRegionKey}' Sequence={lastFragment.surveySequence} " +
            $"Marks={lastFragment.marks?.Count ?? 0} Pattern={lastFragment.patternObjectStableIds?.Count ?? 0} " +
            $"VisualSeed={lastFragment.visualSeed}",
            this);
    }

    [ContextMenu("Celestial Observation / DEBUG Give 5 Charting Paper")]
    private void DebugGiveChartingPaper()
    {
        if (chartingPaperDefinition == null)
        {
            Debug.LogWarning("[CelestialObservationOverlayRunner] Assign Charting Paper ItemDefinition first.", this);
            return;
        }

        GameObject requester = ResolveUniqueDebugRequester();
        if (requester == null)
            return;

        PlayerInventory inventory = CelestialChartPaperConsumption.ResolveInventory(requester);
        if (inventory == null)
        {
            Debug.LogWarning("[CelestialObservationOverlayRunner] Debug requester has no PlayerInventory.", this);
            return;
        }

        ItemInstance stack = ItemInstance.Create(chartingPaperDefinition, 5);
        bool inserted = inventory.TryAutoInsert(stack, out ItemInstance remainder);

        int remaining = remainder != null ? remainder.Quantity : 0;
        Debug.Log(
            $"[CelestialObservationOverlayRunner] DEBUG chart paper insert attempted. " +
            $"InsertedAny={inserted} Remaining={remaining}.",
            this);
    }

    [ContextMenu("Celestial Observation / DEBUG Log Persistent Chart State")]
    private void DebugLogPersistentChartState()
    {
        if (GameState.I == null)
        {
            Debug.LogWarning("[CelestialObservationOverlayRunner] GameState.I is null.", this);
            return;
        }

        GameState.I.EnsureCelestialChartDefaults();
        CelestialChartStateSnapshot state = GameState.I.celestialCharts;
        Debug.Log(
            $"[CelestialObservationOverlayRunner] Persistent chart state: " +
            $"Fragments={state.fragments.Count}, SurveyRegions={state.surveyRegions.Count}, " +
            $"VerifiedConstellations={state.verifiedConstellationIds.Count}.",
            this);
    }

    [ContextMenu("Celestial Observation / DEBUG Log Constellation Truth Summary")]
    private void DebugLogConstellationTruthSummary()
    {
        AutoWire();
        if (fieldSource == null || !fieldSource.EnsureField() || fieldSource.Field == null)
        {
            Debug.LogWarning("[CelestialObservationOverlayRunner] No celestial field available for constellation summary.", this);
            return;
        }

        CelestialConstellationCatalog catalog = fieldSource.Field.Constellations;
        int sampleCount = Mathf.Min(8, catalog.Count);
        var sample = new System.Text.StringBuilder();
        for (int i = 0; i < sampleCount; i++)
        {
            CelestialConstellation c = catalog.All[i];
            if (i > 0) sample.Append(" | ");
            sample.Append(c.TruthName).Append(" [").Append(c.MemberStarStableIds.Count).Append(" stars]");
        }

        Debug.Log(
            $"[CelestialObservationOverlayRunner] Constellation truth count={catalog.Count}. " +
            $"DEBUG ONLY; names remain player-hidden until future NPC verification. Samples: {sample}",
            this);
    }


    [ContextMenu("Celestial Observation / DEBUG Toggle Fragment Preview")]
    private void ToggleFragmentPreview()
    {
        if (_showFragmentPreview)
        {
            _showFragmentPreview = false;
            ReleasePreviewVisual();
            return;
        }

        if (!TryGetPersistentFragments(out CelestialChartStateSnapshot state) ||
            state.fragments == null ||
            state.fragments.Count == 0)
        {
            GameMessageService.PostInfo("No chart fragments have been recorded yet.");
            return;
        }

        _previewFragmentIndex = state.fragments.Count - 1;
        _showFragmentPreview = true;
        EnsurePreviewVisual(state);
    }

    private void DrawFragmentPreviewGUI()
    {
        if (!TryGetPersistentFragments(out CelestialChartStateSnapshot state) ||
            state.fragments == null ||
            state.fragments.Count == 0)
        {
            _showFragmentPreview = false;
            ReleasePreviewVisual();
            return;
        }

        _previewFragmentIndex = Mathf.Clamp(
            _previewFragmentIndex < 0 ? state.fragments.Count - 1 : _previewFragmentIndex,
            0,
            state.fragments.Count - 1);

        EnsurePreviewVisual(state);

        CelestialChartFragmentSnapshot fragment = state.fragments[_previewFragmentIndex];
        if (fragment == null || _previewFragmentVisual == null)
            return;

        float panelWidth = Mathf.Min(Screen.width - 40f, 860f);
        float panelHeight = Mathf.Min(Screen.height - 40f, 760f);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        GUI.Box(panel, GUIContent.none);

        Rect header = new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, 24f);
        GUI.Label(
            header,
            $"PHASE 5B CHART FRAGMENT PREVIEW  •  {_previewFragmentIndex + 1}/{state.fragments.Count}");

        Rect info = new Rect(panel.x + 16f, panel.y + 38f, panel.width - 32f, 38f);
        GUI.Label(
            info,
            $"Fragment {ShortId(fragment.fragmentId)}  |  Survey {fragment.surveySequence}  |  " +
            $"Marks {fragment.marks?.Count ?? 0}  |  F7 closes");

        Rect viewport = new Rect(
            panel.x + 24f,
            panel.y + 84f,
            panel.width - 48f,
            panel.height - 142f);

        Rect paperRect = FitRect(
            viewport,
            _previewFragmentVisual.PixelSize.x,
            _previewFragmentVisual.PixelSize.y);

        GUI.DrawTexture(
            paperRect,
            _previewFragmentVisual.PaperTexture,
            ScaleMode.StretchToFill,
            true);

        GUI.DrawTexture(
            paperRect,
            _previewFragmentVisual.InkTexture,
            ScaleMode.StretchToFill,
            true);

        Rect previous = new Rect(panel.x + 18f, panel.yMax - 46f, 110f, 28f);
        Rect next = new Rect(panel.xMax - 128f, panel.yMax - 46f, 110f, 28f);

        GUI.enabled = _previewFragmentIndex > 0;
        if (GUI.Button(previous, "PREVIOUS"))
        {
            _previewFragmentIndex--;
            ReleasePreviewVisual();
        }

        GUI.enabled = _previewFragmentIndex < state.fragments.Count - 1;
        if (GUI.Button(next, "NEXT"))
        {
            _previewFragmentIndex++;
            ReleasePreviewVisual();
        }

        GUI.enabled = true;
    }

    private void EnsurePreviewVisual(CelestialChartStateSnapshot state)
    {
        if (state == null ||
            state.fragments == null ||
            state.fragments.Count == 0)
        {
            ReleasePreviewVisual();
            return;
        }

        _previewFragmentIndex = Mathf.Clamp(
            _previewFragmentIndex < 0 ? state.fragments.Count - 1 : _previewFragmentIndex,
            0,
            state.fragments.Count - 1);

        CelestialChartFragmentSnapshot fragment = state.fragments[_previewFragmentIndex];
        string id = fragment != null ? fragment.fragmentId : null;

        if (_previewFragmentVisual != null &&
            _previewFragmentId == id)
        {
            return;
        }

        ReleasePreviewVisual();

        if (fragment == null)
            return;

        _previewFragmentVisual =
            CelestialChartFragmentVisualBuilder.Build(
                fragment,
                fragmentVisualSettings);

        _previewFragmentId = id;
    }

    private void ReleasePreviewVisual()
    {
        if (_previewFragmentVisual != null)
        {
            _previewFragmentVisual.Dispose();
            _previewFragmentVisual = null;
        }

        _previewFragmentId = null;
    }

    private bool TryGetPersistentFragments(out CelestialChartStateSnapshot state)
    {
        state = null;

        if (GameState.I == null)
            return false;

        GameState.I.EnsureCelestialChartDefaults();
        state = GameState.I.celestialCharts;
        return state != null;
    }

    private int ResolveFragmentIndex(string fragmentId)
    {
        if (string.IsNullOrWhiteSpace(fragmentId) ||
            !TryGetPersistentFragments(out CelestialChartStateSnapshot state) ||
            state.fragments == null)
        {
            return -1;
        }

        for (int i = 0; i < state.fragments.Count; i++)
        {
            CelestialChartFragmentSnapshot fragment = state.fragments[i];
            if (fragment != null && fragment.fragmentId == fragmentId)
                return i;
        }

        return -1;
    }

    private static Rect FitRect(Rect available, int textureWidth, int textureHeight)
    {
        float width = Mathf.Max(1f, textureWidth);
        float height = Mathf.Max(1f, textureHeight);
        float scale = Mathf.Min(available.width / width, available.height / height);

        float fittedWidth = width * scale;
        float fittedHeight = height * scale;

        return new Rect(
            available.center.x - fittedWidth * 0.5f,
            available.center.y - fittedHeight * 0.5f,
            fittedWidth,
            fittedHeight);
    }

    private static string ShortId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "NULL";

        return id.Length <= 8
            ? id
            : id.Substring(0, 8);
    }

    private GameObject ResolveUniqueDebugRequester()
    {
        PlayerInventory[] inventories = Object.FindObjectsByType<PlayerInventory>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (inventories == null || inventories.Length == 0)
        {
            Debug.LogError(
                "[CelestialObservationOverlayRunner] Debug F6 opener found no active PlayerInventory. " +
                "Future physical interaction should call OpenObservationFor(requester).",
                this);
            return null;
        }

        if (inventories.Length != 1)
        {
            Debug.LogError(
                $"[CelestialObservationOverlayRunner] Debug F6 opener found {inventories.Length} active PlayerInventory objects. " +
                "Refusing to guess which player is charting. Use OpenObservationFor(requester).",
                this);
            return null;
        }

        return inventories[0].gameObject;
    }

    private float ResolveStarVisibility()
    {
        if (skyVisualManager == null)
            AutoWireSkyVisual();

        if (skyVisualManager == null)
        {
            if (!_warnedMissingSkyVisualManager)
            {
                _warnedMissingSkyVisualManager = true;
                Debug.LogWarning(
                    "[CelestialObservationOverlayRunner] SkyVisualManager is unavailable; charting visibility defaults to 0 until it resolves.",
                    this);
            }

            return 0f;
        }

        _warnedMissingSkyVisualManager = false;
        return Mathf.Clamp01(skyVisualManager.StarVisibility01);
    }

    private TimeOfDayManager ResolveTimeManager()
    {
        if (ServiceRoot.Instance != null && ServiceRoot.Instance.Time is TimeOfDayManager rootTime)
            return rootTime;

        return FindAnyObjectByType<TimeOfDayManager>(FindObjectsInactive.Include);
    }

    private void AutoWire()
    {
        if (overlay == null)
            overlay = FindAnyObjectByType<MiniGameOverlayHost>(FindObjectsInactive.Include);

        if (fieldSource == null)
            fieldSource = FindAnyObjectByType<CelestialFieldSource>(FindObjectsInactive.Include);

        AutoWireSkyVisual();
    }

    private void AutoWireSkyVisual()
    {
        if (skyVisualManager == null)
            skyVisualManager = FindAnyObjectByType<SkyVisualManager>(FindObjectsInactive.Include);
    }
}
