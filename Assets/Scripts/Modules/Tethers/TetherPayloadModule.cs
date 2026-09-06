using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InstalledModule))]
[RequireComponent(typeof(Rigidbody2D))]
public class TetherPayloadModule : MonoBehaviour, IInstalledModuleLifecycle, IInstalledModuleRemovalGuard
{
    [Header("Tether")]
    [SerializeField] private Transform tetherAnchor;

    [Header("Runtime")]
    [SerializeField] private TetherDeploymentState state = TetherDeploymentState.Stowed;

    private InstalledModule _installedModule;
    private Rigidbody2D _rb;
    private Hardpoint _ownerHardpoint;

    public event Action<TetherPayloadModule, TetherDeploymentState> StateChanged;

    public TetherDeploymentState State => state;
    public bool IsStowed => state == TetherDeploymentState.Stowed;
    public bool IsCutLoose => state == TetherDeploymentState.CutLoose;
    public Transform TetherAnchor => tetherAnchor != null ? tetherAnchor : transform;
    public Rigidbody2D Rigidbody => _rb;
    public Hardpoint OwnerHardpoint => _ownerHardpoint;

    public float DistanceToStowPose
    {
        get
        {
            ResolveOwnerHardpoint();
            if (_ownerHardpoint == null)
                return float.PositiveInfinity;

            Transform targetAnchor = _ownerHardpoint.ModuleAnchor;
            if (targetAnchor == null)
                return float.PositiveInfinity;

            Transform moduleAnchor = ResolveInstalledModuleAnchor();
            if (moduleAnchor == null)
                return Vector2.Distance(transform.position, targetAnchor.position);

            return Vector2.Distance(moduleAnchor.position, targetAnchor.position);
        }
    }

    protected virtual void Awake()
    {
        CacheRefs();
    }

    public virtual void OnInstalled(Hardpoint hardpoint)
    {
        CacheRefs();
        _ownerHardpoint = hardpoint;
        SetState(TetherDeploymentState.Stowed);
    }

    public virtual void OnRemoved()
    {
    }

    public virtual bool BeginDeploy()
    {
        CacheRefs();
        ResolveOwnerHardpoint();

        if (_installedModule == null || _ownerHardpoint == null || _rb == null)
            return false;

        if (!IsStowed)
            return true;

        _installedModule.DetachMassContributionFromBoat();

        transform.SetParent(null, true);

        _rb.simulated = true;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.WakeUp();

        SetState(TetherDeploymentState.Deploying);
        return true;
    }

    public virtual void MarkSuspended()
    {
        if (!IsStowed && !IsCutLoose)
            SetState(TetherDeploymentState.Suspended);
    }

    public virtual void BeginRetrieving()
    {
        if (!IsStowed && !IsCutLoose)
            SetState(TetherDeploymentState.Retrieving);
    }

    public virtual void BeginDocking()
    {
        if (!IsStowed && !IsCutLoose)
            SetState(TetherDeploymentState.Docking);
    }

    public virtual bool TryStow()
    {
        CacheRefs();
        ResolveOwnerHardpoint();

        if (_ownerHardpoint == null || _installedModule == null || _rb == null)
            return false;

        if (IsCutLoose)
            return false;

        Transform mount = _ownerHardpoint.MountPoint;
        if (mount == null)
            return false;

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.simulated = false;

        transform.SetParent(mount, true);
        transform.rotation = mount.rotation;
        transform.position = mount.position;

        AlignToHardpointModuleAnchor();

        ModuleDefinition definition = _installedModule.Definition;
        _installedModule.Initialize(definition, _ownerHardpoint);

        SetState(TetherDeploymentState.Stowed);
        return true;
    }

    public virtual void CutLoose()
    {
        if (IsStowed)
            return;

        SetState(TetherDeploymentState.CutLoose);
    }

    public bool CanRemoveInstalledModule(out string reason)
    {
        if (!IsStowed)
        {
            reason = $"Tether payload is {state}. Retrieve/stow it before removing the module.";
            return false;
        }

        reason = null;
        return true;
    }

    protected void SetPayloadState(TetherDeploymentState next)
    {
        SetState(next);
    }

    protected virtual void SetState(TetherDeploymentState next)
    {
        if (state == next)
            return;

        state = next;
        StateChanged?.Invoke(this, state);
    }

    private void CacheRefs()
    {
        if (_installedModule == null)
            _installedModule = GetComponent<InstalledModule>();

        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        ResolveOwnerHardpoint();
    }

    private void ResolveOwnerHardpoint()
    {
        if (_ownerHardpoint == null && _installedModule != null)
            _ownerHardpoint = _installedModule.OwnerHardpoint;
    }

    private void AlignToHardpointModuleAnchor()
    {
        if (_ownerHardpoint == null)
            return;

        Transform hardpointAnchor = _ownerHardpoint.ModuleAnchor;
        if (hardpointAnchor == null)
            return;

        Transform moduleAnchor = ResolveInstalledModuleAnchor();
        if (moduleAnchor == null)
            return;

        Vector3 delta = hardpointAnchor.position - moduleAnchor.position;
        transform.position += delta;
    }

    private Transform ResolveInstalledModuleAnchor()
    {
        InstalledModuleAnchor installedAnchor = GetComponent<InstalledModuleAnchor>();
        if (installedAnchor == null)
            installedAnchor = GetComponentInChildren<InstalledModuleAnchor>(true);

        return installedAnchor != null
            ? installedAnchor.ModuleAnchor
            : transform.Find("ModuleAnchor");
    }
}
