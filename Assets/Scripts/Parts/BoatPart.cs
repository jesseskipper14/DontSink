using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BoatPart : MonoBehaviour
{
    [Header("Material")]
    public HullMaterial material;

    protected SpriteRenderer sr;
    protected BoxCollider2D col;

    protected virtual void Awake()
    {
        // Setup SpriteRenderer
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();

        // --- Assign default material if none provided ---
        if (material == null)
        {
            material = Resources.Load<HullMaterial>("Materials/Wood_Hull"); // Make sure this asset exists in Resources
        }

        // --- Assign sprite from material, or default if missing ---
        if (sr.sprite == null)
        {
            if (material != null && material.sprite != null)
            {
                sr.sprite = material.sprite;
            }
            else
            {
                // Load a generic hull block sprite from Resources
                sr.sprite = Resources.Load<Sprite>("Sprites/HullBlock_Sprite")
                            ?? CreateDefaultSprite(material != null ? material.color : Color.gray);
            }
        }

        // Apply material visuals
        if (material != null)
        {
            sr.color = material.color;
            sr.sortingLayerName = material.sortingLayer;
            sr.sortingOrder = material.sortingOrder;
        }

        // Setup BoxCollider2D
        col.isTrigger = false;
    }

    protected virtual void Start()
    {
        // Match collider to sprite bounds
        if (sr != null && sr.sprite != null && col != null)
            col.size = sr.sprite.bounds.size;
    }

    /// <summary>
    /// Creates a simple 1x1 colored square sprite
    /// </summary>
    public static Sprite CreateDefaultSprite(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
    }
}
