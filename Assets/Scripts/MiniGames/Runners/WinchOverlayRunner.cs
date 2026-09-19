using UnityEngine;
using MiniGames;

/// <summary>
/// Opens the dedicated WinchCartridge and routes its control intents to the
/// currently authoritative WinchModule.
///
/// Today this routing is local/synchronous. A future multiplayer transport can
/// replace the local hop with client -> host intent delivery while preserving
/// the same cartridge and WinchModule authority boundary.
/// </summary>
[DisallowMultipleComponent]
public sealed class WinchOverlayRunner : MonoBehaviour
{
    [SerializeField] private MiniGameOverlayHost overlay;

    private bool _subscribed;

    private string _activeTargetId;
    private Hardpoint _activeHardpoint;
    private WinchModule _activeWinch;
    private WinchCartridge _activeCartridge;

    private void Reset()
    {
        overlay =
            FindAnyObjectByType<MiniGameOverlayHost>();
    }

    private void Awake()
    {
        ResolveOverlay();
        Subscribe();
    }

    private void OnEnable()
    {
        ResolveOverlay();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearActiveSession();
    }

    public bool OpenForHardpoint(
        Hardpoint hardpoint)
    {
        if (hardpoint == null)
        {
            Debug.LogWarning(
                "[WinchOverlayRunner] OpenForHardpoint called with null hardpoint.");

            return false;
        }

        if (!hardpoint.HasInstalledModule ||
            hardpoint.InstalledModule == null)
        {
            Debug.LogWarning(
                "[WinchOverlayRunner] Winch hardpoint has no installed module.",
                hardpoint);

            return false;
        }

        WinchModule winch =
            hardpoint.InstalledModule
                .GetComponent<WinchModule>();

        if (winch == null)
        {
            Debug.LogWarning(
                $"[WinchOverlayRunner] Installed module on '{hardpoint.HardpointId}' is not a WinchModule.",
                hardpoint);

            return false;
        }

        if (!ResolveOverlay())
            return false;

        Subscribe();

        WinchReadoutSource readout =
            new WinchReadoutSource(
                hardpoint,
                winch);

        string targetId =
            !string.IsNullOrWhiteSpace(
                hardpoint.HardpointId)
                ? $"winch_console:{hardpoint.HardpointId}"
                : $"winch_console:{hardpoint.GetInstanceID()}";

        WinchCartridge cartridge =
            new WinchCartridge(
                hardpoint,
                winch,
                readout);

        _activeHardpoint =
            hardpoint;

        _activeWinch =
            winch;

        _activeCartridge =
            cartridge;

        _activeTargetId =
            targetId;

        MiniGameContext ctx =
            new MiniGameContext
            {
                targetId =
                    targetId,

                difficulty =
                    1f,

                pressure =
                    0f,

                seed =
                    0
            };

        overlay.Open(
            cartridge,
            ctx);

        return true;
    }

    private void Subscribe()
    {
        if (_subscribed ||
            overlay == null)
        {
            return;
        }

        overlay.EffectEmitted +=
            OnMiniGameEffect;

        _subscribed =
            true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed ||
            overlay == null)
        {
            _subscribed =
                false;

            return;
        }

        overlay.EffectEmitted -=
            OnMiniGameEffect;

        _subscribed =
            false;
    }

    private void OnMiniGameEffect(
        MiniGameEffect effect)
    {
        if (effect.kind !=
            MiniGameEffectKind.Control)
        {
            return;
        }

        if (effect.system !=
            WinchControlIntentPayload.EffectSystem)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                _activeTargetId) ||
            effect.targetId !=
                _activeTargetId)
        {
            return;
        }

        if (_activeHardpoint == null ||
            _activeWinch == null)
        {
            NotifyIntentResult(
                WinchControlIntent.Stop,
                false,
                "WINCH CONTROL TARGET IS NO LONGER AVAILABLE");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                effect.payloadJson))
        {
            NotifyIntentResult(
                WinchControlIntent.Stop,
                false,
                "MISSING WINCH CONTROL INTENT");

            return;
        }

        WinchControlIntentPayload payload =
            JsonUtility.FromJson<WinchControlIntentPayload>(
                effect.payloadJson);

        if (payload == null)
        {
            NotifyIntentResult(
                WinchControlIntent.Stop,
                false,
                "INVALID WINCH CONTROL INTENT");

            return;
        }

        if (payload.version !=
            WinchControlIntentPayload.CurrentVersion)
        {
            NotifyIntentResult(
                payload.intent,
                false,
                $"UNSUPPORTED WINCH INTENT VERSION {payload.version}");

            return;
        }

        bool ok =
            _activeWinch.TryApplyControlIntent(
                payload.intent,
                out string message);

        NotifyIntentResult(
            payload.intent,
            ok,
            message);
    }

    private void NotifyIntentResult(
        WinchControlIntent intent,
        bool success,
        string message)
    {
        _activeCartridge?.NotifyControlIntentApplied(
            intent,
            success,
            message);
    }

    private void ClearActiveSession()
    {
        _activeTargetId =
            null;

        _activeHardpoint =
            null;

        _activeWinch =
            null;

        _activeCartridge =
            null;
    }

    private bool ResolveOverlay()
    {
        if (overlay == null)
        {
            overlay =
                FindAnyObjectByType<MiniGameOverlayHost>();
        }

        if (overlay != null)
            return true;

        Debug.LogError(
            "[WinchOverlayRunner] Missing MiniGameOverlayHost.",
            this);

        return false;
    }
}
