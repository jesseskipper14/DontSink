public enum SimulationLodState
{
    Full = 0,
    Reduced = 1,
    Frozen = 2
}

public enum SimulationLodAuthorityMode
{
    SinglePlayerOrAuthoritative = 0,

    // Future multiplayer clients can use this for renderer/audio/cosmetic-only LOD.
    VisualOnly = 1
}