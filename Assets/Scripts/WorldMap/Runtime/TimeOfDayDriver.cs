using UnityEngine;

public class TimeOfDayDriver : MonoBehaviour
{
    [SerializeField] private TimeOfDayManager timeOfDay;

    [Min(0f)] public float timeScale = 1f; // 1 = normal, 10 = fast

    private void Reset()
    {
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
    }

    private void Awake()
    {
        if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
    }

    private void Update()
    {
        if (timeOfDay == null) return;
        timeOfDay.Tick(Time.deltaTime * timeScale);
    }
}
