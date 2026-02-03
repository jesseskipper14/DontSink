using UnityEngine;
using System;

public class TimeOfDayManager : MonoBehaviour, ITimeOfDayService
{
    [Header("Time Settings")]
    [SerializeField] private float dayLength = 120f;
    [SerializeField, Range(0f, 24f)] private float currentTime = 12f;
    [SerializeField] private DayPhaseConfig phaseConfig;

    public float CurrentTime => currentTime;
    public float NormalizedTime => currentTime / 24f;
    public float DayLength => dayLength;
    public DayPhase CurrentPhase { get; private set; }

    public event Action<float> OnTimeChanged;
    public event Action<DayPhase> OnDayPhaseChanged;

    private void Awake()
    {
        RecalculatePhase(forceNotify: true);
    }

    public void Tick(float deltaTime)
    {
        if (dayLength <= 0f) return;

        float prevTime = currentTime;

        currentTime += (24f / dayLength) * deltaTime;
        if (currentTime >= 24f)
            currentTime -= 24f;

        if (!Mathf.Approximately(prevTime, currentTime))
            OnTimeChanged?.Invoke(currentTime);

        RecalculatePhase();
    }

    public void SetTime(float hour)
    {
        currentTime = Mathf.Repeat(hour, 24f);
        OnTimeChanged?.Invoke(currentTime);
        RecalculatePhase(forceNotify: true);
    }

    private void RecalculatePhase(bool forceNotify = false)
    {
        if (phaseConfig == null) return;

        DayPhase newPhase = phaseConfig.GetPhase(currentTime);

        if (forceNotify || newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            OnDayPhaseChanged?.Invoke(CurrentPhase);
        }
    }
}
