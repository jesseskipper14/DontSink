using System;
using UnityEngine;

[Serializable]
public sealed class WorldGenerationProgress
{
    public bool isRunning;
    public bool cancelRequested;
    public WorldGenerationPhase phase = WorldGenerationPhase.Idle;
    [Range(0f, 1f)] public float progress01;
    public string statusText = "Idle";
    public float elapsedSeconds;

    private float _startTime;

    public void Begin(WorldGenerationPhase nextPhase, string status)
    {
        isRunning = true;
        cancelRequested = false;
        phase = nextPhase;
        progress01 = 0f;
        statusText = status;
        elapsedSeconds = 0f;
        _startTime = Time.realtimeSinceStartup;
    }

    public void Update(float progress, string status = null)
    {
        progress01 = Mathf.Clamp01(progress);

        if (!string.IsNullOrWhiteSpace(status))
            statusText = status;

        elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _startTime);
    }

    public void Complete(string status = "Complete")
    {
        isRunning = false;
        cancelRequested = false;
        phase = WorldGenerationPhase.Complete;
        progress01 = 1f;
        statusText = status;
        elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _startTime);
    }

    public void Cancel(string status = "Cancelled")
    {
        isRunning = false;
        cancelRequested = false;
        phase = WorldGenerationPhase.Cancelled;
        progress01 = 0f;
        statusText = status;
        elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _startTime);
    }

    public void Fail(string status)
    {
        isRunning = false;
        cancelRequested = false;
        phase = WorldGenerationPhase.Failed;
        progress01 = 0f;
        statusText = status;
        elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _startTime);
    }

    public void RequestCancel()
    {
        if (isRunning)
            cancelRequested = true;
    }
}
