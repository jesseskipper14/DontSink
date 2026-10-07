using UnityEngine;

/// <summary>Opaque plain ocean where surface is charted but bathymetry is not; no second shroud.</summary>
public sealed class WorldMapBathymetryConcealment
{
    private Texture2D texture;
    private WorldMapTopographyField lastField;
    private WorldMapKnowledgeState lastState;
    private int revision = -1;
    private float sea;
    private bool showDepths;
    public Texture2D Get(WorldMapTopographyField field, float seaLevel, WorldMapKnowledgeState state, bool showSeaFloor = true)
    {
        if (field == null || !field.IsValid || state == null || !state.IsValid) return null;
        if (texture != null && lastField == field && lastState == state && revision == state.Revision && sea == seaLevel && showDepths == showSeaFloor) return texture;
        if (texture != null) Object.Destroy(texture);
        const int size = 512;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Uncharted bathymetry concealment", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Vector2 world = new Vector2(field.WorldBounds.xMin + (x + .5f) / size * field.WorldBounds.width, field.WorldBounds.yMin + (y + .5f) / size * field.WorldBounds.height);
            if (field.Sample01World(world) < seaLevel && (!showSeaFloor || !state.CanDisplayBathymetry(world))) pixels[y * size + x] = new Color(.055f, .23f, .32f, 1);
        }
        texture.SetPixels32(pixels); texture.Apply(false, true);
        lastField = field; lastState = state; revision = state.Revision; sea = seaLevel; showDepths = showSeaFloor;
        return texture;
    }
    public void Clear() { if (texture != null) Object.Destroy(texture); texture = null; lastState = null; lastField = null; }
}
