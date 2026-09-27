using System;
using System.Collections.Generic;
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

        public sealed class TetherOption
        {
            public string ButtonLabel { get; }
            public string ResultLabel { get; }
            public int Cost { get; }
            public Func<int> GetCount { get; }
            public Func<bool> CanUse { get; }
            public Func<bool> OnCompleted { get; }

            public TetherOption(
                string buttonLabel,
                string resultLabel,
                int cost,
                Func<int> getCount,
                Func<bool> canUse,
                Func<bool> onCompleted)
            {
                ButtonLabel = buttonLabel;
                ResultLabel = resultLabel;
                Cost = Mathf.Max(0, cost);
                GetCount = getCount;
                CanUse = canUse;
                OnCompleted = onCompleted;
            }
        }

        private readonly Action<float, string> _onCompleted;
        private readonly IReadOnlyList<TetherOption> _tetherOptions;

        private MiniGameContext _ctx;
        private System.Random _rng;

        private State _state;
        private float _startSeconds;
        private float _remainingSeconds;

        private bool _startRequested;
        private bool _endRequested;
        private int _requestedTetherIndex = -1;

        private float _finalQuality01;
        private string _finalRating = "None";
        private string _note;

        public CargoSecuringTimingCartridge(
            Action<float, string> onCompleted,
            IReadOnlyList<TetherOption> tetherOptions)
        {
            _onCompleted = onCompleted;
            _tetherOptions = tetherOptions;
        }

        // Backward-compatible constructor for any older call sites that still
        // provide one rope bypass option.
        public CargoSecuringTimingCartridge(
            Action<float, string> onCompleted,
            string ropeButtonLabel = null,
            int ropeCost = 0,
            Func<int> getRopeCount = null,
            Func<bool> canUseRope = null,
            Func<bool> onRopeCompleted = null)
            : this(
                onCompleted,
                BuildLegacyTetherOptions(
                    ropeButtonLabel,
                    ropeCost,
                    getRopeCount,
                    canUseRope,
                    onRopeCompleted))
        {
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
            _requestedTetherIndex = -1;
            _note =
                HasTetherOptions()
                    ? "Click Start, or choose a tether for an automatic perfect result."
                    : "Click Start, then click End as close to 0.00 as possible.";
        }

        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            if (_state == State.WaitingToStart &&
                _requestedTetherIndex >= 0)
            {
                int requestedIndex =
                    _requestedTetherIndex;

                _requestedTetherIndex = -1;

                return FinishTetherAttempt(
                    requestedIndex);
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

                y += 48f;

                DrawTetherButtons(
                    x,
                    ref y,
                    w);

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

        private void DrawTetherButtons(
            float x,
            ref float y,
            float width)
        {
            if (!HasTetherOptions())
                return;

            GUI.Label(
                new Rect(
                    x,
                    y,
                    width,
                    22f),
                "USE TETHER");

            y += 28f;

            for (int i = 0;
                 i < _tetherOptions.Count;
                 i++)
            {
                TetherOption option =
                    _tetherOptions[i];

                if (option == null ||
                    option.OnCompleted == null)
                {
                    continue;
                }

                int have =
                    option.GetCount != null
                        ? Mathf.Max(
                            0,
                            option.GetCount())
                        : 0;

                bool enough =
                    option.Cost <= 0 ||
                    have >= option.Cost;

                bool canUse =
                    enough &&
                    (option.CanUse == null ||
                     option.CanUse());

                string label =
                    $"{option.ButtonLabel}  " +
                    $"[Have: {have}]";

                bool oldEnabled =
                    GUI.enabled;

                GUI.enabled =
                    oldEnabled &&
                    canUse;

                if (GUI.Button(
                        new Rect(
                            x,
                            y,
                            width,
                            34f),
                        label))
                {
                    _requestedTetherIndex =
                        i;
                }

                GUI.enabled =
                    oldEnabled;

                y += 40f;
            }
        }

        private bool HasTetherOptions()
        {
            return
                _tetherOptions != null &&
                _tetherOptions.Count > 0;
        }

        private static IReadOnlyList<TetherOption>
            BuildLegacyTetherOptions(
                string ropeButtonLabel,
                int ropeCost,
                Func<int> getRopeCount,
                Func<bool> canUseRope,
                Func<bool> onRopeCompleted)
        {
            if (string.IsNullOrWhiteSpace(
                    ropeButtonLabel) ||
                onRopeCompleted == null)
            {
                return null;
            }

            return
                new[]
                {
                    new TetherOption(
                        ropeButtonLabel,
                        "Rope",
                        ropeCost,
                        getRopeCount,
                        canUseRope,
                        onRopeCompleted)
                };
        }

        private void StartTimer()
        {
            int hundredths = _rng.Next(100, 401);

            _startSeconds = hundredths / 100f;
            _remainingSeconds = _startSeconds;

            _state = State.Running;
            _note = "Click End as close to 0.00 as possible.";
        }

        private MiniGameResult FinishTetherAttempt(
            int optionIndex)
        {
            if (_tetherOptions == null ||
                optionIndex < 0 ||
                optionIndex >= _tetherOptions.Count)
            {
                _note =
                    "Could not use tether.";

                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.None,
                    quality01 = 0f,
                    note = _note,
                    hasMeaningfulProgress = false
                };
            }

            TetherOption option =
                _tetherOptions[optionIndex];

            bool ok =
                option != null &&
                option.OnCompleted != null &&
                option.OnCompleted();

            if (!ok)
            {
                _note =
                    "Could not use tether.";

                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.None,
                    quality01 = 0f,
                    note = _note,
                    hasMeaningfulProgress = false
                };
            }

            string resultLabel =
                option != null &&
                !string.IsNullOrWhiteSpace(
                    option.ResultLabel)
                    ? option.ResultLabel
                    : "Tether";

            _state =
                State.Finished;

            _finalQuality01 =
                1f;

            _finalRating =
                resultLabel;

            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Completed,
                quality01 = 1f,
                note =
                    $"{resultLabel} used: perfect result.",
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