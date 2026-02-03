/*
 * ExternalBackground.cs – Summary

Purpose:
Manages a repeating checkerboard background that follows the boat, giving the illusion of continuous scenery.

Components & Setup:

Requires MeshRenderer and MeshFilter.

Optional custom checkerboard texture (checkerTexture).

Configurable size (sizeX, sizeY) and repetition (repeatX, repeatY) to control tiling.

resetThreshold determines how far the background can drift from the boat before snapping back.

Core Functionality:

Quad Setup

SetupQuad() creates a simple quad mesh aligned in world-space.

Quad vertices are set based on sizeX / sizeY and positioned slightly in the Z-axis (10f) to render behind the boat/water.

UVs are scaled to repeat the checkerboard texture according to repeatX / repeatY.

Triangles are defined for a single quad.

Generates or assigns a material (Unlit/Texture) using the checkerboard texture.

Checkerboard Generation

GenerateCheckerTexture() creates a 2x2 repeating texture programmatically if none is provided.

Colors alternate between light grey (Color.grey) and dark grey (0.2f,0.2f,0.2f).

Texture is set to repeat and point filtering to maintain crisp edges.

Background Following Boat

In FixedUpdate(), the background’s position is compared to the boat’s position.

If the offset magnitude exceeds resetThreshold, the background snaps back to the boat position.

Ensures continuous background appearance without drifting infinitely.

Notes / Potential Extensions:

Could add parallax scaling for depth perception.

Could dynamically update UV offset to animate movement instead of snapping.

Works with unlit shader, so lighting won’t affect the checkerboard appearance.

Can handle any texture, not just checkerboard, for different stylings.
 */

using UnityEngine;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class ExternalBackground : MonoBehaviour
{
    // ========================
    // References
    // ========================

    public Boat boat;

    // ========================
    // Settings
    // ========================

    [Header("Background Reset")]
    public float resetThreshold = 50f; // Snap back if drifting too far

    [Header("Checkerboard Settings")]
    public int repeatX = 50;
    public int repeatY = 50;
    public float sizeX = 100f;
    public float sizeY = 100f;
    public Texture2D checkerTexture; // Optional: generated if null

    // ========================
    // Cached Components
    // ========================

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    // ========================
    // Unity Lifecycle
    // ========================

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        if (checkerTexture == null)
            checkerTexture = GenerateCheckerTexture();

        SetupQuad();
    }

    private void FixedUpdate()
    {
        if (boat == null)
            return;

        Vector2 boatPos = boat.transform.position;
        Vector2 bgPos = transform.position;

        Vector2 offset = bgPos - boatPos;

        if (offset.magnitude > resetThreshold)
        {
            transform.position = new Vector3(
                boatPos.x,
                boatPos.y,
                transform.position.z // preserve depth
            );
        }
    }


    // ========================
    // Setup & Helpers
    // ========================

    private void SetupQuad()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices =
        {
            new Vector3(-sizeX / 2f, -sizeY / 2f, 10f),
            new Vector3( sizeX / 2f, -sizeY / 2f, 10f),
            new Vector3(-sizeX / 2f,  sizeY / 2f, 10f),
            new Vector3( sizeX / 2f,  sizeY / 2f, 10f)
        };

        Vector2[] uv =
        {
            new Vector2(0, 0),
            new Vector2(repeatX, 0),
            new Vector2(0, repeatY),
            new Vector2(repeatX, repeatY)
        };

        int[] triangles = { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;

        Material material = new Material(Shader.Find("Unlit/Texture"));
        material.mainTexture = checkerTexture;
        meshRenderer.material = material;
    }

    private Texture2D GenerateCheckerTexture()
    {
        Texture2D tex = new Texture2D(2, 2)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        Color light = Color.grey;
        Color dark = new Color(0.2f, 0.2f, 0.2f);

        tex.SetPixel(0, 0, light);
        tex.SetPixel(1, 0, dark);
        tex.SetPixel(0, 1, dark);
        tex.SetPixel(1, 1, light);

        tex.Apply();
        return tex;
    }
}
