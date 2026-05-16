public static class CargoLabelFormatter
{
    public static string Format(ItemDefinition def, int maxCharacters = 14)
    {
        if (def == null)
            return "";

        string raw = !string.IsNullOrWhiteSpace(def.DisplayName)
            ? def.DisplayName
            : def.ItemId;

        if (string.IsNullOrWhiteSpace(raw))
            return "CARGO";

        raw = raw.Trim();

        if (raw.StartsWith("Crate of ", System.StringComparison.OrdinalIgnoreCase))
            raw = raw.Substring("Crate of ".Length);

        if (raw.StartsWith("Cargo ", System.StringComparison.OrdinalIgnoreCase))
            raw = raw.Substring("Cargo ".Length);

        if (raw.EndsWith(" Crate", System.StringComparison.OrdinalIgnoreCase))
            raw = raw.Substring(0, raw.Length - " Crate".Length);

        raw = raw.Replace("_", " ").Replace("-", " ").Trim();

        if (string.IsNullOrWhiteSpace(raw))
            raw = "Cargo";

        raw = raw.ToUpperInvariant();

        if (raw.Length > maxCharacters)
            raw = raw.Replace(" ", "");

        if (raw.Length > maxCharacters)
            raw = raw.Substring(0, maxCharacters);

        return raw;
    }

    public static bool IsCargo(ItemInstance item)
    {
        return item != null &&
               item.Definition != null &&
               (item.Definition.ItemCategories & ItemCategoryFlags.Cargo) != 0;
    }

    public static bool IsCargo(ItemDefinition def)
    {
        return def != null &&
               (def.ItemCategories & ItemCategoryFlags.Cargo) != 0;
    }
}