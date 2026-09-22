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
    private GameObject _activeRequester;

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
        // Compatibility overload for non-player callers. Winch controls may still
        // be observed/routed, but player inventory transfers require an explicit
        // requester and will be rejected.
        return OpenForHardpoint(
            hardpoint,
            null);
    }

    public bool OpenForHardpoint(
        Hardpoint hardpoint,
        GameObject requester)
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

        if (string.IsNullOrWhiteSpace(
                hardpoint.HardpointId))
        {
            Debug.LogWarning(
                "[WinchOverlayRunner] Winch hardpoint has no stable HardpointId. " +
                "Using a runtime instance-id target as compatibility fallback; " +
                "networked routing should require the stable id.",
                hardpoint);
        }

        WinchCartridge cartridge =
            new WinchCartridge(
                hardpoint,
                winch,
                readout,
                requester);

        _activeHardpoint =
            hardpoint;

        _activeWinch =
            winch;

        _activeCartridge =
            cartridge;

        _activeRequester =
            requester;

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
        if (string.IsNullOrWhiteSpace(
                _activeTargetId) ||
            effect.targetId !=
                _activeTargetId)
        {
            return;
        }

        if (effect.kind ==
                MiniGameEffectKind.Control &&
            effect.system ==
                WinchControlIntentPayload.EffectSystem)
        {
            HandleControlIntentEffect(
                effect);

            return;
        }

        if (effect.kind ==
                MiniGameEffectKind.Transaction &&
            effect.system ==
                WinchLineTransferIntentPayload.EffectSystem)
        {
            HandleLineTransferEffect(
                effect);
        }
    }

    private void HandleControlIntentEffect(
        MiniGameEffect effect)
    {
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

    private void HandleLineTransferEffect(
        MiniGameEffect effect)
    {
        if (_activeHardpoint == null ||
            _activeWinch == null)
        {
            NotifyLineTransferResult(
                WinchLineTransferOperation.LoadOneFromPlayer,
                false,
                "WINCH LINE TRANSFER TARGET IS NO LONGER AVAILABLE");

            return;
        }

        if (_activeRequester == null)
        {
            NotifyLineTransferResult(
                WinchLineTransferOperation.LoadOneFromPlayer,
                false,
                "WINCH REQUESTER IS UNAVAILABLE");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                effect.payloadJson))
        {
            NotifyLineTransferResult(
                WinchLineTransferOperation.LoadOneFromPlayer,
                false,
                "MISSING WINCH LINE TRANSFER INTENT");

            return;
        }

        WinchLineTransferIntentPayload payload =
            JsonUtility.FromJson<WinchLineTransferIntentPayload>(
                effect.payloadJson);

        if (payload == null)
        {
            NotifyLineTransferResult(
                WinchLineTransferOperation.LoadOneFromPlayer,
                false,
                "INVALID WINCH LINE TRANSFER INTENT");

            return;
        }

        bool ok =
            WinchLineTransferAuthority.TryApply(
                _activeWinch,
                _activeRequester,
                payload,
                out string message);

        NotifyLineTransferResult(
            payload.operation,
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

    private void NotifyLineTransferResult(
        WinchLineTransferOperation operation,
        bool success,
        string message)
    {
        _activeCartridge?.NotifyLineTransferApplied(
            operation,
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

        _activeRequester =
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
