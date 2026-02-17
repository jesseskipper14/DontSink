using System;
using UnityEngine;

[Serializable]
public struct ResourcePressureBias
{
    [Tooltip("Matches ResourceDef.itemId")]
    public string itemId;

    [Tooltip("Positive = surplus tendency, Negative = shortage tendency. Typical range -2..+2.")]
    [Range(-4f, 4f)]
    public float bias;
}
