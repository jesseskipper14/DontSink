public interface IEscapeClosable
{
    int EscapePriority { get; }
    bool IsEscapeOpen { get; }

    /// <summary>
    /// Return true if this object handled Escape.
    /// </summary>
    bool CloseFromEscape();
}