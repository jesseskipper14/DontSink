using System.Drawing;
using UnityEngine;

public class FallingSquareSpawner : MonoBehaviour
{
    public FallingSquare fallingSquarePrefab;
    public WaveField wave;        // authoritative wave

    [Header("Spawn Settings")]
    public float defaultSize = 0.5f;
    public float defaultMass = 0.5f;

    [Header("Water Level")]
    public float waterSurfaceY = 0f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0f;

            // Only spawn if click is above water surface
            if (worldPos.y > waterSurfaceY)
            {
                SpawnSquare(worldPos);
            }
        }
    }

    void SpawnSquare(Vector3 position)
    {
        FallingSquare square = Instantiate(fallingSquarePrefab, position, Quaternion.identity);

        // Geometry
        square.width = defaultSize;
        square.height = defaultSize;
        square.volume = defaultSize * defaultSize;

        // Adjust the Transform scale so it matches the width/height
        square.transform.localScale = new Vector3(square.width, square.height, 1f);

        // Mass
        square.rb.mass = defaultMass;

        // Wave
        square.wave = wave;

        // Buoyancy system
        if (square.genericBuoyancy != null)
        {
            square.genericBuoyancy.wave = wave;
            square.genericBuoyancy.bodySource = square;
        }
    }

}
