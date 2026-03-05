using UnityEngine;

[DisallowMultipleComponent]
public sealed class NodeWaterBottomBinder : MonoBehaviour
{
    [SerializeField] private NodeGroundSpriteShapeBinder ground;
    [SerializeField] private WaterMeshRenderer water; // your script in screenshot
    [SerializeField] private float extraDepth = 0f;   // if you want water slightly deeper than land fill

    private void Awake()
    {
        if (!ground) ground = FindAnyObjectByType<NodeGroundSpriteShapeBinder>();
        if (!water) water = FindAnyObjectByType<WaterMeshRenderer>();
    }

    private void Start()
    {
        Apply();
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        if (!ground || !water) return;

        // This should match exactly, so no seams.
        water.bottomY = ground.LastUsedBottomY - extraDepth;
    }
}