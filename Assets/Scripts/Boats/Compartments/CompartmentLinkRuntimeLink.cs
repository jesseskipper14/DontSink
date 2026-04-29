using UnityEngine;

[DisallowMultipleComponent]
public sealed class CompartmentLinkRuntimeLink : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private HatchRuntime hatchRuntime;
    [SerializeField] private CompartmentLinkAuthoring linkAuthoring;

    [Header("Debug")]
    [SerializeField] private bool logWarnings = true;
    [SerializeField] private bool logStateChanges = false;

    private void Reset()
    {
        boat = GetComponentInParent<Boat>();
        hatchRuntime = GetComponent<HatchRuntime>();
        linkAuthoring = GetComponent<CompartmentLinkAuthoring>();
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (hatchRuntime != null)
            hatchRuntime.StateChanged += HandleHatchStateChanged;

        SyncState();
    }

    private void OnDisable()
    {
        if (hatchRuntime != null)
            hatchRuntime.StateChanged -= HandleHatchStateChanged;
    }

    private void HandleHatchStateChanged(bool isOpen)
    {
        SyncState();
    }

    public void SyncState()
    {
        ResolveRefs();

        if (boat == null || linkAuthoring == null)
        {
            if (logWarnings)
                Debug.LogWarning("[CompartmentLinkRuntimeLink] Missing boat or link authoring.", this);
            return;
        }

        bool isOpen = linkAuthoring.IsCurrentlyOpen;

        // Sync internal generated connections by transform identity.
        if (boat.Connections != null)
        {
            foreach (var conn in boat.Connections)
            {
                if (conn == null)
                    continue;

                if (conn.transform == linkAuthoring.transform)
                    conn.isOpen = isOpen;
            }
        }

        // Sync generated external sources by generated source name.
        string generatedName = CompartmentLinkAuthoring.GetGeneratedExternalSourceName(linkAuthoring.LinkId);

        if (boat.Compartments != null)
        {
            foreach (var comp in boat.Compartments)
            {
                if (comp == null || comp.externalWaterSources == null)
                    continue;

                foreach (var src in comp.externalWaterSources)
                {
                    if (src == null)
                        continue;

                    if (src.name == generatedName)
                        src.IsActive = isOpen;
                }
            }
        }

        if (logStateChanges)
        {
            Debug.Log(
                $"[CompartmentLinkRuntimeLink] '{name}' synced generated topology open={isOpen}.",
                this);
        }
    }

    private void ResolveRefs()
    {
        if (boat == null)
            boat = GetComponentInParent<Boat>();

        if (hatchRuntime == null)
            hatchRuntime = GetComponent<HatchRuntime>();

        if (linkAuthoring == null)
            linkAuthoring = GetComponent<CompartmentLinkAuthoring>();
    }

#if UNITY_EDITOR
    [ContextMenu("Sync State")]
    private void EditorSyncState()
    {
        SyncState();
    }
#endif
}