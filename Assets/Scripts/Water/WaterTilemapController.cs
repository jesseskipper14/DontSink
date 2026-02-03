using UnityEngine;

public class WaterTilemapController : MonoBehaviour
{
    public Transform boat;
    public Material waterMaterial;
    public float tilingFactor = 1f;

    void Update()
    {
        if (boat == null || waterMaterial == null) return;

        // Convert boat position to Vector2 for the shader offset
        Vector2 offset = new Vector2(boat.position.x, boat.position.y) / tilingFactor;

        // Set shader property
        waterMaterial.SetVector("_WorldOffset", new Vector4(offset.x, offset.y, 0f, 0f));
    }
}
