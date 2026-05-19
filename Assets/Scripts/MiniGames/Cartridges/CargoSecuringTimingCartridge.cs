using System;
using UnityEngine;

namespace MiniGames
{
    public sealed class CargoSecuringTimingCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private enum State
        {
            WaitingToStart,
            Running,
            Finished
        }

        private readonly Action<float, string> _onCompleted;

        private readonly string _ropeButtonLabel;
        private readonly int _ropeCost;
        private readonly Func<int> _getRopeCount;
        private readonly Func<bool> _canUseRope;
        private readonly Func<bool> _onRopeCompleted;

        private MiniGameContext _ctx;
        private System.Random _rng;

        private State _state;
        private float _startSeconds;
        private float _remainingSeconds;

        private bool _startRequested;
        private bool _endRequested;
        private bool _ropeRequested;

        private float _finalQuality01;
        private string _finalRating = "None";
        private string _note;

        public CargoSecuringTimingCartridge(
            Action<float, string> onCompleted,
            string ropeButtonLabel = null,
            int ropeCost = 0,
            Func<int> getRopeCount = null,
            Func<bool> canUseRope = null,
            Func<bool> onRopeCompleted = null)
        {
            _onCompleted = onCompleted;
            _ropeButtonLabel = ropeButtonLabel;
            _ropeCost = Mathf.Max(0, ropeCost);
            _getRopeCount = getRopeCount;
            _canUseRope = canUseRope;
            _onRopeCompleted = onRopeCompleted;
        }

        public void Begin(MiniGameContext context)
        {
            _ctx = context ?? new MiniGameContext();

            int seed = _ctx.seed != 0
                ? _ctx.seed
                : Environment.TickCount;

            _rng = new System.Random(seed);

            _state = State.WaitingToStart;
            _startSeconds = 0f;
            _remainingSeconds = 0f;
            _finalQuality01 = 0f;
            _finalRating = "None";
            _note = "Click Start, or use rope for an automatic perfect result.";
        }

        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            if (_state == State.WaitingToStart && _ropeRequested)
            {
                _ropeRequested = false;
                return FinishRopeAttempt();
            }

            if (_state == State.WaitingToStart && _startRequested)
            {
                _startRequested = false;
                StartTimer();
            }

            if (_state == State.Running)
            {
                _remainingSeconds -= Mathf.Max(0f, dt);

                if (_endRequested)
                {
                    _endRequested = false;
                    return FinishAttempt();
                }
            }

