using UnityEngine;

/// <summary>
/// Generic state-application seam for physical things that can be cut.
/// Request validation (authority, range, tool capability) intentionally lives
/// outside the target so ropes, nets, restraints, lift-bag tethers, etc. can
/// share the same cutting path.
/// </summary>
public interface ICuttable
{
    bool IsCuttable { get; }
    Vector2 CutWorldPosition { get; }

    /// <summary>
    /// Applies the severed/cut state. Callers that represent player intent
    /// should go through CuttingAuthority first.
    /// </summary>
    void ApplyCut();
}
