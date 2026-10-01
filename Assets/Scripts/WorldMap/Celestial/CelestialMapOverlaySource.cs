using UnityEngine;

/// <summary>
/// Builds and owns the runtime map-rendering cache for a CelestialField.
/// The cached textures are disposable presentation data, never authoritative state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CelestialFieldSource))]
public sealed class CelestialMapOverlaySource : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CelestialFieldSource fieldSource;
    [SerializeField] private CelestialMapOverlaySettings overlaySettings;

    [Header("Startup")]
    [SerializeField] private bool buildOnAwake = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private CelestialMapOverlaySettings _runtimeDefaultSettings;
    private CelestialMapTextureSet _textureSet;
    private string _builtFieldKey;
    private string _builtVisualKey;

    public CelestialFieldSource FieldSource => fieldSource;
    public CelestialField Field => fieldSource != null ? fieldSource.Field : null;
    public CelestialMapTextureSet TextureSet => _textureSet;
    public bool HasOverlay => _textureSet != null && _textureSet.IsValid;

    public Rect WorldBounds => HasOverlay ? _textureSet.WorldBounds : default;
    public Texture2D AmbientTexture => HasOverlay ? _textureSet.AmbientStars : null;
    public Texture2D LandmarkTexture => HasOverlay ? _textureSet.LandmarkStars : null;
    public Texture2D NebulaTexture => HasOverlay ? _textureSet.Nebulae : null;
    public Texture2D DeepSkyTexture => HasOverlay ? _textureSet.DeepSkyObjects : null;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();

        if (buildOnAwake)
            EnsureBuilt();
    }

    public bool EnsureBuilt()
    {
        AutoWire();

        if (fieldSource == null || !fieldSource.EnsureField())
            return false;

        CelestialField field = fieldSource.Field;
        CelestialMapOverlaySettings resolvedSettings = ResolveSettings();

        if (field == null || !field.IsValid || resolvedSettings == null)
            return false;

        string fieldKey = BuildFieldKey(field);
        string visualKey = JsonUtility.ToJson(resolvedSettings);

        if (HasOverlay &&
            string.Equals(_builtFieldKey, fieldKey, System.StringComparison.Ordinal) &&
            string.Equals(_builtVisualKey, visualKey, System.StringComparison.Ordinal))
        {
            return true;
        }

        return BuildNow(field, resolvedSettings, fieldKey, visualKey);
    }

    [ContextMenu("Rebuild Celestial Map Overlay")]
    public void Rebuild()
    {
        AutoWire();

        if (fieldSource != null)
            fieldSource.RebuildField();

        ReleaseTextures();
        EnsureBuilt();
    }

    [ContextMenu("Log Celestial Map Overlay")]
    private void LogSummary()
    {
        if (!EnsureBuilt())
        {
            Debug.LogWarning("[CelestialMapOverlaySource] No valid overlay to summarize.", this);
            return;
        }

        Debug.Log(
            $"[CelestialMapOverlaySource] Bounds={_textureSet.WorldBounds}, " +
            $"Texture={_textureSet.Width}x{_textureSet.Height}, Total={_textureSet.TotalCount}, " +
            $"Ambient={_textureSet.AmbientCount}, Landmarks={_textureSet.LandmarkCount}, " +
            $"Nebulae={_textureSet.NebulaCount}, DeepSky={_textureSet.DeepSkyCount}",
            this);
    }

    private bool BuildNow(
        CelestialField field,
        CelestialMapOverlaySettings settings,
        string fieldKey,
        string visualKey)
    {
        CelestialMapTextureSet next = CelestialMapTextureBuilder.Build(field, settings);
        if (next == null || !next.IsValid)
        {
            Debug.LogError("[CelestialMapOverlaySource] Failed to build celestial map textures.", this);
            return false;
        }

        ReleaseTextures();

        _textureSet = next;
        _builtFieldKey = fieldKey;
        _builtVisualKey = visualKey;

        if (verboseLogging)
        {
            Debug.Log(
                $"[CelestialMapOverlaySource] Built overlay. " +
                $"Seed={field.WorldSeed}, Bounds={next.WorldBounds}, " +
                $"Texture={next.Width}x{next.Height}, Total={next.TotalCount}, " +
                $"Ambient={next.AmbientCount}, Landmarks={next.LandmarkCount}, " +
                $"Nebulae={next.NebulaCount}, DeepSky={next.DeepSkyCount}",
                this);
        }

        return true;
    }

    private CelestialMapOverlaySettings ResolveSettings()
    {
        if (overlaySettings != null)
            return overlaySettings;

        if (_runtimeDefaultSettings == null)
        {
            _runtimeDefaultSettings = ScriptableObject.CreateInstance<CelestialMapOverlaySettings>();
            _runtimeDefaultSettings.name = "Runtime Default Celestial Map Overlay Settings";
            _runtimeDefaultSettings.hideFlags = HideFlags.DontSave;
        }

        return _runtimeDefaultSettings;
    }

    private void AutoWire()
    {
        if (fieldSource == null)
            fieldSource = GetComponent<CelestialFieldSource>();

        if (fieldSource == null)
        {
            fieldSource = FindAnyObjectByType<CelestialFieldSource>(
                FindObjectsInactive.Include);
        }
    }

    private static string BuildFieldKey(CelestialField field)
    {
        if (field == null || field.Identity == null)
            return string.Empty;

        return
            field.Identity.generatorVersion + "|" +
            field.WorldSeed + "|" +
            field.Identity.configHash + "|" +
            field.WorldBounds.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            field.WorldBounds.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            field.WorldBounds.width.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            field.WorldBounds.height.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ReleaseTextures()
    {
        if (_textureSet != null)
        {
            DestroyTexture(_textureSet.AmbientStars);
            DestroyTexture(_textureSet.LandmarkStars);
            DestroyTexture(_textureSet.Nebulae);
            DestroyTexture(_textureSet.DeepSkyObjects);
        }

        _textureSet = null;
        _builtFieldKey = null;
        _builtVisualKey = null;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(texture);
        else
            Object.DestroyImmediate(texture);
    }

    private void OnDestroy()
    {
        ReleaseTextures();

        if (_runtimeDefaultSettings != null)
        {
            if (Application.isPlaying)
                Destroy(_runtimeDefaultSettings);
            else
                DestroyImmediate(_runtimeDefaultSettings);
        }
    }
}
