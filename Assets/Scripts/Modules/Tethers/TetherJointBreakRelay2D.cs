using UnityEngine;

/// <summary>
/// Lives on the payload Rigidbody GameObject so Unity's OnJointBreak2D message
/// can be forwarded to the TetherConstraint2D that created the runtime
/// DistanceJoint2D.
///
/// A payload can have other breakable joints (for example an anchor's seabed
/// latch), so the relay only forwards the exact watched joint.
/// </summary>
[DisallowMultipleComponent]
public sealed class TetherJointBreakRelay2D : MonoBehaviour
{
    private TetherConstraint2D _owner;
    private Joint2D _watchedJoint;

    public void Bind(
        TetherConstraint2D owner,
        Joint2D watchedJoint)
    {
        _owner = owner;
        _watchedJoint = watchedJoint;
    }

    public void Unbind(
        TetherConstraint2D owner)
    {
        if (_owner != owner)
            return;

        _owner = null;
        _watchedJoint = null;
    }

    private void OnJointBreak2D(
        Joint2D brokenJoint)
    {
        if (_owner == null ||
            _watchedJoint == null ||
            brokenJoint == null ||
            brokenJoint != _watchedJoint)
        {
            return;
        }

        _owner.NotifyRuntimeJointBroken(
            brokenJoint);
    }
}
