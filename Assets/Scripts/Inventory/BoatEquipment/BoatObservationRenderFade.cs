using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporary camera-render alpha changes, restored before any other camera renders.</summary>
internal sealed class BoatObservationRenderFade
{
    private struct SpriteState { public SpriteRenderer renderer; public Color color; }
    private struct LineState { public LineRenderer renderer; public Gradient gradient; }
    private struct MeshState { public Renderer renderer; public int slot; public MaterialPropertyBlock block; }
    private struct CanvasState { public CanvasRenderer renderer; public float alpha; }
    private readonly List<SpriteState> sprites = new List<SpriteState>();
    private readonly List<LineState> lines = new List<LineState>();
    private readonly List<MeshState> meshes = new List<MeshState>();
    private readonly List<CanvasState> canvases = new List<CanvasState>();
    private static readonly int[] ColorProperties = {
        Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_Color"),
        Shader.PropertyToID("_FaceColor"), Shader.PropertyToID("_OutlineColor")
    };

    public void Apply(Renderer renderer, float opacity)
    {
        if (renderer is SpriteRenderer sprite)
        {
            Color original = sprite.color;
            sprites.Add(new SpriteState { renderer = sprite, color = original });
            original.a *= opacity;
            sprite.color = original;
            return;
        }
        if (renderer is LineRenderer line)
        {
            Gradient original = line.colorGradient;
            lines.Add(new LineState { renderer = line, gradient = original });
            GradientAlphaKey[] alpha = original.alphaKeys;
            for (int i = 0; i < alpha.Length; i++) alpha[i].alpha *= opacity;
            var faded = new Gradient { mode = original.mode };
            faded.SetKeys(original.colorKeys, alpha);
            line.colorGradient = faded;
            return;
        }
        // Transparent mesh/TMP materials retain their existing property overrides.
        Material[] materials = renderer.sharedMaterials;
        for (int slot = 0; slot < materials.Length; slot++)
        {
            Material material = materials[slot];
            if (material == null) continue;
            var original = new MaterialPropertyBlock();
            var faded = new MaterialPropertyBlock();
            var global = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(original, slot);
            renderer.GetPropertyBlock(global);
            // Per-material overrides take precedence over renderer-wide overrides.
            if (original.isEmpty) renderer.GetPropertyBlock(faded);
            else renderer.GetPropertyBlock(faded, slot);
            bool changed = false;
            foreach (int property in ColorProperties)
            {
                if (!material.HasProperty(property)) continue;
                Color color = !original.isEmpty && original.HasProperty(property) ? original.GetColor(property) :
                    original.isEmpty && global.HasProperty(property) ? global.GetColor(property) : material.GetColor(property);
                color.a *= opacity;
                faded.SetColor(property, color);
                changed = true;
            }
            if (!changed) continue;
            meshes.Add(new MeshState { renderer = renderer, slot = slot, block = original });
            renderer.SetPropertyBlock(faded, slot);
        }
    }

    public void Apply(CanvasRenderer renderer, float opacity)
    {
        float alpha = renderer.GetAlpha();
        canvases.Add(new CanvasState { renderer = renderer, alpha = alpha });
        renderer.SetAlpha(alpha * opacity);
    }

    public void Restore()
    {
        foreach (var state in sprites) if (state.renderer != null) state.renderer.color = state.color;
        foreach (var state in lines) if (state.renderer != null) state.renderer.colorGradient = state.gradient;
        foreach (var state in meshes) if (state.renderer != null)
            state.renderer.SetPropertyBlock(state.block.isEmpty ? null : state.block, state.slot);
        foreach (var state in canvases) if (state.renderer != null) state.renderer.SetAlpha(state.alpha);
        sprites.Clear(); lines.Clear(); meshes.Clear(); canvases.Clear();
    }
}
