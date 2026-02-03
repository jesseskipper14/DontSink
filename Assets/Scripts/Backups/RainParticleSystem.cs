using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class RainParticleSystem : MonoBehaviour
{
    public ParticleSystem PS { get; private set; }

    private void Awake()
    {
        // Get or create
        PS = GetComponent<ParticleSystem>();
        if (PS == null)
            PS = gameObject.AddComponent<ParticleSystem>();

        SetupParticleSystem();
    }

    private void SetupParticleSystem()
    {
        // --- Main module ---
        var main = PS.main;
        main.loop = true;
        main.startLifetime = 1f;
        main.startSpeed = 10f;
        main.startSize = 0.05f;
        main.startColor = new Color(0.6f, 0.6f, 0.8f, 0.7f); // grey-blue
        main.maxParticles = 1000;

        // --- Shape ---
        var shape = PS.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(50f, 1f, 1f);
        shape.position = new Vector3(0f, 10f, 0f);

        // --- Emission ---
        var emission = PS.emission;
        emission.enabled = true;
        emission.rateOverTime = 200f; // start value, can adjust dynamically

        // --- Renderer ---
        var renderer = PS.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Particles/Default"));

        // --- Velocity over lifetime ---
        var velocity = PS.velocityOverLifetime;
        velocity.enabled = true;

        // Must use MinMaxCurve, cannot assign struct
        velocity.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
        velocity.y = new ParticleSystem.MinMaxCurve(-10f); // falling
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        // Force particle system to start
        PS.Clear();
        PS.Play();
    }
}