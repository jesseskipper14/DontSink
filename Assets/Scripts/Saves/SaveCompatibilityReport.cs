using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class SaveCompatibilityReport
{
    public readonly List<string> Errors = new();
    public readonly List<string> Warnings = new();
    public readonly List<string> Info = new();

    public bool HasErrors => Errors.Count > 0;
    public bool HasWarnings => Warnings.Count > 0;
    public bool IsClean => !HasErrors && !HasWarnings;

    public string Summary =>
        $"errors={Errors.Count}, warnings={Warnings.Count}, info={Info.Count}";

    public void Error(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Errors.Add(message);
    }

    public void Warning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Warnings.Add(message);
    }

    public void AddInfo(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Info.Add(message);
    }

    public void Merge(SaveCompatibilityReport other)
    {
        if (other == null)
            return;

        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
        Info.AddRange(other.Info);
    }

    public string ToConsoleString(string title)
    {
        StringBuilder sb = new();

        sb.AppendLine($"[{title}] Save Compatibility Report | {Summary}");

        if (Errors.Count > 0)
        {
            sb.AppendLine("Errors:");
            for (int i = 0; i < Errors.Count; i++)
                sb.AppendLine($"  ❌ {Errors[i]}");
        }

        if (Warnings.Count > 0)
        {
            sb.AppendLine("Warnings:");
            for (int i = 0; i < Warnings.Count; i++)
                sb.AppendLine($"  ⚠ {Warnings[i]}");
        }

        if (Info.Count > 0)
        {
            sb.AppendLine("Info:");
            for (int i = 0; i < Info.Count; i++)
                sb.AppendLine($"  ℹ {Info[i]}");
        }

        return sb.ToString();
    }

    public void LogUnity(string title, Object context = null)
    {
        string msg = ToConsoleString(title);

        if (HasErrors)
            Debug.LogError(msg, context);
        else if (HasWarnings)
            Debug.LogWarning(msg, context);
        else
            Debug.Log(msg, context);
    }
}