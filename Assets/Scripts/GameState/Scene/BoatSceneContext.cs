using UnityEngine;

public sealed class BoatSceneContext : MonoBehaviour
{
    [Header("Anchors")]
    public Transform playerSpawn;         // where boat spawns (0,0 typically)
    public Transform sourceDockAnchor;    // visual/trigger dock near start (-20,0)
    public Transform targetDockAnchor;    // visual/trigger dock at +distance

    [Header("Dock Triggers")]
    public DockTrigger sourceDockTrigger;
    public DockTrigger targetDockTrigger;
}