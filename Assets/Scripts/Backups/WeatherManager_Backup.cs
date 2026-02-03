//using UnityEngine;

//public class WeatherManager_OLD : MonoBehaviour
//{
//    public static WeatherManager Instance { get; private set; }

//    [Header("Rain Settings")]
//    [Range(0f, 1f)]
//    public float rainIntensity = 0f;
//    public float rainRate = 0.03f;

//    [Header("Wind Settings")]
//    [Range(0f, 1f)]
//    public float wind = 0.5f; // 0 = strong left, 1 = strong right
//    public float maxWindSpeed = 5f; // how fast particles drift horizontally

//    public ParticleSystem rainPS;
//    private ParticleSystem.EmissionModule emission;

//   public Boat boat;

//    private void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        Instance = this;
//        DontDestroyOnLoad(gameObject);

//        // Grab the RainParticleSystem component on this GameObject
//        var rainPSComp = GetComponent<RainParticleSystem>();
//        if (rainPSComp != null)
//        {
//            rainPS = rainPSComp.PS; // assign the actual ParticleSystem
//        }
//    }

//    private void Update()
//    {
//        if (rainPS != null && boat != null)
//        {
//            var e = rainPS.emission;
//            e.rateOverTime = Mathf.Lerp(0f, 2000f, rainIntensity);
//            var f = rainPS.velocityOverLifetime;
//            f.x = Mathf.Lerp(-maxWindSpeed - (boat.velocity.x), maxWindSpeed - (boat.velocity.x), wind);
//        }
//    }

//    public void SetRain(float intensity)
//    {
//        rainIntensity = Mathf.Clamp01(intensity);
//    }

//    public float GetRainDelta()
//    {
//        return rainRate * rainIntensity;
//    }
//}