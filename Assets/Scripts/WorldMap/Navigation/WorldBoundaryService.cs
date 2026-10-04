/// <summary>Read-only seam over current shared navigation. No retained save/scene state.
/// Consequential consumers must apply GameplayAuthority gating themselves.</summary>
public static class WorldBoundaryService
{
    public static bool TryGetCurrent(out WorldBoundarySample sample,
        float softFraction = WorldBoundaryQuery.DefaultSoftFraction,
        float hardFraction = WorldBoundaryQuery.DefaultHardFraction)
    {
        sample = default;
        if (!WorldNavigationService.TryGetTrueWorldPosition(out var position)) return false;
        sample = WorldBoundaryQuery.Evaluate(WorldTopologyService.Current, position, softFraction, hardFraction);
        return sample.IsAvailable;
    }
}
