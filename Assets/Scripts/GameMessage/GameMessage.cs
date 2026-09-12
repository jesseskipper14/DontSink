using System;

public enum GameMessageType
{
    System = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    PlayerChat = 4
}

[Serializable]
public sealed class GameMessage
{
    public long Sequence;
    public string Text;
    public GameMessageType Type;
    public string Sender;
    public double PostedRealtimeSeconds;

    public GameMessage(
        long sequence,
        string text,
        GameMessageType type,
        string sender,
        double postedRealtimeSeconds)
    {
        Sequence = sequence;
        Text = text;
        Type = type;
        Sender = sender;
        PostedRealtimeSeconds = postedRealtimeSeconds;
    }
}
