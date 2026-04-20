using UnityEngine;

/// <summary>
/// Boat hatch. Can be opened/closed.
/// </summary>
public class Hatch : BoatPart
{
    [Header("Hatch Properties")]
    public bool isOpen = false;
    public float openAngle = 90f;

    /// <summary>
    /// Open hatch (disables collider for passage)
    /// </summary>
    public void Open()
    {
        isOpen = true;
        transform.localRotation = Quaternion.Euler(0f, 0f, openAngle);
        if (col != null) col.enabled = false;
    }

    /// <summary>
    /// Close hatch (reactivates collider)
    /// </summary>
    public void Close()
    {
        isOpen = false;
        transform.localRotation = Quaternion.identity;
        if (col != null) col.enabled = true;
    }
}
