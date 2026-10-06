using UnityEngine;

/// <summary>Local town presentation only; no live simulation or shared state changes.</summary>
public static class NodeTownPresentationRules
{
    public static int CargoCapacity(float streetWidth) => Mathf.Clamp(Mathf.CeilToInt(streetWidth / 16),2,6);
    public static int CargoCount(float streetWidth,float prosperity) =>
        Mathf.RoundToInt(CargoCapacity(streetWidth) * Mathf.Pow(Mathf.Clamp01(prosperity),1.35f));

    public static float Desaturation(string archetype)
    {
        string id = (archetype ?? "").ToLowerInvariant();
        return id switch {
            "fuel_depot" => .78f, "salvage_yard" => .72f, "shipyard" => .60f,
            "fortress_island" => .50f, "storm_refuge" => .35f, "smuggler_cove" => .25f,
            "trade_hub" => .12f, "fishing_hamlet" => .08f, "lumber_port" => .04f,
            "farming_atoll" => .02f,
            _ => id.Contains("industrial") || id.Contains("industrial_complex") ? .82f : .18f
        };
    }

    public static Color Tint(Color original,string archetype)
    {
        float gray = original.r * .2126f + original.g * .7152f + original.b * .0722f;
        return Color.Lerp(original,new Color(gray,gray,gray,original.a),Desaturation(archetype));
    }
}
