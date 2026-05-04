using UnityEngine;

public struct CharacterIntent
{
    public float MoveX;
    public bool JumpPressed;
    public bool JumpHeld;
    public bool UprightHeld;
    public bool SwimUpHeld;
    public bool DiveHeld;
    public bool SprintHeld;
    public bool ClimbUpHeld;
    public bool ClimbDownHeld;
    public bool FocusHeld;
    public Vector2 FocusWorldPoint;
    public Vector2 AimWorldPoint;
    public bool PrimaryUseHeld;
    public bool CancelHeld;
}

public interface ICharacterIntentSource
{
    CharacterIntent Current { get; }
}
