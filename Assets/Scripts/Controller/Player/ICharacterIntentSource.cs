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
}

public interface ICharacterIntentSource
{
    CharacterIntent Current { get; }
}
