public enum TetherDeploymentState
{
    Stowed = 0,
    Deploying = 1,
    Suspended = 2,
    Bottomed = 3,
    Holding = 4,
    Retrieving = 5,
    Docking = 6,
    CutLoose = 7
}

public enum WinchCommand
{
    Stop = 0,
    Lower = 1,
    Raise = 2,
    QuickRelease = 3
}
