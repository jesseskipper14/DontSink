using UnityEngine;
using UnityEngine.UI;

public class TimeDateHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TimeOfDayManager timeOfDay;
    [SerializeField] private Text label;

    [Header("Format")]
    public bool showTimeOfDay = false; // optional
    public string prefix = "";

    private void Reset()
    {
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        label = GetComponent<Text>();
    }

    private void Awake()
    {
        if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
    }

    private void Update()
    {
        if (timeOfDay == null || label == null) return;

        // Requires TimeOfDayManager exposing Year/Month/Day
        int y = timeOfDay.Year;
        int m = timeOfDay.Month;
        int d = timeOfDay.Day;

        if (showTimeOfDay)
        {
            // hour as 0..24
            float h = timeOfDay.CurrentTime;
            label.text = $"{prefix}{m:00}/{d:00}/{y:0000}  {h:00}h";
        }
        else
        {
            label.text = $"{prefix}{m:00}/{d:00}/{y:0000}";
        }
    }
}
