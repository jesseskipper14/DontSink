using UnityEngine;

/// <summary>Saved generation inputs for a decorative sky, independent of gameplay world/knowledge.</summary>
[CreateAssetMenu(menuName = "WorldMap/Celestial/Fixed Sky Profile", fileName = "MainMenuSky")]
public sealed class CelestialFixedSkyProfile : ScriptableObject
{
    [Tooltip("Used only when generating a new saved sky. Later edits do not change an existing sky.")]
    public CelestialGenerationSettings generationSettings;
    public Rect worldBounds = new Rect(-500f, -250f, 1000f, 500f);
    public Vector2 observerPosition;

    [SerializeField] private int savedSeed;
    [SerializeField] private int savedGeneratorVersion;
    [SerializeField, HideInInspector] private Rect savedBounds;
    [SerializeField, HideInInspector] private Vector2 savedObserver;
    [SerializeField, HideInInspector] private CelestialGenerationConfig savedConfig;
    [SerializeField, HideInInspector] private CelestialConstellationGenerationConfig savedConstellations;

    public Vector2 ObserverPosition => savedObserver;
    public bool IsGenerated => savedGeneratorVersion == CelestialFieldGenerator.CurrentGeneratorVersion &&
        savedConfig != null && WorldMapCoordinateSpace.IsValidBounds(savedBounds);
    public int Seed => savedSeed;
    public Rect Bounds => savedBounds;
    public string ConfigHash => savedConfig != null ? CelestialGenerationFingerprint.Build(savedConfig) : null;

    public CelestialField CreateField()
    {
        if (!IsGenerated) return null;
        var field = CelestialFieldGenerator.Create(savedSeed, savedBounds, savedConfig);
        if (field != null) field.ConfigureConstellations(savedConstellations);
        return field;
    }

    [ContextMenu("Generate And Save New Random Sky")]
    private void GenerateAndSave()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("Generate the fixed sky outside Play Mode so the asset can be saved.", this);
            return;
        }
        if (generationSettings == null || !WorldMapCoordinateSpace.IsValidBounds(worldBounds) ||
            !WorldTopology.IsFinite(observerPosition.x) || !WorldTopology.IsFinite(observerPosition.y) ||
            !worldBounds.Contains(observerPosition))
        {
            Debug.LogError("Assign generation settings, valid bounds, and an observer inside those bounds first.", this);
            return;
        }
        UnityEditor.Undo.RecordObject(this, "Generate fixed celestial sky");
        savedSeed = System.BitConverter.ToInt32(System.Guid.NewGuid().ToByteArray(), 0);
        savedGeneratorVersion = CelestialFieldGenerator.CurrentGeneratorVersion;
        savedBounds = worldBounds;
        savedObserver = observerPosition;
        savedConfig = generationSettings.CreateConfigSnapshot();
        savedConstellations = generationSettings.CreateConstellationConfigSnapshot();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        Debug.Log($"Saved fixed celestial sky: seed {savedSeed}. This sky will be reused until you explicitly generate another.", this);
#endif
    }
}
