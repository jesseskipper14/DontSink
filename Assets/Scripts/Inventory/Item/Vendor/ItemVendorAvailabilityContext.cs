public readonly struct ItemVendorAvailabilityContext
{
    public readonly bool HasNodeContext;
    public readonly string NodeId;
    public readonly string NodeArchetypeId;

    public readonly int Prosperity;
    public readonly int TradeRating;
    public readonly int Security;
    public readonly int Stability;

    public ItemVendorAvailabilityContext(
        string nodeId,
        string nodeArchetypeId,
        int prosperity,
        int tradeRating,
        int security,
        int stability)
    {
        HasNodeContext = true;
        NodeId = nodeId;
        NodeArchetypeId = nodeArchetypeId;
        Prosperity = prosperity;
        TradeRating = tradeRating;
        Security = security;
        Stability = stability;
    }

    public static ItemVendorAvailabilityContext Unknown => default;
}