using System;
using UnityEngine;

public static class WorldMapTopographyHeightCodec
{
    public const string UShortBase64Encoding = "ushort_base64_v1";
    public const int UShortMax = 65535;

    public static string EncodeUShortBase64(float[] height01)
    {
        if (height01 == null || height01.Length == 0)
            return string.Empty;

        byte[] bytes = new byte[height01.Length * 2];

        for (int i = 0; i < height01.Length; i++)
        {
            ushort q = Quantize01(height01[i]);
            int b = i * 2;

            // Explicit little-endian so saves are stable across platforms.
            bytes[b] = (byte)(q & 0xFF);
            bytes[b + 1] = (byte)((q >> 8) & 0xFF);
        }

        return Convert.ToBase64String(bytes);
    }

    public static float[] DecodeUShortBase64(string encoded, int expectedSampleCount)
    {
        if (string.IsNullOrWhiteSpace(encoded) || expectedSampleCount <= 0)
            return null;

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WorldMapTopographyHeightCodec] Failed to decode topography base64: {ex.Message}");
            return null;
        }

        int availableSamples = bytes.Length / 2;
        int count = Mathf.Min(expectedSampleCount, availableSamples);

        if (count <= 0)
            return null;

        var height01 = new float[expectedSampleCount];

        for (int i = 0; i < count; i++)
        {
            int b = i * 2;
            ushort q = (ushort)(bytes[b] | (bytes[b + 1] << 8));
            height01[i] = q / (float)UShortMax;
        }

        // If the byte payload is short somehow, leave the remainder as 0 and let validation scream.
        return height01;
    }

    public static ushort Quantize01(float value)
    {
        return (ushort)Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Clamp01(value) * UShortMax),
            0,
            UShortMax
        );
    }

    public static int GetEncodedByteCount(int sampleCount)
    {
        return Mathf.Max(0, sampleCount) * 2;
    }

    public static int GetBase64CharCount(int sampleCount)
    {
        int byteCount = GetEncodedByteCount(sampleCount);
        return ((byteCount + 2) / 3) * 4;
    }
}
