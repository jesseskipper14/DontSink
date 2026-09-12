using UnityEngine;

[DisallowMultipleComponent]
public sealed class TetherWinchLink : MonoBehaviour
{
    [SerializeField] private Hardpoint ownerWinchHardpoint;
    [SerializeField] private Hardpoint linkedPayloadHardpoint;

    public Hardpoint OwnerWinchHardpoint
    {
        get
        {
            ResolveOwner();
            return ownerWinchHardpoint;
        }
    }

    public Hardpoint LinkedPayloadHardpoint => linkedPayloadHardpoint;
    public Hardpoint LinkedDeploymentHardpoint => linkedPayloadHardpoint;
    public bool HasLinkedPayload => linkedPayloadHardpoint != null;
    public bool HasLinkedDeployment => linkedPayloadHardpoint != null;

    private void Reset()
    {
        ResolveOwner();
    }

    private void Awake()
    {
        ResolveOwner();
    }

    public bool TryLink(Hardpoint payloadHardpoint)
    {
        ResolveOwner();

        if (ownerWinchHardpoint == null || payloadHardpoint == null)
            return false;

        if (!HardpointAccepts(ownerWinchHardpoint, HardpointType.Winch))
            return false;

        if (!HardpointAccepts(payloadHardpoint, HardpointType.TetherPayload) &&
            !HardpointAccepts(payloadHardpoint, HardpointType.Anchor))
        {
            return false;
        }

        Boat winchBoat = ownerWinchHardpoint.GetComponentInParent<Boat>();
        Boat payloadBoat = payloadHardpoint.GetComponentInParent<Boat>();

        if (winchBoat != null && payloadBoat != null && winchBoat != payloadBoat)
            return false;

        linkedPayloadHardpoint = payloadHardpoint;
        return true;
    }

    public void Unlink()
    {
        linkedPayloadHardpoint = null;
    }

    public bool TryGetDeploymentModule(out TetherDeploymentModule deployment)
    {
        deployment = null;

        if (linkedPayloadHardpoint == null)
            return false;

        return linkedPayloadHardpoint.TryGetInstalledModuleComponent(out deployment) &&
               deployment != null;
    }

    private void ResolveOwner()
    {
        if (ownerWinchHardpoint == null)
            ownerWinchHardpoint = GetComponent<Hardpoint>();
    }

    private static bool HardpointAccepts(Hardpoint hardpoint, HardpointType type)
    {
        if (hardpoint == null)
            return false;

        HardpointType[] accepted = hardpoint.GetAcceptedTypes();
        for (int i = 0; i < accepted.Length; i++)
        {
            if (accepted[i] == type)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    public void EditorSetOwner(Hardpoint hardpoint)
    {
        ownerWinchHardpoint = hardpoint;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
