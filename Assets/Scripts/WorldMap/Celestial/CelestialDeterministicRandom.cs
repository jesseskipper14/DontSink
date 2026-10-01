using UnityEngine;

/// <summary>
/// Small platform-stable PRNG used only by celestial generation. Do not replace it
/// with System.Random or UnityEngine.Random without intentionally changing the
/// celestial generator version.
/// </summary>
internal struct CelestialDeterministicRandom
{
    private ulong _state;
    private readonly ulong _increment;

    public CelestialDeterministicRandom(ulong seed, ulong sequence)
    {
        _state = 0UL;
        _increment = (sequence << 1) | 1UL;

        NextUIntInternal(ref _state, _increment);
        _state = unchecked(_state + seed);
        NextUIntInternal(ref _state, _increment);
    }

    public uint NextUInt()
    {
        return NextUIntInternal(ref _state, _increment);
    }

    public float Next01()
    {
        // 24 deterministic random bits map cleanly into a Unity float mantissa.
        return (NextUInt() >> 8) * (1f / 16777216f);
    }

    public int RangeInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            return minInclusive;

        uint span = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt() % span);
    }

    public float Range(float minInclusive, float maxInclusive)
    {
        if (maxInclusive <= minInclusive)
            return minInclusive;

        return Mathf.Lerp(minInclusive, maxInclusive, Next01());
    }

    private static uint NextUIntInternal(ref ulong state, ulong increment)
    {
        unchecked
        {
            ulong oldState = state;
            state = oldState * 6364136223846793005UL + increment;

            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);

            return (xorShifted >> rotation) |
                   (xorShifted << ((-rotation) & 31));
        }
    }
}
