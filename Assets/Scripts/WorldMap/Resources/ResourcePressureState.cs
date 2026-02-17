using System;

[Serializable]
public struct ResourcePressureState
{
    public float value;      // current pressure (-4..+4)
    public float baseline;   // archetype baseline
    public float driftRate;  // how strongly it returns to baseline per day

    public void Tick(float dt)
    {
        // Move toward baseline
        float delta = baseline - value;
        value += delta * driftRate * dt;

        value = UnityEngine.Mathf.Clamp(value, -4f, 4f);
    }
}
