using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ProceduralCloud : MonoBehaviour
{
    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;

    public float speed = 1f;          // Horizontal movement speed
    public float parallax = 1f;       // Multiplier for speed/depth
    private float scale = 1f;         // Local scale
    private float density = 0.5f;     // Shader density
    private float cloudSpeed = 0.02f; // Shader animation speed

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);

        // Randomize shader parameters for uniqueness
        scale = Random.Range(0.8f, 2f);
        speed = Random.Range(-0.5f, -10f);
        density = Random.Range(0.3f, 0.7f);
        cloudSpeed = Random.Range(0.01f, 0.05f);
        parallax = Random.Range(0.5f, 1.5f);

        mpb.SetFloat("_CloudScale", scale);
        mpb.SetFloat("_CloudDensity", density);
        mpb.SetFloat("_CloudSpeed", cloudSpeed);

        sr.SetPropertyBlock(mpb);
    }

    public void UpdateCloud(float sunBrightness)
    {
        // Move cloud horizontally with parallax
        transform.position += Vector3.left * speed * parallax * Time.deltaTime;

        // Update alpha based on sun brightness
        sr.GetPropertyBlock(mpb);
        Color col = sr.color;
        col.a = Mathf.Lerp(0.2f, 0.8f, sunBrightness);
        mpb.SetColor("_CloudColor", col);
        sr.SetPropertyBlock(mpb);

        // Self-destruct if offscreen
        if (transform.position.x > 15f) // match cloudDestroyX in manager
        {
            Destroy(gameObject);
        }
    }
}
