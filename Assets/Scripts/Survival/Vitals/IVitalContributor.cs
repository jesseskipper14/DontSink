//// DEPRECATED

//namespace Survival.Vitals
//{
//    /// <summary>
//    /// Contributors "write into" the VitalContext each tick.
//    /// Order should NOT matter if contributors are well-behaved.
//    /// Prefer multiplicative modifiers for cross-mod compatibility.
//    /// </summary>
//    public interface IVitalContributor
//    {
//        StableId ContributorId { get; }
//        void Contribute(ref VitalContext ctx, float dt);
//    }
//}