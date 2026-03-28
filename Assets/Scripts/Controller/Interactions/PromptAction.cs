using UnityEngine;

public readonly struct PromptAction
{
    public readonly string Text;
    public readonly int Priority;
    public readonly bool ShowProgress;
    public readonly float Progress01;
    public readonly Color? TextColor;
    public readonly bool Pulse;

    public PromptAction(
        string text,
        int priority = 0,
        bool showProgress = false,
        float progress01 = 0f,
        Color? textColor = null,
        bool pulse = false)
    {
        Text = text;
        Priority = priority;
        ShowProgress = showProgress;
        Progress01 = progress01;
        TextColor = textColor;
        Pulse = pulse;
    }
}