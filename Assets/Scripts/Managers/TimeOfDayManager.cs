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

    [Header("Calendar Settings")]
    [SerializeField] private int daysPerMonth = 30;
    [SerializeField] private int monthsPerYear = 8;

    // Start date (1-indexed for humans)
    [SerializeField] private int year = 1;
    [SerializeField, Range(1, 8)] private int month = 1;
    [SerializeField, Range(1, 30)] private int day = 1;

    public int Year => year;
    public int Month => month;
    public int Day => day;

    // Total day count since epoch (useful for sims)
    public int DayIndex => (year - 1) * (monthsPerYear * daysPerMonth)
                         + (month - 1) * daysPerMonth
                         + (day - 1);

    public event Action<int, int, int> OnDateChanged; // year, month, day
    public event Action<int> OnDayAdvanced; // new DayIndex

    private void Awake()
    {
        RecalculatePhase(forceNotify: true);
    }

    public void Tick(float deltaTime)
    {
        if (dayLength <= 0f) return;

        float prevTime = currentTime;

        currentTime += (24f / dayLength) * deltaTime;

        int daysAdvanced = 0;
        while (currentTime >= 24f)
        {
            currentTime -= 24f;
            daysAdvanced++;
        }

        if (daysAdvanced > 0)
        {
            AdvanceDays(daysAdvanced);
        }

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

    private void AdvanceDays(int count)
    {
        for (int i = 0; i < count; i++)
        {
            day++;

            if (day > daysPerMonth)
            {
                day = 1;
                month++;

                if (month > monthsPerYear)
                {
                    month = 1;
                    year++;
                }
            }
        }

        OnDayAdvanced?.Invoke(DayIndex);
        OnDateChanged?.Invoke(year, month, day);
    }
}
