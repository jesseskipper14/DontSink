using System;
using UnityEngine;

namespace MiniGames
{
    public sealed class MiniGameOverlayHost : MonoBehaviour, IEscapeClosable
    {
        [Header("Behavior")]
        public KeyCode exitKey = KeyCode.Escape;

        [Header("Input Blocking")]
        [SerializeField] private bool blockGameplayInputWhileOpen = true;

        [Header("State (debug)")]
        [SerializeField] private bool isOpen;
        [SerializeField] private string activeTargetId;

        [Header("Escape Routing")]
        [SerializeField] private bool closeViaGlobalEscapeRouter = true;
        [SerializeField] private int escapePriority = 1000;

        private IMiniGameCartridge _cartridge;
        private MiniGameContext _context;
        private MiniGameResult _lastResult;

        private bool _ownsGameplayInputBlock;

        public int EscapePriority => escapePriority;
        public bool IsEscapeOpen => isOpen;
        public bool IsOpen => isOpen;
        public MiniGameResult LastResult => _lastResult;
        public IOverlayDebugDrawable DebugDrawable { get; private set; }
        public IMiniGameCartridge ActiveCartridge => _cartridge;

        public event Action<MiniGameEffect> EffectEmitted;

        public void Open(IMiniGameCartridge cartridge, MiniGameContext context)
        {
            if (isOpen)
                CloseInternal();

            _cartridge = cartridge;
            DebugDrawable = cartridge as IOverlayDebugDrawable;

            _context = context ?? new MiniGameContext();
            _context.emitEffect = EmitEffect;

            activeTargetId = _context.targetId;
            isOpen = true;

            AcquireGameplayInputBlock();

            if (closeViaGlobalEscapeRouter)
            {
                EscapeCloseRegistry registry = EscapeCloseRegistry.GetOrFind();
                if (registry != null)
                    registry.Register(this);
            }

            _cartridge.Begin(_context);
        }

        private void EmitEffect(MiniGameEffect e)
        {
            EffectEmitted?.Invoke(e);
        }

        public MiniGameResult Close()
        {
            if (!isOpen)
                return _lastResult;

            _lastResult = _cartridge != null ? _cartridge.Cancel() : null;
            CloseInternal();
            return _lastResult;
        }

        public MiniGameResult Interrupt(string reason)
        {
            if (!isOpen)
                return _lastResult;

            _lastResult = _cartridge != null ? _cartridge.Interrupt(reason) : null;
            CloseInternal();
            return _lastResult;
        }

        private void Update()
        {
            if (!isOpen)
                return;

            if (!closeViaGlobalEscapeRouter && Input.GetKeyDown(exitKey))
            {
                Close();
                return;
            }

            var input = MiniGameInput.FromUnity();
            var result = _cartridge.Tick(Time.deltaTime, input);

            if (result != null && result.outcome != MiniGameOutcome.None)
            {
                if (_cartridge is IMiniGameLifecycleHints hints && hints.IsOngoing)
                {
                    _lastResult = result;
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

            if (_context != null)
                _context.emitEffect = null;

            _cartridge = null;
            DebugDrawable = null;
            _context = null;

            isOpen = false;

            ReleaseGameplayInputBlock();

            if (EscapeCloseRegistry.I != null)
                EscapeCloseRegistry.I.Unregister(this);

            activeTargetId = null;
        }

        private void AcquireGameplayInputBlock()
        {
            if (!blockGameplayInputWhileOpen)
                return;

            if (_ownsGameplayInputBlock)
                return;

            GameplayInputBlocker.Push(this);
            _ownsGameplayInputBlock = true;
        }

        private void ReleaseGameplayInputBlock()
        {
            if (!_ownsGameplayInputBlock)
                return;

            GameplayInputBlocker.Pop(this);
            _ownsGameplayInputBlock = false;
        }

        private void OnDisable()
        {
            ReleaseGameplayInputBlock();

            if (EscapeCloseRegistry.I != null)
                EscapeCloseRegistry.I.Unregister(this);
        }

        private void OnDestroy()
        {
            ReleaseGameplayInputBlock();

            if (EscapeCloseRegistry.I != null)
                EscapeCloseRegistry.I.Unregister(this);
        }

        public bool CloseFromEscape()
        {
            if (!isOpen)
                return false;

            Close();
            return true;
        }
    }
}