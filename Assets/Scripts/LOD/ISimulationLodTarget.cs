public interface ISimulationLodTarget
{
    SimulationLodState CurrentLodState { get; }

    void SetSimulationLodState(
        SimulationLodState state,
        SimulationLodAuthorityMode authorityMode);
}