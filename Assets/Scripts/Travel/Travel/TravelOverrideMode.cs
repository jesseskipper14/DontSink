public enum TravelOverrideMode { None, AlwaysSucceed, AlwaysFail }

public sealed class TravelOverrideResolver : ITravelResolver
{
    private readonly TravelOverrideMode _mode;
    public TravelOverrideResolver(TravelOverrideMode mode) => _mode = mode;

    public TravelResult Resolve(TravelRequest req, WorldMapSimContext ctx, WorldMapPlayerState player)
    {
        if (_mode == TravelOverrideMode.AlwaysSucceed)
            return new TravelResult(true, "", roll: 0);
        if (_mode == TravelOverrideMode.AlwaysFail)
            return new TravelResult(false, "Debug override: forced failure", roll: 0);

        return default; // Means “no decision”, let chain continue
    }
}
