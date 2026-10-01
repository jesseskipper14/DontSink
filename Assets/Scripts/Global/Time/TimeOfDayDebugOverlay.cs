using UnityEngine;

/// <summary>
/// Persistent developer overlay for directly inspecting and editing the
/// authoritative time-of-day/calendar state. Intended to live on ServiceRoot.
/// </summary>
public sealed class TimeOfDayDebugOverlay : MonoBehaviour
{
    [Header("Toggle")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F5;
    [SerializeField] private bool startOpen;

    [Header("Window")]
    [SerializeField] private Rect windowRect = new Rect(20f, 80f, 380f, 310f);
    [SerializeField] private int windowId = 92751;

    private bool isOpen;
    private string dayText = "1";
    private string monthText = "1";
    private string yearText = "1";
    private string statusText = string.Empty;

    private TimeOfDayManager manager;

    private void Awake()
    {
        isOpen = startOpen;
        ResolveManager();
        RefreshDateFields();
    }

    private void OnEnable()
    {
        ResolveManager();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isOpen = !isOpen;
            statusText = string.Empty;

            if (isOpen)
            {
                ResolveManager();
                RefreshDateFields();
            }
        }
    }

    private void OnGUI()
    {
        if (!isOpen)
            return;

        ResolveManager();
        windowRect = GUI.Window(windowId, windowRect, DrawWindow, "Time / Calendar Debug");
    }

    private void DrawWindow(int id)
    {
        if (manager == null)
        {
            GUILayout.Label("No TimeOfDayManager is currently available.");
            if (GUILayout.Button("Retry"))
                ResolveManager();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            return;
        }

        GUILayout.Label($"Current: {FormatClock(manager.CurrentTime)}   Phase: {manager.CurrentPhase}");
        GUILayout.Label($"Date: Day {manager.Day}, Month {manager.Month}, Year {manager.Year}");

        GUILayout.Space(8f);
        GUILayout.Label("Time");

        float before = manager.CurrentTime;
        float after = GUILayout.HorizontalSlider(before, 0f, 23.999f);
        if (!Mathf.Approximately(before, after))
        {
            manager.SetTime(after);
            statusText = string.Empty;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("00:00", GUILayout.Width(45f));
        GUILayout.FlexibleSpace();
        GUILayout.Label("06:00");
        GUILayout.FlexibleSpace();
        GUILayout.Label("12:00");
        GUILayout.FlexibleSpace();
        GUILayout.Label("18:00");
        GUILayout.FlexibleSpace();
        GUILayout.Label("24:00", GUILayout.Width(45f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Midnight")) manager.SetTime(0f);
        if (GUILayout.Button("06:00")) manager.SetTime(6f);
        if (GUILayout.Button("Noon")) manager.SetTime(12f);
        if (GUILayout.Button("18:00")) manager.SetTime(18f);
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.Label("Calendar");

        DrawIntegerField("Day", ref dayText);
        DrawIntegerField("Month", ref monthText);
        DrawIntegerField("Year", ref yearText);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Date"))
            ApplyDate();

        if (GUILayout.Button("Refresh"))
        {
            RefreshDateFields();
            statusText = string.Empty;
        }
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(statusText))
            GUILayout.Label(statusText);

        GUILayout.FlexibleSpace();
        GUILayout.Label($"{toggleKey}: close overlay");

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private static void DrawIntegerField(string label, ref string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(60f));
        value = GUILayout.TextField(value, GUILayout.Width(100f));
        GUILayout.EndHorizontal();
    }

    private void ApplyDate()
    {
        if (!int.TryParse(dayText, out int requestedDay) ||
            !int.TryParse(monthText, out int requestedMonth) ||
            !int.TryParse(yearText, out int requestedYear))
        {
            statusText = "Day, month, and year must be whole numbers.";
            return;
        }

        TimeOfDaySnapshot snapshot = manager.CaptureSnapshot();
        snapshot.day = requestedDay;
        snapshot.month = requestedMonth;
        snapshot.year = requestedYear;

        manager.ApplySnapshot(snapshot, forceNotify: true);
        RefreshDateFields();
        statusText = "Date applied. Values are clamped to the configured calendar.";
    }

    private void RefreshDateFields()
    {
        if (manager == null)
            return;

        dayText = manager.Day.ToString();
        monthText = manager.Month.ToString();
        yearText = manager.Year.ToString();
    }

    private void ResolveManager()
    {
        if (ServiceRoot.Instance != null && ServiceRoot.Instance.TimeManager != null)
        {
            manager = ServiceRoot.Instance.TimeManager;
            return;
        }

        if (manager == null)
            manager = FindAnyObjectByType<TimeOfDayManager>();
    }

    private static string FormatClock(float hour)
    {
        int totalMinutes = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) * 60f + 0.5f);
        totalMinutes %= 24 * 60;

        int h = totalMinutes / 60;
        int m = totalMinutes % 60;
        return $"{h:00}:{m:00}";
    }
}
