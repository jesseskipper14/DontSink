using System;
using UnityEngine;

public static class WorldMapKnowledgeBitCodec
{
    public const string BitBase64Encoding = "bitset_base64_v1";

    public static string Encode(bool[] values)
    {
        if (values == null || values.Length == 0)
            return string.Empty;

        byte[] bytes = new byte[(values.Length + 7) / 8];

        for (int i = 0; i < values.Length; i++)
        {
            if (!values[i])
                continue;

            int b = i >> 3;
            int bit = i & 7;
            bytes[b] |= (byte)(1 << bit);
        }

        return Convert.ToBase64String(bytes);
    }

    public static bool[] Decode(string encoded, int expectedCount)
    {
        if (expectedCount <= 0)
            return Array.Empty<bool>();

        bool[] values = new bool[expectedCount];

        if (string.IsNullOrWhiteSpace(encoded))
            return values;

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WorldMapKnowledgeBitCodec] Failed to decode knowledge bitset: {ex.Message}");
            return values;
        }

        int max = Mathf.Min(expectedCount, bytes.Length * 8);

        for (int i = 0; i < max; i++)
        {
            int b = i >> 3;
            int bit = i & 7;
            values[i] = (bytes[b] & (1 << bit)) != 0;
        }

        return values;
    }

    public static int CountRevealed(bool[] values)
    {
        if (values == null)
            return 0;

        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
                count++;
        }

        return count;
    }

    public static float PercentRevealed(bool[] values)
    {
        if (values == null || values.Length == 0)
            return 0f;

        return CountRevealed(values) / (float)values.Length;
    }
}
