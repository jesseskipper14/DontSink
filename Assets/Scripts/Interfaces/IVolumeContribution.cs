public interface IVolumeContribution
{
    /// <summary>
    /// Additional displacement volume contributed to the owning ForceBody2D.
    /// Use zero when an object's physical volume is not meaningful to gameplay.
    /// </summary>
    float VolumeContribution { get; }
}
