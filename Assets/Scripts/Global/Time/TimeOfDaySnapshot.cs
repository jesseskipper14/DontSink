using System;
using UnityEngine;

[Serializable]
public sealed class TimeOfDaySnapshot
{
    public bool isValid;
    [Range(0f, 24f)] public float currentTime = 12f;
    public int year = 1;
    public int month = 1;
    public int day = 1;

    public TimeOfDaySnapshot Clone()
    {
        return new TimeOfDaySnapshot
        {
            isValid = isValid,
            currentTime = currentTime,
            year = year,
            month = month,
            day = day
        };
    }
}