            return new MiniGameResult
            {
                outcome = MiniGameOutcome.None,
                quality01 = 0f,
                note = null,
                hasMeaningfulProgress = false
            };
        }

        public MiniGameResult Cancel()
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 0f,
                note = "Cargo securing cancelled.",
                hasMeaningfulProgress = false
            };
        }

        public MiniGameResult Interrupt(string reason)
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 0f,
                note = $"Cargo securing interrupted: {reason}",
                hasMeaningfulProgress = false
            };
        }

        public void End()
        {
            _ctx = null;
        }

        public void DrawOverlayGUI(Rect panel)
        {
            float pad = 18f;
            float x = panel.x + pad;
            float y = panel.y + pad;
            float w = panel.width - pad * 2f;

            GUI.Label(new Rect(x, y, w, 24), "CARGO SECURING");
            y += 30f;

            GUI.Label(new Rect(x, y, w, 22), _note ?? "");
            y += 34f;

            if (_state == State.WaitingToStart)
            {
                GUI.Label(new Rect(x, y, w, 22), "Timer will be random: 1.00s to 4.00s");
                y += 34f;

                if (GUI.Button(new Rect(x, y, 140f, 34f), "Start"))
                    _startRequested = true;

                DrawRopeButton(x + 154f, y);

                return;
            }

            if (_state == State.Running)
            {
                GUIStyle timerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 42,
                    alignment = TextAnchor.MiddleCenter
                };

                Color old = GUI.color;
                GUI.color = Mathf.Abs(_remainingSeconds) <= 0.20f
                    ? Color.green
                    : _remainingSeconds < 0f
                        ? new Color(1f, 0.45f, 0.35f)
                        : Color.white;

                GUI.Label(
                    new Rect(x, y, w, 70f),
                    _remainingSeconds.ToString("0.00"),
                    timerStyle);

                GUI.color = old;
                y += 86f;

                if (GUI.Button(new Rect(panel.center.x - 70f, y, 140f, 40f), "End"))
                    _endRequested = true;

                y += 54f;

                GUI.Label(
                    new Rect(x, y, w, 22),
                    "Goal: end as close to 0.00 as possible. Early or late both count.");

                return;
            }

            GUI.Label(new Rect(x, y, w, 24), $"Result: {_finalRating}");
            y += 28f;

            GUI.Label(new Rect(x, y, w, 24), $"Quality: {Mathf.RoundToInt(_finalQuality01 * 100f)}%");
        }

        private void DrawRopeButton(float x, float y)
        {
            if (string.IsNullOrWhiteSpace(_ropeButtonLabel) || _onRopeCompleted == null)
                return;

            int have = _getRopeCount != null ? Mathf.Max(0, _getRopeCount()) : 0;
            bool enough = _ropeCost <= 0 || have >= _ropeCost;
            bool canUse = enough && (_canUseRope == null || _canUseRope());

            string label = $"{_ropeButtonLabel}  [{have}]";

            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && canUse;

            if (GUI.Button(new Rect(x, y, 220f, 34f), label))
                _ropeRequested = true;

            GUI.enabled = oldEnabled;
        }

        private void StartTimer()
        {
            int hundredths = _rng.Next(100, 401);

            _startSeconds = hundredths / 100f;
            _remainingSeconds = _startSeconds;

            _state = State.Running;
            _note = "Click End as close to 0.00 as possible.";
        }

        private MiniGameResult FinishRopeAttempt()
        {
            bool ok = _onRopeCompleted != null && _onRopeCompleted();

            if (!ok)
            {
                _note = "Could not use rope.";
                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.None,
                    quality01 = 0f,
                    note = _note,
                    hasMeaningfulProgress = false
                };
            }

            _state = State.Finished;
            _finalQuality01 = 1f;
            _finalRating = "Rope";

            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Completed,
                quality01 = 1f,
                note = "Rope used: perfect result.",
                hasMeaningfulProgress = true
            };
        }

        private MiniGameResult FinishAttempt()
        {
            _state = State.Finished;

            float error = Mathf.Abs(_remainingSeconds);
            _finalQuality01 = EvaluateQuality(error, out _finalRating);
            _note = $"Error: {error:0.00}s";

            bool meaningful = _finalQuality01 > 0f;

            if (meaningful)
                _onCompleted?.Invoke(_finalQuality01, _finalRating);

            return new MiniGameResult
            {
                outcome = meaningful ? MiniGameOutcome.Completed : MiniGameOutcome.Failed,
                quality01 = _finalQuality01,
                note = $"{_finalRating} ({_note})",
                hasMeaningfulProgress = meaningful
            };
        }

        private static float EvaluateQuality(float errorSeconds, out string rating)
        {
            if (errorSeconds <= 0.05f)
            {
                rating = "Perfect";
                return 1.00f;
            }

            if (errorSeconds <= 0.10f)
            {
                rating = "Great";
                return 0.90f;
            }

            if (errorSeconds <= 0.15f)
            {
                rating = "Good";
                return 0.75f;
            }

            if (errorSeconds <= 0.20f)
            {
                rating = "Okay";
                return 0.55f;
            }

            if (errorSeconds <= 0.50f)
            {
                rating = "Bad";
                return 0.25f;
            }

            rating = "Fail";
            return 0f;
        }
    }
}