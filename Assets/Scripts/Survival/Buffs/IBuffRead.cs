namespace Survival.Buffs
{
    public interface IPlayerBuffRead
    {
        bool Has(string stableId);
        bool TryGetSeverity01(string stableId, out float severity01);
    }
}