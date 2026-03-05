namespace Survival.Vitals
{
    // Supply-side: air reservoir + whether oxygenation can occur.
    public interface IAirOxygenationRead
    {
        float Air01 { get; }             // quantity/volume 0..1
        bool CanOxygenate { get; }       // surface/tank/hose etc.
        float OxygenQuality01 { get; }   // environment/source quality 0..1
        float LungGasQuality01 { get; }  // lung gas quality 0..1 (holding breath decay)
        bool IsUnderwater { get; }       // informational
    }

    // Body-side: how well lungs/circulation/blood deliver oxygen.
    public interface IOxygenationBodyRead
    {
        float LungEffectiveness01 { get; }    // 0..1
        float Perfusion01 { get; }            // 0..1 (circulation)
        float BloodQuality01 { get; }         // 0..1 (CO/anemia)
        float DemandMul { get; }              // >= 0.1 (fever/exertion)
    }
}