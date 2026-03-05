using UnityEngine;

public class CorpseIntentSource : MonoBehaviour, ICharacterIntentSource
{
    public CharacterIntent Current { get; private set; }
}
