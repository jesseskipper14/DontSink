using System;
using System.Collections.Generic;

/// <summary>
/// Deterministic nautical name generator for constellation truth names.
/// Names are intentionally simple for the first pass: "The" + adjective + noun.
/// </summary>
public static class CelestialConstellationNameGenerator
{
    private static readonly string[] Adjectives =
    {
        "Drowned", "Dark", "Blue", "Black", "Briny", "Saltbound", "Sunken", "Stormborn",
        "Deep", "Cold", "Pale", "Silver", "Red", "Broken", "Hollow", "Lost",
        "Wandering", "Jagged", "Quiet", "Moonlit", "Foaming", "Tidal", "Drifting", "Sable",
        "Gilded", "Bleached", "Fathomless", "Ragged", "Ancient", "Flooded", "Whispering", "White"
    };

    private static readonly string[] Nouns =
    {
        "Crab", "Shark", "Ray", "Whale", "Eel", "Squid", "Octopus", "Gull",
        "Albatross", "Mariner", "Anchor", "Trident", "Kelp", "Seaweed", "Reef", "Shoal",
        "Wreck", "Lantern", "Tide", "Wave", "Maelstrom", "Abyss", "Shell", "Pearl",
        "Coral", "Leviathan", "Dolphin", "Seahorse", "Turtle", "Hook", "Sail", "Mast",
        "Sea", "Current", "Harpoon", "Keel", "Compass", "Cove", "Breaker", "Rudder"
    };

    public static string GenerateUniqueName(string stableId, HashSet<string> alreadyUsed)
    {
        ulong hash = Hash64(stableId ?? string.Empty);
        int adjectiveStart = (int)(hash % (ulong)Adjectives.Length);
        int nounStart = (int)((hash >> 17) % (ulong)Nouns.Length);

        int attempts = Adjectives.Length * Nouns.Length;
        for (int i = 0; i < attempts; i++)
        {
            int adjectiveIndex = (adjectiveStart + i) % Adjectives.Length;
            int nounIndex = (nounStart + i / Adjectives.Length + i * 7) % Nouns.Length;
            string candidate = $"The {Adjectives[adjectiveIndex]} {Nouns[nounIndex]}";

            if (alreadyUsed == null || alreadyUsed.Add(candidate))
                return candidate;
        }

        string fallback = $"The {Adjectives[adjectiveStart]} {Nouns[nounStart]} {hash & 0xFFFF:X4}";
        alreadyUsed?.Add(fallback);
        return fallback;
    }

    internal static ulong Hash64(string value)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                hash ^= (byte)(c & 0xFF);
                hash *= prime;
                hash ^= (byte)(c >> 8);
                hash *= prime;
            }

            return hash;
        }
    }
}
