using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatPowerState : MonoBehaviour
{
    [Header("Power")]
    [SerializeField] private float currentPower;
    [SerializeField] private float maxPower = 100f;

    public float CurrentPower => currentPower;
    public float MaxPower => Mathf.Max(0f, maxPower);

    public float Normalized => MaxPower > 0f
        ? Mathf.Clamp01(currentPower / MaxPower)
        : 0f;

    public float CurrentDemandPerSecond => CalculateCurrentDemandPerSecond();

    public void AddPower(float amount)
    {
        if (amount <= 0f) return;
        currentPower = Mathf.Clamp(currentPower + amount, 0f, MaxPower);
    }

    public bool TryConsume(float amount)
    {
        if (amount <= 0f) return true;
        if (currentPower < amount) return false;

        currentPower -= amount;
        return true;
    }

    public void SetMaxPower(float value, bool clampCurrent = true)
    {
        maxPower = Mathf.Max(0f, value);

        if (clampCurrent)
            currentPower = Mathf.Clamp(currentPower, 0f, MaxPower);
    }

    public void SetCurrentPower(float value)
    {
        currentPower = Mathf.Clamp(value, 0f, MaxPower);
    }

    private float CalculateCurrentDemandPerSecond()
    {
        float total = 0f;

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPowerConsumerModule consumer && consumer.IsConsumingPower)
                total += Mathf.Max(0f, consumer.PowerDemandPerSecond);
        }

        return total;
    }
}