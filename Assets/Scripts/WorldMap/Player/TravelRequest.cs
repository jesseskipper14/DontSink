public readonly struct TravelRequest
{
    public readonly string fromNodeId;
    public readonly string toNodeId;
    public readonly float routeLength;
    public readonly int seed; // host-generated for determinism

    public TravelRequest(string from, string to, float length, int seed)
    {
        fromNodeId = from; toNodeId = to; routeLength = length; this.seed = seed;
    }
}

public readonly struct TravelResult
{
    public readonly bool success;
    public readonly string failureReason;
    public readonly int roll; // 0..9999 for debugging

    public TravelResult(bool success, string reason, int roll)
    {
        this.success = success; failureReason = reason; this.roll = roll;
    }
}
