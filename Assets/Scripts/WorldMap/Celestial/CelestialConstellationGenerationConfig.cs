using System;
using UnityEngine;

/// <summary>Derivative truth tuning, intentionally excluded from the star-field fingerprint.</summary>
[Serializable]
public sealed class CelestialConstellationGenerationConfig
{
    [Min(2)] public int averageStarsPerConstellation = 6;
    [Min(0)] public int starCountVariation = 1;
    [Min(0)] public int averageBranchesPerConstellation = 5;
    [Min(0)] public int branchCountVariation;
    [Min(2)] public int minimumStarsPerConstellation = 4;
    [Range(2, 64)] public int maximumStarsPerConstellation = 7;

    public void Sanitize()
    {
        minimumStarsPerConstellation = Mathf.Clamp(minimumStarsPerConstellation, 2, 64);
        maximumStarsPerConstellation = Mathf.Clamp(maximumStarsPerConstellation, minimumStarsPerConstellation, 64);
        averageStarsPerConstellation = Mathf.Clamp(averageStarsPerConstellation, minimumStarsPerConstellation, maximumStarsPerConstellation);
        starCountVariation = Mathf.Clamp(starCountVariation, 0, 64);
        averageBranchesPerConstellation = Mathf.Clamp(averageBranchesPerConstellation, 0, 2016);
        branchCountVariation = Mathf.Clamp(branchCountVariation, 0, 2016);
    }

    public CelestialConstellationGenerationConfig Clone()
    {
        var copy = (CelestialConstellationGenerationConfig)MemberwiseClone();
        copy.Sanitize();
        return copy;
    }

    public string Fingerprint => CelestialConstellationNameGenerator.Hash64(JsonUtility.ToJson(this)).ToString("X16");
    public bool IsLegacy => averageStarsPerConstellation == 6 && starCountVariation == 1 &&
        averageBranchesPerConstellation == 5 && branchCountVariation == 0 &&
        minimumStarsPerConstellation == 4 && maximumStarsPerConstellation == 7;
}
