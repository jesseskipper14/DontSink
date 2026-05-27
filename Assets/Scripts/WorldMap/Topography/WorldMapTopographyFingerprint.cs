using System.Text;
using UnityEngine;

public static class WorldMapTopographyFingerprint
{
    public static string Build(int worldSeed, WorldMapTopographySettings settings)
    {
        if (settings == null)
            return $"{worldSeed}:NULL_SETTINGS";

        string json = JsonUtility.ToJson(settings);
        uint hash = Fnv1a32(json);

        return $"{worldSeed}:{hash:X8}";
    }

    private static uint Fnv1a32(string text)
    {
        const uint offset = 2166136261u;
        const uint prime = 16777619u;

        uint hash = offset;

        if (!string.IsNullOrEmpty(text))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }
        }

        return hash;
    }
}