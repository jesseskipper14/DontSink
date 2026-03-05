using System;

namespace Survival.Vitals
{
    /// <summary>
    /// Small wrapper to make it obvious we're dealing in stable IDs for modding/network safety.
    /// </summary>
    [Serializable]
    public struct StableId : IEquatable<StableId>
    {
        public string value;

        public StableId(string v) => value = v ?? string.Empty;

        public bool Equals(StableId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StableId other && Equals(other);
        public override int GetHashCode() => (value ?? string.Empty).GetHashCode();
        public override string ToString() => value;

        public static implicit operator string(StableId id) => id.value;
        public static implicit operator StableId(string s) => new StableId(s);
    }
}