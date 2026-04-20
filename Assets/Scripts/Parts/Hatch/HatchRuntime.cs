using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HatchRuntime : MonoBehaviour
{
    [Header("Authoring")]
    [SerializeField] private HatchAuthoring authoring;

    [Header("Runtime State")]
    [SerializeField] private bool isOpen;

    public event Action<bool> StateChanged;

    public bool IsOpen => isOpen;
    public HatchAuthoring Authoring => authoring;

    private void Reset()
    {
        if (authoring == null)
            authoring = GetComponent<HatchAuthoring>();
    }

    private void Awake()
    {
        if (authoring == null)
            authoring = GetComponent<HatchAuthoring>();

        if (authoring == null)
        {
            Debug.LogError("[HatchRuntime] Missing HatchAuthoring.", this);
            enabled = false;
            return;
        }

        isOpen = authoring.StartsOpen;
        RefreshPresentation();
    }

    public bool CanToggle(out string reason)
    {
        reason = null;

        if (authoring == null)
        {
            reason = "Missing hatch authoring.";
            return false;
        }

        return true;
    }

    public bool SetOpen(bool open)
    {
        if (isOpen == open)
            return false;

        isOpen = open;
        RefreshPresentation();
        StateChanged?.Invoke(isOpen);
        return true;
    }

    public bool Toggle()
    {
        if (!CanToggle(out _))
            return false;

        return SetOpen(!isOpen);
    }

    public void RefreshPresentation()
    {
        if (authoring == null)
            return;

        if (authoring.FrameRenderer != null)
            authoring.FrameRenderer.enabled = true;

        if (authoring.ClosedRenderer != null)
            authoring.ClosedRenderer.enabled = !isOpen;

        if (authoring.OpenRenderer != null)
            authoring.OpenRenderer.enabled = isOpen;

        if (authoring.BlockingCollider != null)
            authoring.BlockingCollider.enabled = !isOpen;
    }

    public void ForceNotifyState()
    {
        RefreshPresentation();
        StateChanged?.Invoke(isOpen);
    }

#if UNITY_EDITOR
    [ContextMenu("Open Hatch")]
    private void EditorOpen()
    {
        isOpen = true;
        RefreshPresentation();
    }

    [ContextMenu("Close Hatch")]
    private void EditorClose()
    {
        isOpen = false;
        RefreshPresentation();
    }

    [ContextMenu("Refresh Hatch Presentation")]
    private void EditorRefresh()
    {
        RefreshPresentation();
    }

    private void OnValidate()
    {
        if (authoring == null)
            authoring = GetComponent<HatchAuthoring>();

        if (!Application.isPlaying)
            RefreshPresentation();
    }

#endif
}