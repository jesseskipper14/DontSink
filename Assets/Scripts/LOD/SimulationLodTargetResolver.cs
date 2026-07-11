using UnityEngine;

public static class SimulationLodTargetResolver
{
    public static Transform ResolveDefaultTarget()
    {
        CharacterPlayer player = Object.FindFirstObjectByType<CharacterPlayer>();
        if (player != null)
            return player.transform;

        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform;

        return null;
    }
}