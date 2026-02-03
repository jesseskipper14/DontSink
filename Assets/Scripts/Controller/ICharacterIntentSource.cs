using UnityEngine;

public struct CharacterIntent
{
    public float MoveX;     // [-1..1]
    public bool JumpPressed;
    public bool JumpHeld;
}

public interface ICharacterIntentSource
{
    CharacterIntent Current { get; }
}
