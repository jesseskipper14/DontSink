using UnityEngine;

[CreateAssetMenu(menuName = "Environment/Day Phase Config")]
public class DayPhaseConfig : ScriptableObject
{
    public float dawnStart = 5f;
    public float dayStart = 7f;
    public float duskStart = 18f;
    public float nightStart = 20f;

    public DayPhase GetPhase(float time)
    {
        if (time >= nightStart || time < dawnStart)
            return DayPhase.Night;
        if (time >= dawnStart && time < dayStart)
            return DayPhase.Dawn;
        if (time >= dayStart && time < duskStart)
            return DayPhase.Day;
        return DayPhase.Dusk;
    }
}
