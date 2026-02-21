using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    public PhysicsGlobals globals; // assign the ScriptableObject here

    // Optional: expose static shortcuts if you want
    public static PhysicsManager Instance { get; private set; }

    void Awake()
    {
        // Ensure singleton exists if you want global access
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (globals == null)
            Debug.LogError("PhysicsGlobals not assigned in PhysicsGlobalsHolder!");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

}
