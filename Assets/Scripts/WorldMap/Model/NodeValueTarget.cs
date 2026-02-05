public enum NodeValueTargetKind
{
    Stat,
    DockRating,
    TradeRating,
    OptionalBuildingRating
}

[System.Serializable]
public struct NodeValueTarget
{
    public NodeValueTargetKind kind;
    public NodeStatId statId;                  // used if kind == Stat
    public SettlementBuildingId buildingId;     // used if kind == OptionalBuildingRating
}
