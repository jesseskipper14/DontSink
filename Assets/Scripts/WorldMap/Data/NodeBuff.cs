using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Node Buff")]
public class NodeBuff : ScriptableObject
{
    public string buffId;              // stable key for saves/mods
    public string displayName;

    [Tooltip("What value this buff influences.")]
    public NodeValueTarget target;

    [Tooltip("Acceleration applied per game-hour. Positive pushes up, negative pushes down.")]
    public float accelPerHour = 0.05f;

    [Header("Stacking")]
    public bool stacks = false;
    public float maxStacks = 3;

    [Header("Shaping")]
    public bool rampInOut = true;
    [Range(0f, 0.5f)] public float rampFraction = 0.2f; // 20% of duration ramps in/out
}
