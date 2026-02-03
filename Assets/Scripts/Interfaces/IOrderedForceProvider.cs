public interface IOrderedForceProvider : IForceProvider
{
    bool Enabled { get; }
    int Priority { get; }
}
