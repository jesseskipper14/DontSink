using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniGames
{
    [Serializable]
    public sealed class MoneyChestCoinVisualDefinition
    {
        [Header("Art")]
        public Sprite sprite;

        [Header("Value")]
        [Min(1)] public int value = 1;

        [Header("Usage")]
        public bool useForExactCounting = true;
        public bool useForRepresentativePile = true;

        [Tooltip("Minimum chest balance before this coin can appear in representative mode.")]
        [Min(0)] public int representativeMinBalance = 0;

        [Tooltip("Relative chance in representative mode.")]
        [Min(0f)] public float representativeWeight = 1f;
    }

    [Serializable]
    public sealed class MoneyChestVisualSettings
    {
        [Header("Sprites")]
        public Sprite chestInteriorSprite;
        public MoneyChestCoinVisualDefinition[] coinVisuals;

        [Header("Chest Layout")]
        [Tooltip("Normalized rect inside the chest sprite where coins are allowed to appear. X/Y are top-left normalized.")]
        public Rect coinArea01 = new Rect(0.14f, 0.30f, 0.72f, 0.52f);

        [Header("Accounting")]
        [Tooltip("At or below this balance, visible coin values try to add up exactly.")]
        [Min(0)] public int exactVisualAccountingMaxBalance = 1000;

        [Tooltip("Exact mode will split larger coins into smaller coins until it approaches this visual count.")]
        [Min(1)] public int exactTargetCoinsAtMaxBalance = 55;

        [Tooltip("Hard cap for exact-mode visible coins.")]
        [Min(1)] public int maxExactVisualCoins = 90;

        [Header("Representative Coin Count")]
        [Min(0)] public int emptyChestCoinCount = 0;
        [Min(1)] public int poorChestCoinCount = 6;
        [Min(1)] public int fullChestCoinCount = 150;

        [Tooltip("Balance where the chest should feel visually full of ordinary coins.")]
        [Min(1)] public int balanceForFullChest = 10000;

        [Tooltip("Balance where fancy/rich visuals become common.")]
        [Min(1)] public int balanceForDisgustingRichChest = 100000;

        [Header("Coin Rendering")]
        [Tooltip("Uses each sliced sprite's source size, multiplied by this scale. No random scaling.")]
        [Min(0.1f)] public float coinSpriteScale = 1f;

        [Tooltip("Used only when no coin sprite exists for a coin.")]
        [Min(1f)] public float fallbackCoinSizePx = 16f;

        [Header("Exact/Representative Transition")]
        [Tooltip("Representative pile mode eases in over this many balance units after exact visual accounting ends.")]
        [Min(0)] public int representativeTransitionBlendRange = 2000;

        [Tooltip("Representative coin types fade in over this many balance units after their representativeMinBalance.")]
        [Min(0)] public int representativeUnlockFadeRange = 3000;

        [Header("Behavior")]
        public bool rebuildWhenBalanceChanges = true;
        public bool showDebugInfo = false;
    }

    public sealed class MoneyChestCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private struct CoinVisual
        {
            public Vector2 normPos01;
            public Vector2 sizePx;
            public float rotationDeg;
            public float layerKey;
            public int definitionIndex;
            public int value;
            public Color fallbackColor;
        }

        private readonly MoneyChestState _chest;
        private readonly MoneyChestVisualSettings _settings;
        private readonly List<CoinVisual> _coins = new List<CoinVisual>(192);

        private MiniGameContext _ctx;
        private bool _requestedClose;
        private string _lastNote;

        private int _lastBalance = int.MinValue;
        private int _shakeGeneration;
        private Texture2D _white;

        private bool _lastBuildWasExact;
        private int _lastVisualSum;

        public MoneyChestCartridge(MoneyChestState chest, MoneyChestVisualSettings settings)
        {
            _chest = chest;
            _settings = settings ?? new MoneyChestVisualSettings();
        }

        public void Begin(MiniGameContext context)
        {
            _ctx = context ?? new MiniGameContext();
            _requestedClose = false;
            _lastNote = null;
            _shakeGeneration = 0;
            _white = Texture2D.whiteTexture;

            RebuildCoinVisuals("Begin");
        }

        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            if (_requestedClose)
            {
                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.Cancelled,
                    quality01 = 1f,
                    note = string.IsNullOrWhiteSpace(_lastNote) ? "Closed money chest" : _lastNote,
                    hasMeaningfulProgress = false
                };
            }

            if (_settings.rebuildWhenBalanceChanges && _chest != null && _chest.Balance != _lastBalance)
                RebuildCoinVisuals("Balance changed");

            return new MiniGameResult
            {
                outcome = MiniGameOutcome.None,
                quality01 = 1f,
                note = null,
                hasMeaningfulProgress = false
            };
        }

        public MiniGameResult Cancel()
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = "Cancelled money chest",
                hasMeaningfulProgress = false
            };
        }

        public MiniGameResult Interrupt(string reason)
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = $"Interrupted money chest: {reason}",
                hasMeaningfulProgress = false
            };
        }

        public void End()
        {
            _ctx = null;
            _coins.Clear();
        }

        public void DrawOverlayGUI(Rect panel)
        {
            float pad = 14f;

            int balance = CurrentBalance;
            string chestId = _chest != null ? _chest.ChestInstanceId : "missing";

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 10f, panel.width - pad * 2f, 24f),
                "MONEY CHEST");

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 34f, panel.width - pad * 2f, 22f),
                $"Balance: ${balance:n0}");

            if (_settings.showDebugInfo)
            {
                string mode = _lastBuildWasExact ? "Exact" : "Representative";

                GUI.Label(
                    new Rect(panel.x + pad, panel.y + 56f, panel.width - pad * 2f, 20f),
                    $"id='{chestId}' coins={_coins.Count} visualSum={_lastVisualSum} mode={mode} shake={_shakeGeneration}");
            }

            float closeX = panel.xMax - 36f;
            float closeY = panel.y + 8f;

            if (GUI.Button(new Rect(closeX, closeY, 28f, 24f), "X"))
            {
                _requestedClose = true;
                _lastNote = "Closed money chest";
            }

            Rect chestRect = new Rect(
                panel.x + pad,
                panel.y + 82f,
                panel.width - pad * 2f,
                panel.height - 150f);

            Rect renderedChestRect = DrawChestInterior(chestRect);
            Rect coinAreaRect = GetCoinAreaRect(renderedChestRect);

            DrawCoins(coinAreaRect);

            if (_settings.chestInteriorSprite == null)
                DrawChestLidAndBorder(renderedChestRect);

            Rect bottom = new Rect(
                panel.x + pad,
                panel.yMax - 54f,
                panel.width - pad * 2f,
                38f);

            if (GUI.Button(new Rect(bottom.x, bottom.y, 120f, bottom.height), "Shake"))
            {
                _shakeGeneration++;
                RebuildCoinVisuals("Shake");
                _lastNote = "Shook money chest";
            }

            if (GUI.Button(new Rect(bottom.x + 130f, bottom.y, 120f, bottom.height), "Recount"))
            {
                RebuildCoinVisuals("Recount");
                _lastNote = "Rebuilt coin visuals";
            }

            GUI.Label(
                new Rect(bottom.x + 270f, bottom.y + 8f, bottom.width - 400f, 22f),
                GetWealthFlavor(balance));

            if (GUI.Button(new Rect(bottom.xMax - 120f, bottom.y, 120f, bottom.height), "Close"))
            {
                _requestedClose = true;
                _lastNote = "Closed money chest";
            }
        }

        private int CurrentBalance
        {
            get
            {
                if (_chest == null)
                    return 0;

                return Mathf.Max(0, _chest.Balance);
            }
        }

        private void RebuildCoinVisuals(string reason)
        {
            _coins.Clear();

            int balance = CurrentBalance;
            _lastBalance = balance;
            _lastVisualSum = 0;
            _lastBuildWasExact = false;

            if (balance <= 0)
                return;

            int seed = BuildSeed(balance, _shakeGeneration);
            System.Random rng = new System.Random(seed);

            bool shouldExact =
                balance <= Mathf.Max(0, _settings.exactVisualAccountingMaxBalance) &&
                HasExactCountingDefinitions();

            if (shouldExact && TryBuildExactCoinVisuals(balance, rng))
                return;

            BuildRepresentativeCoinVisuals(balance, rng);
        }

        private bool TryBuildExactCoinVisuals(int balance, System.Random rng)
        {
            List<int> definitionIndices = BuildBalancedExactCoinList(balance, rng);

            if (definitionIndices == null || definitionIndices.Count == 0)
                return false;

            int sum = SumDefinitionValues(definitionIndices);
            if (sum != balance)
                return false;

            List<Vector2> positions = BuildDistributedPositions01(definitionIndices.Count, rng);

            for (int i = 0; i < definitionIndices.Count; i++)
            {
                int definitionIndex = definitionIndices[i];
                Vector2 pos01 = positions[i];

                AddCoinVisualFromDefinition(definitionIndex, pos01, rng);
            }

            SortCoinVisuals();

            _lastBuildWasExact = true;
            _lastVisualSum = sum;

            return true;
        }

        private List<int> BuildBalancedExactCoinList(int amount, System.Random rng)
        {
            if (amount <= 0)
                return new List<int>();

            List<int> values = CollectExactValuesAscending(null);
            if (values.Count == 0)
                return null;

            int minValue = values[0];
            if (minValue <= 0)
                return null;

            int maxCoinsByValue = amount / minValue;
            int maxCoins = Mathf.Min(
                Mathf.Max(1, _settings.maxExactVisualCoins),
                Mathf.Max(1, maxCoinsByValue));

            int targetCount = Mathf.Clamp(
                ComputeExactTargetCoinCount(amount),
                1,
                maxCoins);

            bool[,] dp = BuildExactCountDp(amount, maxCoins, values);
            int feasibleCount = FindNearestFeasibleExactCount(dp, amount, targetCount, maxCoins);

            if (feasibleCount <= 0)
                return null;

            List<int> chosenValues = ReconstructExactValuesFromDp(
                dp,
                values,
                amount,
                feasibleCount,
                rng);

            if (chosenValues == null || chosenValues.Count == 0)
                return null;

            List<int> definitionIndices = new List<int>(chosenValues.Count);

            for (int i = 0; i < chosenValues.Count; i++)
            {
                int definitionIndex = PickExactDefinitionIndexForValue(chosenValues[i], rng, null);
                if (definitionIndex < 0)
                    return null;

                definitionIndices.Add(definitionIndex);
            }

            return definitionIndices;
        }

        private bool[,] BuildExactCountDp(int amount, int maxCoins, List<int> values)
        {
            bool[,] dp = new bool[maxCoins + 1, amount + 1];
            dp[0, 0] = true;

            for (int coinCount = 1; coinCount <= maxCoins; coinCount++)
            {
                for (int sum = 0; sum <= amount; sum++)
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        int value = values[i];

                        if (value <= 0)
                            continue;

                        if (sum < value)
                            continue;

                        if (dp[coinCount - 1, sum - value])
                        {
                            dp[coinCount, sum] = true;
                            break;
                        }
                    }
                }
            }

            return dp;
        }

        private int FindNearestFeasibleExactCount(bool[,] dp, int amount, int targetCount, int maxCoins)
        {
            if (dp == null)
                return -1;

            targetCount = Mathf.Clamp(targetCount, 1, maxCoins);

            if (dp[targetCount, amount])
                return targetCount;

            for (int offset = 1; offset <= maxCoins; offset++)
            {
                int lower = targetCount - offset;
                int upper = targetCount + offset;

                // Prefer slightly fewer coins over suddenly exploding the pile.
                if (lower >= 1 && dp[lower, amount])
                    return lower;

                if (upper <= maxCoins && dp[upper, amount])
                    return upper;
            }

            return -1;
        }

        private List<int> ReconstructExactValuesFromDp(
            bool[,] dp,
            List<int> values,
            int amount,
            int coinCount,
            System.Random rng)
        {
            List<int> result = new List<int>(coinCount);

            int remainingAmount = amount;
            int remainingCoins = coinCount;

            while (remainingCoins > 0)
            {
                List<int> candidates = new List<int>();

                for (int i = 0; i < values.Count; i++)
                {
                    int value = values[i];

                    if (value <= 0)
                        continue;

                    if (remainingAmount < value)
                        continue;

                    if (dp[remainingCoins - 1, remainingAmount - value])
                        candidates.Add(value);
                }

                if (candidates.Count == 0)
                    return null;

                float targetAverage = remainingAmount / Mathf.Max(1f, remainingCoins);
                int picked = PickExactValueNearAverage(candidates, targetAverage, rng);

                result.Add(picked);

                remainingAmount -= picked;
                remainingCoins--;
            }

            if (remainingAmount != 0)
                return null;

            return result;
        }

        private int PickExactValueNearAverage(List<int> candidates, float targetAverage, System.Random rng)
        {
            if (candidates == null || candidates.Count == 0)
                return 1;

            float totalWeight = 0f;
            float[] weights = new float[candidates.Count];

            for (int i = 0; i < candidates.Count; i++)
            {
                int value = candidates[i];

                // Prefer values near the needed average. This avoids both:
                // - all tiny coins
                // - all giant coins
                float distance = Mathf.Abs(value - targetAverage);
                float weight = 1f / (1f + distance);

                // Tiny nudge upward so values like 10/20 are allowed to show up
                // when the average supports them.
                weight *= Mathf.Sqrt(Mathf.Max(1f, value));

                weights[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return candidates[rng.Next(0, candidates.Count)];

            float roll = NextFloat(rng) * totalWeight;
            float cursor = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                cursor += weights[i];

                if (roll <= cursor)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private List<int> CollectExactValuesAscending(Predicate<MoneyChestCoinVisualDefinition> extraFilter)
        {
            List<int> values = new List<int>();

            if (_settings.coinVisuals == null)
                return values;

            for (int i = 0; i < _settings.coinVisuals.Length; i++)
            {
                MoneyChestCoinVisualDefinition def = _settings.coinVisuals[i];
                if (def == null)
                    continue;

                if (!def.useForExactCounting)
                    continue;

                if (def.value <= 0)
                    continue;

                if (extraFilter != null && !extraFilter(def))
                    continue;

                if (!values.Contains(def.value))
                    values.Add(def.value);
            }

            values.Sort();
            return values;
        }

        private void BuildRepresentativeCoinVisuals(int balance, System.Random rng)
        {
            int coinCount = ComputeRepresentativeCoinCount(balance);
            if (coinCount <= 0)
                return;

            List<Vector2> positions = BuildDistributedPositions01(coinCount, rng);

            for (int i = 0; i < coinCount; i++)
            {
                int definitionIndex = PickRepresentativeDefinitionIndex(balance, rng);
                Vector2 pos01 = positions[i];
                AddCoinVisualFromDefinition(definitionIndex, pos01, rng);
            }

            SortCoinVisuals();

            _lastBuildWasExact = false;
            _lastVisualSum = SumCoinVisualValues();
        }

        private void AddCoinVisualFromDefinition(int definitionIndex, Vector2 pos01, System.Random rng)
        {
            Vector2 sizePx = GetCoinDrawSize(definitionIndex);
            int value = GetDefinitionValue(definitionIndex);

            _coins.Add(new CoinVisual
            {
                normPos01 = pos01,
                sizePx = sizePx,
                rotationDeg = Mathf.Lerp(-35f, 35f, NextFloat(rng)),
                layerKey = NextFloat(rng),
                definitionIndex = definitionIndex,
                value = value,
                fallbackColor = PickFallbackCoinColor(value, definitionIndex)
            });
        }

        private List<Vector2> BuildDistributedPositions01(int count, System.Random rng)
        {
            List<Vector2> result = new List<Vector2>(Mathf.Max(0, count));
            if (count <= 0)
                return result;

            // Use a shuffled grid so fuller chests naturally occupy empty spaces more evenly.
            // Sparse chests still spread across the whole chest, just with fewer occupied cells.
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count * 1.35f));
            cols = Mathf.Max(1, cols);

            int rows = Mathf.CeilToInt(count / (float)cols);
            rows = Mathf.Max(1, rows);

            int cellCount = rows * cols;
            int[] cells = new int[cellCount];

            for (int i = 0; i < cellCount; i++)
                cells[i] = i;

            // Fisher-Yates shuffle
            for (int i = cellCount - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                int temp = cells[i];
                cells[i] = cells[j];
                cells[j] = temp;
            }

            for (int i = 0; i < count; i++)
            {
                int cell = cells[i];
                int row = cell / cols;
                int col = cell % cols;

                // Keep some jitter, but stay comfortably within the cell
                float jx = Mathf.Lerp(0.18f, 0.82f, NextFloat(rng));
                float jy = Mathf.Lerp(0.18f, 0.82f, NextFloat(rng));

                float x = (col + jx) / cols;
                float y = (row + jy) / rows;

                result.Add(new Vector2(
                    Mathf.Clamp01(x),
                    Mathf.Clamp01(y)));
            }

            return result;
        }

        private List<int> BuildExactGreedyCoinList(
            int amount,
            System.Random rng,
            Predicate<MoneyChestCoinVisualDefinition> extraFilter)
        {
            if (amount <= 0)
                return new List<int>();

            List<int> values = CollectExactValuesDescending(extraFilter);
            if (values.Count == 0)
                return null;

            List<int> result = new List<int>();
            int remaining = amount;

            for (int i = 0; i < values.Count; i++)
            {
                int value = values[i];
                if (value <= 0)
                    continue;

                int count = remaining / value;
                if (count <= 0)
                    continue;

                for (int c = 0; c < count; c++)
                {
                    int definitionIndex = PickExactDefinitionIndexForValue(value, rng, extraFilter);
                    if (definitionIndex < 0)
                        return null;

                    result.Add(definitionIndex);
                }

                remaining -= count * value;
            }

            if (remaining != 0)
                return null;

            return result;
        }

        private void ExpandExactCoinsTowardTarget(List<int> definitionIndices, int targetCount, System.Random rng)
        {
            if (definitionIndices == null || definitionIndices.Count == 0)
                return;

            int maxExact = Mathf.Max(1, _settings.maxExactVisualCoins);
            targetCount = Mathf.Clamp(targetCount, 1, maxExact);

            int guard = 0;

            while (definitionIndices.Count < targetCount && definitionIndices.Count < maxExact && guard < 512)
            {
                guard++;

                int candidateListIndex = PickSplittableCoinListIndex(definitionIndices, rng);
                if (candidateListIndex < 0)
                    return;

                int oldDefinitionIndex = definitionIndices[candidateListIndex];
                int oldValue = GetDefinitionValue(oldDefinitionIndex);

                List<int> replacement = BuildExactGreedyCoinList(
                    oldValue,
                    rng,
                    def => def != null &&
                           def.useForExactCounting &&
                           def.value > 0 &&
                           def.value < oldValue);

                if (replacement == null || replacement.Count <= 1)
                    return;

                int newCount = definitionIndices.Count - 1 + replacement.Count;
                if (newCount > maxExact)
                    return;

                definitionIndices.RemoveAt(candidateListIndex);
                definitionIndices.AddRange(replacement);
            }
        }

        private int PickSplittableCoinListIndex(List<int> definitionIndices, System.Random rng)
        {
            if (definitionIndices == null || definitionIndices.Count == 0)
                return -1;

            List<int> candidates = new List<int>();

            for (int i = 0; i < definitionIndices.Count; i++)
            {
                int value = GetDefinitionValue(definitionIndices[i]);

                if (value <= 1)
                    continue;

                if (HasExactDefinitionBelowValue(value))
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
                return -1;

            // Bias toward splitting larger coins first.
            candidates.Sort((a, b) =>
            {
                int av = GetDefinitionValue(definitionIndices[a]);
                int bv = GetDefinitionValue(definitionIndices[b]);
                return bv.CompareTo(av);
            });

            int topWindow = Mathf.Min(candidates.Count, 4);
            return candidates[rng.Next(0, topWindow)];
        }

        private int ComputeExactTargetCoinCount(int balance)
        {
            if (balance <= 0)
                return 0;

            int threshold = Mathf.Max(1, _settings.exactVisualAccountingMaxBalance);
            int maxTarget = Mathf.Max(1, _settings.exactTargetCoinsAtMaxBalance);

            float t = Mathf.Clamp01(balance / (float)threshold);

            // sqrt makes low balances gain visible coins quickly,
            // but SmoothStep keeps the top end from going feral.
            t = Mathf.Sqrt(t);
            t = Mathf.SmoothStep(0f, 1f, t);

            return Mathf.RoundToInt(Mathf.Lerp(3f, maxTarget, t));
        }

        private int ComputeRepresentativeCoinCount(int balance)
        {
            if (balance <= 0)
                return Mathf.Max(0, _settings.emptyChestCoinCount);

            int poor = Mathf.Max(1, _settings.poorChestCoinCount);
            int full = Mathf.Max(poor, _settings.fullChestCoinCount);

            int rawRepresentativeCount = ComputeRawRepresentativeCoinCount(balance);

            int exactThreshold = Mathf.Max(0, _settings.exactVisualAccountingMaxBalance);
            int blendRange = Mathf.Max(0, _settings.representativeTransitionBlendRange);

            if (exactThreshold > 0 && blendRange > 0 && balance > exactThreshold && balance < exactThreshold + blendRange)
            {
                int exactEndCount = Mathf.Clamp(
                    ComputeExactTargetCoinCount(exactThreshold),
                    poor,
                    full);

                float t = Mathf.InverseLerp(
                    exactThreshold,
                    exactThreshold + blendRange,
                    balance);

                t = Mathf.SmoothStep(0f, 1f, t);

                return Mathf.RoundToInt(Mathf.Lerp(
                    exactEndCount,
                    rawRepresentativeCount,
                    t));
            }

            return rawRepresentativeCount;
        }

        private int ComputeRawRepresentativeCoinCount(int balance)
        {
            if (balance <= 0)
                return Mathf.Max(0, _settings.emptyChestCoinCount);

            float fullness01 = ComputeFullness01(balance);

            int poor = Mathf.Max(1, _settings.poorChestCoinCount);
            int full = Mathf.Max(poor, _settings.fullChestCoinCount);

            float eased = Mathf.SmoothStep(0f, 1f, fullness01);

            return Mathf.RoundToInt(Mathf.Lerp(poor, full, eased));
        }

        private float ComputeFullness01(int balance)
        {
            if (balance <= 0)
                return 0f;

            float fullAt = Mathf.Max(1f, _settings.balanceForFullChest);

            float logBalance = Mathf.Log10(balance + 1f);
            float logFull = Mathf.Log10(fullAt + 1f);

            return Mathf.Clamp01(logBalance / Mathf.Max(0.001f, logFull));
        }

        private bool HasExactCountingDefinitions()
        {
            if (_settings.coinVisuals == null)
                return false;

            for (int i = 0; i < _settings.coinVisuals.Length; i++)
            {
                MoneyChestCoinVisualDefinition def = _settings.coinVisuals[i];
                if (def != null && def.useForExactCounting && def.value > 0)
                    return true;
            }

            return false;
        }

        private bool HasExactDefinitionBelowValue(int value)
        {
            if (_settings.coinVisuals == null)
                return false;

            for (int i = 0; i < _settings.coinVisuals.Length; i++)
            {
                MoneyChestCoinVisualDefinition def = _settings.coinVisuals[i];
                if (def == null)
                    continue;

                if (!def.useForExactCounting)
                    continue;

                if (def.value > 0 && def.value < value)
                    return true;
            }

            return false;
        }

        private List<int> CollectExactValuesDescending(Predicate<MoneyChestCoinVisualDefinition> extraFilter)
        {
            List<int> values = new List<int>();

            if (_settings.coinVisuals == null)
                return values;

            for (int i = 0; i < _settings.coinVisuals.Length; i++)
            {
                MoneyChestCoinVisualDefinition def = _settings.coinVisuals[i];
                if (def == null)
                    continue;

                if (!def.useForExactCounting)
                    continue;

                if (def.value <= 0)
                    continue;

                if (extraFilter != null && !extraFilter(def))
                    continue;

                if (!values.Contains(def.value))
                    values.Add(def.value);
            }

            values.Sort((a, b) => b.CompareTo(a));
            return values;
        }

        private int PickExactDefinitionIndexForValue(
            int value,
            System.Random rng,
            Predicate<MoneyChestCoinVisualDefinition> extraFilter)
        {
            if (_settings.coinVisuals == null)
                return -1;

            List<int> candidates = new List<int>();

            for (int i = 0; i < _settings.coinVisuals.Length; i++)
            {
                MoneyChestCoinVisualDefinition def = _settings.coinVisuals[i];
                if (def == null)
                    continue;

                if (!def.useForExactCounting)
                    continue;

                if (def.value != value)
                    continue;

                if (extraFilter != null && !extraFilter(def))
                    continue;

                candidates.Add(i);
            }

            if (candidates.Count == 0)
                return -1;

            return candidates[rng.Next(0, candidates.Count)];
        }

        private int PickRepresentativeDefinitionIndex(int balance, System.Random rng)
        {
            if (_settings.coinVisuals == null || _settings.coinVisuals.Length == 0)
                return -1;

            float totalWeight = 0f;
            float[] weights = new float[_settings.coinVisuals.Length];

            for (int i = 0; i < _settings.coinVisuals.Length; i++)
            {
                MoneyChestCoinVisualDefinition def = _settings.coinVisuals[i];
                if (def == null)
                    continue;

                if (!def.useForRepresentativePile)
                    continue;

                float unlock01 = GetRepresentativeUnlock01(balance, def);
                if (unlock01 <= 0f)
                    continue;

                float weight = Mathf.Max(0f, def.representativeWeight) * unlock01;

                weights[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return PickAnyUsableDefinitionIndex(rng);

            float roll = NextFloat(rng) * totalWeight;
            float cursor = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                cursor += weights[i];

                if (roll <= cursor)
                    return i;
            }

            return PickAnyUsableDefinitionIndex(rng);
        }

        private float GetRepresentativeUnlock01(int balance, MoneyChestCoinVisualDefinition def)
        {
            if (def == null)
                return 0f;

            if (balance < def.representativeMinBalance)
                return 0f;

            int fadeRange = Mathf.Max(0, _settings.representativeUnlockFadeRange);
            if (fadeRange <= 0)
                return 1f;

            float t = Mathf.InverseLerp(
                def.representativeMinBalance,
                def.representativeMinBalance + fadeRange,
                balance);

            return Mathf.SmoothStep(0f, 1f, t);
        }

        private int PickAnyUsableDefinitionIndex(System.Random rng)
        {
            if (_settings.coinVisuals == null || _settings.coinVisuals.Length == 0)
                return -1;

            List<int> candidates = new List<int>();

            for (int i = 0; i < _settings.coinVisuals.Length; i++)
            {
                if (_settings.coinVisuals[i] != null)
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
                return -1;

            return candidates[rng.Next(0, candidates.Count)];
        }

        private int SumDefinitionValues(List<int> definitionIndices)
        {
            if (definitionIndices == null)
                return 0;

            int total = 0;

            for (int i = 0; i < definitionIndices.Count; i++)
                total += GetDefinitionValue(definitionIndices[i]);

            return total;
        }

        private int SumCoinVisualValues()
        {
            int total = 0;

            for (int i = 0; i < _coins.Count; i++)
                total += Mathf.Max(0, _coins[i].value);

            return total;
        }

        private int GetDefinitionValue(int definitionIndex)
        {
            MoneyChestCoinVisualDefinition def = GetCoinDefinition(definitionIndex);
            return def != null ? Mathf.Max(1, def.value) : 1;
        }

        private MoneyChestCoinVisualDefinition GetCoinDefinition(int definitionIndex)
        {
            if (_settings.coinVisuals == null)
                return null;

            if (definitionIndex < 0 || definitionIndex >= _settings.coinVisuals.Length)
                return null;

            return _settings.coinVisuals[definitionIndex];
        }

        private Sprite GetCoinSprite(int definitionIndex)
        {
            MoneyChestCoinVisualDefinition def = GetCoinDefinition(definitionIndex);
            return def != null ? def.sprite : null;
        }

        private Vector2 GetCoinDrawSize(int definitionIndex)
        {
            Sprite sprite = GetCoinSprite(definitionIndex);

            if (sprite != null)
            {
                Vector2 size = sprite.rect.size * Mathf.Max(0.1f, _settings.coinSpriteScale);
                return new Vector2(
                    Mathf.Max(1f, size.x),
                    Mathf.Max(1f, size.y));
            }

            float fallback = Mathf.Max(1f, _settings.fallbackCoinSizePx);
            return new Vector2(fallback, fallback);
        }

        private int BuildSeed(int balance, int shakeGeneration)
        {
            int seed = _ctx != null ? _ctx.seed : 0;

            unchecked
            {
                seed = seed * 397 ^ balance;
                seed = seed * 397 ^ shakeGeneration;

                string id = _chest != null ? _chest.ChestInstanceId : null;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    for (int i = 0; i < id.Length; i++)
                        seed = seed * 31 + id[i];
                }
            }

            return seed;
        }

        private Rect DrawChestInterior(Rect rect)
        {
            if (_settings.chestInteriorSprite != null)
            {
                Rect fitted = FitRectPreserveAspect(rect, _settings.chestInteriorSprite.rect.size);
                DrawSpriteRaw(fitted, _settings.chestInteriorSprite);
                return fitted;
            }

            Color old = GUI.color;

            GUI.color = new Color(0.30f, 0.17f, 0.08f, 1f);
            GUI.DrawTexture(rect, _white);

            Rect inner = new Rect(
                rect.x + rect.width * 0.05f,
                rect.y + rect.height * 0.08f,
                rect.width * 0.90f,
                rect.height * 0.82f);

            GUI.color = new Color(0.16f, 0.08f, 0.035f, 1f);
            GUI.DrawTexture(inner, _white);

            GUI.color = old;

            return rect;
        }

        private void DrawCoins(Rect coinAreaRect)
        {
            for (int i = 0; i < _coins.Count; i++)
            {
                CoinVisual coin = _coins[i];

                Vector2 size = coin.sizePx;

                float minX = coinAreaRect.xMin + size.x * 0.5f;
                float maxX = coinAreaRect.xMax - size.x * 0.5f;
                float minY = coinAreaRect.yMin + size.y * 0.5f;
                float maxY = coinAreaRect.yMax - size.y * 0.5f;

                if (maxX < minX)
                {
                    float mid = coinAreaRect.center.x;
                    minX = mid;
                    maxX = mid;
                }

                if (maxY < minY)
                {
                    float mid = coinAreaRect.center.y;
                    minY = mid;
                    maxY = mid;
                }

                Vector2 pos = new Vector2(
                    Mathf.Lerp(minX, maxX, coin.normPos01.x),
                    Mathf.Lerp(minY, maxY, coin.normPos01.y));

                Rect r = new Rect(
                    pos.x - size.x * 0.5f,
                    pos.y - size.y * 0.5f,
                    size.x,
                    size.y);

                Sprite sprite = GetCoinSprite(coin.definitionIndex);

                Matrix4x4 oldMatrix = GUI.matrix;
                Color oldColor = GUI.color;

                GUIUtility.RotateAroundPivot(coin.rotationDeg, r.center);

                if (sprite != null)
                {
                    GUI.color = Color.white;
                    DrawSpriteRaw(r, sprite);
                }
                else
                {
                    GUI.color = coin.fallbackColor;
                    GUI.DrawTexture(r, _white);
                }

                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
            }
        }

        private void DrawChestLidAndBorder(Rect rect)
        {
            Color old = GUI.color;

            GUI.color = new Color(0.46f, 0.27f, 0.11f, 0.92f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 18f), _white);

            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 18f, rect.width, 18f), _white);

            GUI.color = new Color(1f, 0.85f, 0.35f, 0.22f);
            DrawRectOutline(rect, 2f);

            GUI.color = old;
        }

        private Rect GetCoinAreaRect(Rect chestSpriteRect)
        {
            Rect n = ClampNormalizedRect(_settings.coinArea01);

            return new Rect(
                chestSpriteRect.x + chestSpriteRect.width * n.x,
                chestSpriteRect.y + chestSpriteRect.height * n.y,
                chestSpriteRect.width * n.width,
                chestSpriteRect.height * n.height);
        }

        private static Rect ClampNormalizedRect(Rect r)
        {
            float x = Mathf.Clamp01(r.x);
            float y = Mathf.Clamp01(r.y);
            float w = Mathf.Clamp01(r.width);
            float h = Mathf.Clamp01(r.height);

            if (x + w > 1f)
                w = 1f - x;

            if (y + h > 1f)
                h = 1f - y;

            return new Rect(x, y, Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        private void DrawSpriteRaw(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return;

            Rect texRect = sprite.textureRect;
            Texture2D tex = sprite.texture;

            Rect uv = new Rect(
                texRect.x / tex.width,
                texRect.y / tex.height,
                texRect.width / tex.width,
                texRect.height / tex.height);

            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }

        private Rect FitRectPreserveAspect(Rect outer, Vector2 sourceSize)
        {
            if (sourceSize.x <= 0f || sourceSize.y <= 0f)
                return outer;

            float sourceAspect = sourceSize.x / sourceSize.y;
            float outerAspect = outer.width / outer.height;

            if (outerAspect > sourceAspect)
            {
                float width = outer.height * sourceAspect;
                float x = outer.x + (outer.width - width) * 0.5f;
                return new Rect(x, outer.y, width, outer.height);
            }
            else
            {
                float height = outer.width / sourceAspect;
                float y = outer.y + (outer.height - height) * 0.5f;
                return new Rect(outer.x, y, outer.width, height);
            }
        }

        private void SortCoinVisuals()
        {
            _coins.Sort((a, b) =>
            {
                int layerCompare = a.layerKey.CompareTo(b.layerKey);
                if (layerCompare != 0)
                    return layerCompare;

                // tiny tie-breakers only
                int yCompare = a.normPos01.y.CompareTo(b.normPos01.y);
                if (yCompare != 0)
                    return yCompare;

                return a.sizePx.y.CompareTo(b.sizePx.y);
            });
        }

        private static Color PickFallbackCoinColor(int value, int definitionIndex)
        {
            if (value >= 500)
                return new Color(0.55f, 0.95f, 1.00f, 1f);

            if (value >= 100)
                return new Color(1.00f, 0.92f, 0.45f, 1f);

            if (value >= 10)
                return new Color(1.00f, 0.78f, 0.22f, 1f);

            if (value >= 5)
                return new Color(0.72f, 0.72f, 0.68f, 1f);

            return new Color(0.72f, 0.41f, 0.18f, 1f);
        }

        private string GetWealthFlavor(int balance)
        {
            if (balance <= 0)
                return "Empty. Tragic, but financially honest.";

            if (balance < 100)
                return _lastBuildWasExact
                    ? "A few coins. They even add up correctly. Miracles happen."
                    : "A few coins. Technically wealth.";

            if (balance < 1000)
                return _lastBuildWasExact
                    ? "A modest little pile. Accounted for, somehow."
                    : "A modest little pile.";

            if (balance < _settings.balanceForFullChest)
                return "A respectable haul.";

            if (balance < _settings.balanceForDisgustingRichChest)
                return "The chest is properly full.";

            return "Disgusting rich-player behavior detected.";
        }

        private static float NextFloat(System.Random rng)
        {
            return (float)rng.NextDouble();
        }

        private void DrawRectOutline(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _white);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _white);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _white);
        }
    }
}