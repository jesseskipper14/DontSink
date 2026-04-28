using System.Linq;
using UnityEngine;

public sealed class CompartmentBoundedSpaceDebugClickTest : MonoBehaviour
{
    [SerializeField] private Transform searchRoot;
    [SerializeField] private float joinEpsilon = 0.06f;

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 click = new Vector2(mouse.x, mouse.y);

        CompartmentBoundaryAuthoring[] boundaries =
            (searchRoot != null ? searchRoot : transform)
            .GetComponentsInChildren<CompartmentBoundaryAuthoring>(true);

        bool ok = CompartmentBoundedSpaceDetector.TryDetectBoundedSpaceAtPoint(
            click,
            boundaries,
            out var result,
            joinEpsilon);

        Debug.Log($"BoundedSpace click={click} ok={ok} result={result}");
    }
}