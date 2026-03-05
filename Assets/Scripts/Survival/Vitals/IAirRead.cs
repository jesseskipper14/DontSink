//namespace Survival.Vitals
//{

//    //DEPRECATED

//    /// <summary>
//    /// Adapter interface so vital system doesn't depend on your concrete PlayerAirState class.
//    /// </summary>
//    public interface IAirRead
//    {
//        float Air01 { get; }             // quantity/volume 0..1
//        bool CanOxygenate { get; }       // surface/tank/hose etc.
//        float OxygenQuality01 { get; }   // environment/source quality 0..1
//        float LungGasQuality01 { get; }  // lung gas quality 0..1 (holding breath decay)
//        bool IsUnderwater { get; }       // informational
//    }
//}