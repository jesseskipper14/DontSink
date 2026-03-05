using Survival.Vitals;

namespace Survival.Afflictions
{
    public interface IAfflictionRead
    {
        bool Has(StableId id);
        bool TryGetSeverity01(StableId id, out float severity01);
    }
}