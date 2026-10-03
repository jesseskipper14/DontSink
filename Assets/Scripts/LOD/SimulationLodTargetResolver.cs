using UnityEngine;

public static class SimulationLodTargetResolver
{
    public static Transform ResolveDefaultTarget()
    {
        // This compatibility resolver is not an authority/streaming policy.
        // Hosts with several players must supply relevant actor targets explicitly.
        var players = Object.FindObjectsByType<CharacterPlayer>(FindObjectsSortMode.None);
        return players.Length == 1 ? players[0].transform : null;
    }
}
