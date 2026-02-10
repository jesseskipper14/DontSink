using System; // <-- add
using UnityEngine;

namespace MiniGames
{
    public sealed class MiniGameOverlayHost : MonoBehaviour
    {
        [Header("Behavior")]
        public KeyCode exitKey = KeyCode.Escape;

        [Header("State (debug)")]
        [SerializeField] private bool isOpen;
        [SerializeField] private string activeTargetId;

        private IMiniGameCartridge _cartridge;
        private MiniGameContext _context;
        private MiniGameResult _lastResult;

        public bool IsOpen => isOpen;
        public MiniGameResult LastResult => _lastResult;
        public IOverlayDebugDrawable DebugDrawable { get; private set; }
        public IMiniGameCartridge ActiveCartridge => _cartridge;

        // NEW: consumers (services, runners) can subscribe to effects from the active mini-game.
        public event Action<MiniGameEffect> EffectEmitted;

        public void Open(IMiniGameCartridge cartridge, MiniGameContext context)
        {
            if (isOpen) CloseInternal(); // hard close previous

            _cartridge = cartridge;
            DebugDrawable = cartridge as IOverlayDebugDrawable;

            _context = context ?? new MiniGameContext();

            // NEW: provide effect emitter hook to cartridge via context
            _context.emitEffect = EmitEffect;

            activeTargetId = _context.targetId;
            isOpen = true;

            _cartridge.Begin(_context);
        }

        private void EmitEffect(MiniGameEffect e)
        {
            EffectEmitted?.Invoke(e);
        }

        public MiniGameResult Close()
        {
            if (!isOpen) return _lastResult;

            _lastResult = _cartridge != null ? _cartridge.Cancel() : null;
            CloseInternal();
            return _lastResult;
        }

        public MiniGameResult Interrupt(string reason)
        {
            if (!isOpen) return _lastResult;

            _lastResult = _cartridge != null ? _cartridge.Interrupt(reason) : null;
            CloseInternal();
            return _lastResult;
        }

        private void Update()
        {
            if (!isOpen) return;

            if (Input.GetKeyDown(exitKey))
            {
                Close();
                return;
            }

            var input = MiniGameInput.FromUnity();
            var result = _cartridge.Tick(Time.deltaTime, input);

            if (result != null && result.outcome != MiniGameOutcome.None)
            {
                // Ongoing mini-games (e.g., piloting) are allowed to emit outcomes without auto-closing.
                // They only end when the player closes or the world interrupts.
                if (_cartridge is IMiniGameLifecycleHints hints && hints.IsOngoing)
                {
                    _lastResult = result; // keep latest summary for debug/telemetry
                    return;
                }

                _lastResult = result;
                CloseInternal();
            }
        }

        private void CloseInternal()
        {
            if (_cartridge != null)
                _cartridge.End();

            // NEW: clear emitter to avoid dangling delegate references
            if (_context != null)
                _context.emitEffect = null;

            _cartridge = null;
            DebugDrawable = null;
            _context = null;

            isOpen = false;
            activeTargetId = null;
        }
    }
}
