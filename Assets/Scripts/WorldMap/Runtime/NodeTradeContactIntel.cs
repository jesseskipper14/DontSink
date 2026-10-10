/// <summary>
/// Future abstract-trade adapter. Observe on authority when information is collected;
/// deliver that frozen observation only after the caller resolves a real trade contact.
/// This class neither schedules contacts nor changes trade/economic state.
/// </summary>
public static class NodeTradeContactIntel
{
    public static bool TryObserve(string contactId, string sendingNodeId, string receivingNodeId,
        double observedAtWorldHours, out AdjacentNodeIntelSnapshot observation, out string reason)
    {
        observation = null;
        reason = "Trade-contact reports require the authoritative host and a stable contact ID.";
        if (!GameplayAuthority.IsAuthoritative || string.IsNullOrWhiteSpace(contactId)) return false;
        observation = NodeAdjacentIntelService.CaptureObservation(receivingNodeId, sendingNodeId,
            observedAtWorldHours, "Trade contact report");
        if (observation == null) { reason = "Contact settlements, source state or observation time are unavailable."; return false; }
        observation.contactId = contactId;
        reason = "Observation captured; not yet delivered.";
        return true;
    }

    public static bool TryDeliver(string contactId, string sendingNodeId, string receivingNodeId,
        AdjacentNodeIntelSnapshot observation, double receivedAtWorldHours, out string reason)
    {
        reason = "Trade-contact delivery requires the authoritative host.";
        if (!GameplayAuthority.IsAuthoritative) return false;
        if (observation == null || string.IsNullOrWhiteSpace(contactId) || observation.contactId != contactId ||
            observation.sourceNodeId != receivingNodeId || observation.observedNodeId != sendingNodeId)
        { reason = "Observation does not belong to this trade contact."; return false; }
        var receipt = observation.Copy();
        receipt.receivedAtWorldHours = receivedAtWorldHours;
        // Never read the sender's present state at delivery: travel delay is real staleness.
        return NodeAdjacentIntelService.TryReceive(receipt, out reason);
    }
}
