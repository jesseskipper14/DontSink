//using UnityEngine;

//namespace Survival.Vitals
//{
//    public interface IBloodRead
//    {
//        float BloodVolume01 { get; }     // 0..1
//        float BloodQuality01 { get; }    // 0..1 (CO poisoning etc.)
//    }

//    /// <summary>
//    /// Optional now, easy later. Blood volume affects perfusion; quality affects transport.
//    /// </summary>
//    [DisallowMultipleComponent]
//    public sealed class BloodVitalContributor : MonoBehaviour, IVitalContributor
//    {
//        [SerializeField] private MonoBehaviour bloodSource; // must implement IBloodRead
//        public StableId ContributorId => "core.blood";

//        private IBloodRead Blood => bloodSource as IBloodRead;

//        public void Contribute(ref VitalContext ctx, float dt)
//        {
//            if (Blood == null)
//                return;

//            ctx.perfusion01 *= Mathf.Clamp01(Blood.BloodVolume01);
//            ctx.oxygenTransport01 *= Mathf.Clamp01(Blood.BloodQuality01);
//        }
//    }
//}