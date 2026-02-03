using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class WindShaderRenderer : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private MonoBehaviour windSource;

    [Header("Material")]
    [SerializeField] private Material windMaterial;

    [Header("Oscillation")]
    //[SerializeField] private float oscillationPeriod = 2f; // total "cycle time" for active part
    //[SerializeField] private float zeroHoldMultiplier = 3f; // hold at zero strength ~3x longer

    private IWindService wind;

    private static readonly int WindStrengthID = Shader.PropertyToID("_WindStrength");
    private static readonly int WindDirID = Shader.PropertyToID("_WindDir");
    private static readonly int LineSpeedID = Shader.PropertyToID("_LineSpeed");
    private static readonly int OscPhaseID = Shader.PropertyToID("_OscPhase");

    private float oscillationTimer;

    private void Awake()
    {
        wind = windSource as IWindService;
        if (wind == null)
        {
            Debug.LogError($"{name}: windSource does not implement IWindService");
            enabled = false;
            return;
        }

        if (windMaterial == null)
        {
            var rend = GetComponent<MeshRenderer>();
            if (rend != null)
                windMaterial = rend.sharedMaterial;
            if (windMaterial == null)
            {
                Debug.LogError($"{name}: No material assigned");
                enabled = false;
                return;
            }
        }

        // Subscribe to wind changes
        wind.OnWindChanged += OnWindChanged;

        // Initialize
        UpdateWindProperties(wind.WindStrength01);
    }

    private void OnDestroy()
    {
        if (wind != null)
            wind.OnWindChanged -= OnWindChanged;
    }

    private void Update()
    {
        if (windMaterial == null || wind == null)
            return;

        oscillationTimer += Time.deltaTime;

        // ----------------------------
        // 1. Oscillating strength
        // ----------------------------
        //float fullPeriod = oscillationPeriod;
        //float zeroHold = zeroHoldMultiplier / (zeroHoldMultiplier + 1f); // fraction of period at zero
        //float activeFraction = 1f - zeroHold;

        //float tNorm = (oscillationTimer % fullPeriod) / fullPeriod; // 0..1
        //float osc;

        //if (tNorm < zeroHold)
        //{
        //    // Hold near zero
        //    osc = 0f;
        //}
        //else
        //{
        //    // Active sine portion
        //    float activeT = (tNorm - zeroHold) / activeFraction; // remap 0..1
        //    osc = Mathf.Sin(activeT * Mathf.PI); // 0..1 curve, smooth rise/fall
        //}

        //float currentStrength = osc * wind.WindStrength01;
        float currentStrength = wind.WindStrength01;

        // ----------------------------
        // 2. Direction toggle
        // ----------------------------
        float dirX = wind.WindStrength01 >= 0 ? -1f : 1f;
        Vector2 dir = new Vector2(dirX, 0f);

        // ----------------------------
        // 3. Speed scales with magnitude
        // ----------------------------
        float speed = Mathf.Abs(wind.WindStrength01);

        // ----------------------------
        // 4. Apply to shader
        // ----------------------------
        windMaterial.SetFloat(WindStrengthID, currentStrength);
        windMaterial.SetVector(WindDirID, dir);
        windMaterial.SetFloat(LineSpeedID, speed);
        windMaterial.SetFloat("_TimeOffset", Time.time);
    }

    private void OnWindChanged(float windStrength01)
    {
        UpdateWindProperties(windStrength01);
    }

    private void UpdateWindProperties(float windStrength01)
    {
        if (windMaterial == null)
            return;

        // Line speed directly proportional to wind strength
        windMaterial.SetFloat(LineSpeedID, Mathf.Abs(windStrength01));

        // Direction toggle: x=1 or -1
        float dirX = windStrength01 >= 0f ? 1f : -1f;
        windMaterial.SetVector(WindDirID, new Vector2(dirX, 0f));

        // Reset oscillation timer to start from zero
        oscillationTimer = 0f;
    }
}
