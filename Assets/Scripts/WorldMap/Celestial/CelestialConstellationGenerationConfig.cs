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
    // Missing in older saves means no thinning, preserving their derivative truth.
    public float constellationReductionPercent;
    // False for missing older-save data: retain the previous total-edge interpretation.
    public bool branchCountIsExtraConnections;

    public void Sanitize()
    {
        minimumStarsPerConstellation = Mathf.Clamp(minimumStarsPerConstellation, 2, 64);
        maximumStarsPerConstellation = Mathf.Clamp(maximumStarsPerConstellation, minimumStarsPerConstellation, 64);
        averageStarsPerConstellation = Mathf.Clamp(averageStarsPerConstellation, minimumStarsPerConstellation, maximumStarsPerConstellation);
        starCountVariation = Mathf.Clamp(starCountVariation, 0, 64);
        averageBranchesPerConstellation = Mathf.Clamp(averageBranchesPerConstellation, 0, 2016);
        branchCountVariation = Mathf.Clamp(branchCountVariation, 0, 2016);
        constellationReductionPercent = Mathf.Clamp(constellationReductionPercent, 0f, 100f);
    }

    public CelestialConstellationGenerationConfig Clone()
    {
        var copy = (CelestialConstellationGenerationConfig)MemberwiseClone();
        copy.Sanitize();
        return copy;
    }

    [Serializable]
    private sealed class PreviousIdentity
    {
        public int averageStarsPerConstellation, starCountVariation, averageBranchesPerConstellation,
            branchCountVariation, minimumStarsPerConstellation, maximumStarsPerConstellation;
    }

    [Serializable]
    private sealed class ReductionIdentity
    {
        public int averageStarsPerConstellation, starCountVariation, averageBranchesPerConstellation,
            branchCountVariation, minimumStarsPerConstellation, maximumStarsPerConstellation;
        public float constellationReductionPercent;
    }

    public string Fingerprint
    {
        get
        {
            // Preserve the exact pre-thinning fingerprint for existing custom-tuned saves too.
            string json;
            if (branchCountIsExtraConnections) json = JsonUtility.ToJson(this);
            else if (constellationReductionPercent > 0f) json = JsonUtility.ToJson(new ReductionIdentity
            {
                averageStarsPerConstellation = averageStarsPerConstellation,
                starCountVariation = starCountVariation,
                averageBranchesPerConstellation = averageBranchesPerConstellation,
                branchCountVariation = branchCountVariation,
                minimumStarsPerConstellation = minimumStarsPerConstellation,
                maximumStarsPerConstellation = maximumStarsPerConstellation,
                constellationReductionPercent = constellationReductionPercent
            });
            else json = JsonUtility.ToJson(new PreviousIdentity
            {
                averageStarsPerConstellation = averageStarsPerConstellation,
                starCountVariation = starCountVariation,
                averageBranchesPerConstellation = averageBranchesPerConstellation,
                branchCountVariation = branchCountVariation,
                minimumStarsPerConstellation = minimumStarsPerConstellation,
                maximumStarsPerConstellation = maximumStarsPerConstellation
            });
            return CelestialConstellationNameGenerator.Hash64(json).ToString("X16");
        }
    }
    public bool IsLegacy => UsesLegacyBranchCounts && constellationReductionPercent == 0f;
    public bool UsesLegacyBranchCounts => !branchCountIsExtraConnections && averageStarsPerConstellation == 6 && starCountVariation == 1 &&
        averageBranchesPerConstellation == 5 && branchCountVariation == 0 &&
        minimumStarsPerConstellation == 4 && maximumStarsPerConstellation == 7;
}
