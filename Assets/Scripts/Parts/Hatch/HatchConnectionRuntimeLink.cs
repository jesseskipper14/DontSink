using UnityEngine;

[DisallowMultipleComponent]
public sealed class HatchConnectionRuntimeLink : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private HatchRuntime hatchRuntime;

    [Tooltip("Usually the hatch root transform. This must match CompartmentConnection.transform.")]
    [SerializeField] private Transform connectionTransform;

    [Header("Debug")]
    [SerializeField] private bool logWarnings = true;
    [SerializeField] private bool logStateChanges = false;

    private CompartmentConnection _connection;

    private void Reset()
    {
        boat = GetComponentInParent<Boat>();
        hatchRuntime = GetComponent<HatchRuntime>();
        connectionTransform = transform;
    }

    private void Awake()
    {
        ResolveRefs();
        ResolveConnection();
        SyncConnectionState();
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (hatchRuntime != null)
            hatchRuntime.StateChanged += HandleHatchStateChanged;

        ResolveConnection();
        SyncConnectionState();
    }

    private void OnDisable()
    {
        if (hatchRuntime != null)
            hatchRuntime.StateChanged -= HandleHatchStateChanged;
    }

    private void HandleHatchStateChanged(bool isOpen)
    {
        if (_connection == null)
            ResolveConnection();

        if (_connection == null)
            return;

        _connection.isOpen = isOpen;

        if (logStateChanges)
        {
            Debug.Log(
                $"[HatchConnectionRuntimeLink] '{name}' set connection.isOpen={isOpen}.",
                this);
        }
    }

    public void SyncConnectionState()
    {
        if (hatchRuntime == null)
            return;

        if (_connection == null)
            ResolveConnection();

        if (_connection == null)
            return;

        _connection.isOpen = hatchRuntime.IsOpen;
    }

    private void ResolveRefs()
    {
        if (boat == null)
            boat = GetComponentInParent<Boat>();

        if (hatchRuntime == null)
            hatchRuntime = GetComponent<HatchRuntime>();

        if (connectionTransform == null)
            connectionTransform = transform;
    }

    private void ResolveConnection()
    {
        _connection = null;

        if (boat == null)
        {
            if (logWarnings)
                Debug.LogWarning("[HatchConnectionRuntimeLink] No Boat found in parents.", this);
            return;
        }

        if (connectionTransform == null)
        {
            if (logWarnings)
                Debug.LogWarning("[HatchConnectionRuntimeLink] No connectionTransform assigned.", this);
            return;
        }

        if (boat.Connections == null)
        {
            if (logWarnings)
                Debug.LogWarning("[HatchConnectionRuntimeLink] Boat.Connections is null.", this);
            return;
        }

        foreach (var conn in boat.Connections)
        {
            if (conn == null)
                continue;

            if (conn.transform == connectionTransform)
            {
                _connection = conn;
                return;
            }
        }

        if (logWarnings)
        {
            Debug.LogWarning(
                $"[HatchConnectionRuntimeLink] No CompartmentConnection found on boat '{boat.name}' " +
                $"for transform '{connectionTransform.name}'. Did you run Resolve + Apply Connection?",
                this);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Resolve Connection")]
    private void EditorResolveConnection()
    {
        ResolveRefs();
        ResolveConnection();
        SyncConnectionState();

        if (_connection != null)
            Debug.Log($"[HatchConnectionRuntimeLink] Resolved connection for '{name}'.", this);
    }
#endif
}