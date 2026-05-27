using MiniGames;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldGenOverlayRunner : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private MiniGameOverlayHost overlay;

    [Header("WorldGen")]
    [SerializeField] private WorldGenerationPipelineRunner pipelineRunner;

    [Header("Debug Open")]
    [SerializeField] private bool debugOpenWithKey = true;
    [SerializeField] private KeyCode debugOpenKey = KeyCode.G;

    public bool IsWorldGenOpen =>
        overlay != null &&
        overlay.IsOpen &&
        overlay.ActiveCartridge is WorldGenCartridge;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
    }

    private void Update()
    {
        if (!debugOpenWithKey)
            return;

        if (Input.GetKeyDown(debugOpenKey))
            ToggleWorldGen();
    }

    public bool ToggleWorldGen()
    {
        AutoWire();

        if (IsWorldGenOpen)
        {
            overlay.Close();
            return true;
        }

        return OpenWorldGen();
    }

    public bool OpenWorldGen()
    {
        AutoWire();

        if (overlay == null)
        {
            Debug.LogError("[WorldGenOverlayRunner] Missing MiniGameOverlayHost.", this);
            return false;
        }

        if (pipelineRunner == null)
        {
            Debug.LogError("[WorldGenOverlayRunner] Missing WorldGenerationPipelineRunner.", this);
            return false;
        }

        var cart = new WorldGenCartridge(pipelineRunner);

        var ctx = new MiniGameContext
        {
            targetId = "world_generation_lab",
            difficulty = 1f,
            pressure = 0f,
            seed = 0
        };

        overlay.Open(cart, ctx);
        return true;
    }

    public bool CloseWorldGen()
    {
        AutoWire();

        if (!IsWorldGenOpen)
            return false;

        overlay.Close();
        return true;
    }

    private void AutoWire()
    {
        if (overlay == null)
            overlay = FindAnyObjectByType<MiniGameOverlayHost>(FindObjectsInactive.Include);

        if (pipelineRunner == null)
            pipelineRunner = FindAnyObjectByType<WorldGenerationPipelineRunner>(FindObjectsInactive.Include);
    }
}
