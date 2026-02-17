using UnityEngine;

namespace MiniGames
{
    public interface IOverlayDebugDrawable
    {
        // Normalized overlay space -1..1 (matches cartridge math)
        Vector2 GetTargetNorm01();
        float GetProgress01();
        float GetAverageQuality01();
    }
}
