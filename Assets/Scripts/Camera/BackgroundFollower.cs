using UnityEngine;

public class BackgroundFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public bool lockY = true;

    [Range(0f, 1f)]
    public float celestialScale = 0.1f;

    private EnvironmentManager env;

    void Start()
    {
        env = GameObject
            .Find("EnvironmentManager")
            ?.GetComponent<EnvironmentManager>();
    }

    void LateUpdate()
    {
        Vector3 pos = target.position + offset;
        if (lockY) pos.y = offset.y;
        transform.position = pos;

        CelestialBodyManager celestial = EnvironmentManager.Instance?.CelestialBodyManager;
        if (celestial != null)
        {
            celestial.SetHorizontalOffset(transform.position.x * celestialScale);
        }
    }

}
