using System;
using System.Globalization;

public readonly struct CelestialStableIdParts
{
    public readonly int GeneratorVersion;
    public readonly int WorldSeed;
    public readonly int CellX;
    public readonly int CellY;
    public readonly CelestialObjectKind Kind;
    public readonly int LocalIndex;

    public CelestialStableIdParts(
        int generatorVersion,
        int worldSeed,
        int cellX,
        int cellY,
        CelestialObjectKind kind,
        int localIndex)
    {
        GeneratorVersion = generatorVersion;
        WorldSeed = worldSeed;
        CellX = cellX;
        CellY = cellY;
        Kind = kind;
        LocalIndex = localIndex;
    }
}

public static class CelestialStableId
{
    public static string Build(
        int generatorVersion,
        int worldSeed,
        int cellX,
        int cellY,
        CelestialObjectKind kind,
        int localIndex)
    {
        uint seedBits = unchecked((uint)worldSeed);
        return $"cel:{generatorVersion}:{seedBits:X8}:{cellX}:{cellY}:{(int)kind}:{localIndex}";
    }

    public static bool TryParse(string stableId, out CelestialStableIdParts parts)
    {
        parts = default;

        if (string.IsNullOrWhiteSpace(stableId))
            return false;

        string[] tokens = stableId.Split(':');
        if (tokens.Length != 7 || !string.Equals(tokens[0], "cel", StringComparison.Ordinal))
            return false;

        if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int generatorVersion))
            return false;

        if (!uint.TryParse(tokens[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seedBits))
            return false;

        if (!int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cellX))
            return false;

        if (!int.TryParse(tokens[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cellY))
            return false;

        if (!int.TryParse(tokens[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int kindRaw))
            return false;

        if (!Enum.IsDefined(typeof(CelestialObjectKind), kindRaw))
            return false;

        if (!int.TryParse(tokens[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int localIndex))
            return false;

        if (cellX < 0 || cellY < 0 || localIndex < 0)
            return false;

        parts = new CelestialStableIdParts(
            generatorVersion,
            unchecked((int)seedBits),
            cellX,
            cellY,
            (CelestialObjectKind)kindRaw,
            localIndex);

        return true;
    }
}
