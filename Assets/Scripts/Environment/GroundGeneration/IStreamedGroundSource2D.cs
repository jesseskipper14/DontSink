public interface IStreamedGroundSource2D
{
    bool IsReady { get; }
    bool TryGetWorldSpan(out float minX, out float maxX);
    bool TrySampleGround(float worldX, out float groundY, out float slopeDegrees);
    bool EnsureCoverage(float worldX, float radius);
}
