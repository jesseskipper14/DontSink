using System;
using UnityEngine;

[Serializable]
public struct TimedBuffInstance
{
    public NodeBuff buff;
    public float durationHours;
    public float elapsedHours;

    public int stacks;

    public bool IsExpired => elapsedHours >= durationHours;
    public float RemainingHours => Mathf.Max(0f, durationHours - elapsedHours);

    public TimedBuffInstance(NodeBuff buff, float durationHours, int stacks = 1)
    {
        this.buff = buff;
        this.durationHours = Mathf.Max(0.001f, durationHours);
        this.elapsedHours = 0f;
        this.stacks = Mathf.Max(1, stacks);
    }

    public void Tick(float dtHours)
    {
        elapsedHours += dtHours;
    }

    public float GetAccelThisTick()
    {
        if (buff == null) return 0f;

        float a = buff.accelPerHour * stacks;

        if (!buff.rampInOut) return a;

        float t = Mathf.Clamp01(elapsedHours / durationHours);
        float rf = Mathf.Clamp01(buff.rampFraction);

        // Simple trapezoid ramp: fade in first rf, fade out last rf
        float w = 1f;
        if (rf > 0f)
        {
            if (t < rf) w = t / rf;
            else if (t > 1f - rf) w = (1f - t) / rf;
        }

        return a * Mathf.Clamp01(w);
    }
}
