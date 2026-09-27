/// <summary>
/// Optional policy for an IVolumeContribution that needs runtime control over
/// how much of its authored contribution actually participates in buoyancy and
/// whether that effective volume may bypass the owning body's historical
/// buoyant-acceleration cap.
///
/// VolumeEffectiveness01:
/// 0 = suppress this runtime contribution completely for the current solve.
/// 1 = use the full IVolumeContribution value.
///
/// BodyAccelerationCapBypass01:
/// 0 = keep the effective contribution on the historical capped path.
/// 1 = move the effective contribution to the uncapped center-of-mass path.
/// Values between 0 and 1 split the effective contribution proportionally.
///
/// BodyAccelerationCapScale01:
/// Optional per-body scale for the historical buoyant-acceleration ceiling.
/// 1 = use the normal configured ceiling. 0.5 = half of that ceiling.
/// This only affects the capped path; explicitly bypassed volume is unchanged.
/// </summary>
public interface IBuoyancyVolumePolicy
{
    float VolumeEffectiveness01 { get; }
    float BodyAccelerationCapBypass01 { get; }
    float BodyAccelerationCapScale01 { get; }
}
