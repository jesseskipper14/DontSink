using UnityEngine;

public readonly struct ItemChargeDisplayInfo
{
    public readonly bool ShouldShow;
    public readonly bool HasChargeSource;
    public readonly int Current;
    public readonly int Max;
    public readonly string Label;

    public float Normalized =>
        Max <= 0 ? 0f : Mathf.Clamp01((float)Current / Max);

    public bool IsEmpty => ShouldShow && Current <= 0;

    public ItemChargeDisplayInfo(
        bool shouldShow,
        bool hasChargeSource,
        int current,
        int max,
        string label)
    {
        ShouldShow = shouldShow;
        HasChargeSource = hasChargeSource;
        Current = Mathf.Max(0, current);
        Max = Mathf.Max(0, max);
        Label = string.IsNullOrWhiteSpace(label) ? "Charge" : label;
    }

    public static ItemChargeDisplayInfo Hidden =>
        new ItemChargeDisplayInfo(false, false, 0, 0, string.Empty);

    public static ItemChargeDisplayInfo Empty(string label) =>
        new ItemChargeDisplayInfo(true, false, 0, 1, label);

    public static ItemChargeDisplayInfo FromCharges(int current, int max, string label) =>
        new ItemChargeDisplayInfo(true, true, current, max, label);
}