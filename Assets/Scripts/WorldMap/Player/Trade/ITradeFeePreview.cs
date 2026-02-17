using System.Collections.Generic;
using WorldMap.Player.Trade;

public interface ITradeFeePreview
{
    /// Returns total fee (credits) for the given draft lines at this node/time.
    int ComputeFeeTotal(MapNodeState nodeState, IReadOnlyList<TradeLine> lines, int timeBucket);

    /// Optional: used for UI explanations (rate/mins etc). You can skip this if you don’t care.
    string DebugDescribe(MapNodeState nodeState);
}
