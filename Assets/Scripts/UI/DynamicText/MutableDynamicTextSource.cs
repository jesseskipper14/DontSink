using UnityEngine;

/// <summary>
/// Generic runtime-settable text source for signs/labels whose value is owned by
/// another system. Call SetText and any bound DynamicTextBinder refreshes at once.
/// </summary>
[DisallowMultipleComponent]
public sealed class MutableDynamicTextSource : DynamicTextSourceBehaviour
{
    [SerializeField, TextArea] private string value = "";

    public string Value => value ?? string.Empty;

    public override bool TryGetText(out string text)
    {
        text = Value;
        return true;
    }

    public void SetText(string nextValue)
    {
        nextValue ??= string.Empty;

        if (value == nextValue)
            return;

        value = nextValue;
        NotifyTextChanged();
    }
}
