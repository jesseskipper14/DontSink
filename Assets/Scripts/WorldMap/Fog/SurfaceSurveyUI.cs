using UnityEngine;

/// <summary>Reference/navigation presentation only. No exact target coordinates or automatic sky matching.</summary>
public static class SurfaceSurveyUI
{
    // Crop the central half of the registered sky projection; saved cards retain their original coordinates.
    public static bool TryProjectReference(Vector2 viewport, out Vector2 reference)
    {
        reference = (viewport - Vector2.one * .5f) * 2 + Vector2.one * .5f;
        return WorldTopology.IsFinite(reference.x) && WorldTopology.IsFinite(reference.y) &&
            reference.x >= 0 && reference.x <= 1 && reference.y >= 0 && reference.y <= 1;
    }

    public static void DrawInstructions(SurfaceSurveyContract contract, ref int selectedZone)
    {
        if (contract?.zones == null || contract.zones.Count == 0) return;
        GUILayout.Label(contract.title);
        GUILayout.Label($"Readings: {contract.CompletedCount}/{contract.zones.Count}. Any order.");
        GUILayout.Label("Headings and approximate distances are measured from " + contract.originName + ". North = 0°, clockwise.");
        for (int i = 0; i < contract.zones.Count; i++)
        {
            var zone = contract.zones[i];
            if (GUILayout.Button($"{(zone.completed ? "Recorded" : "Point")} {i + 1}: {zone.heading:0}° · about {zone.roughDistance:0} map units")) selectedZone = i;
        }
        selectedZone = Mathf.Clamp(selectedZone, 0, contract.zones.Count - 1);
        GUILayout.Label($"Point {selectedZone + 1} sky reference — north up, east right");
        Rect available = GUILayoutUtility.GetRect(180, 180, GUILayout.ExpandWidth(true));
        float size = Mathf.Min(180, available.width);
        Rect card = new Rect(available.x + (available.width - size) * .5f, available.y, size, size);
        Color prior = GUI.color;
        GUI.color = new Color(.03f, .06f, .12f); GUI.DrawTexture(card, Texture2D.whiteTexture);
        GUI.BeginGroup(card);
        var points = contract.zones[selectedZone].sky;
        if (points != null) foreach (var star in points)
        {
            if (star == null || !TryProjectReference(star.viewport, out var reference)) continue;
            float diameter = star.landmark ? 5 : Mathf.Lerp(1, 3, star.brightness);
            Vector2 center = new Vector2(reference.x * card.width, (1 - reference.y) * card.height);
            GUI.color = star.color; GUI.DrawTexture(new Rect(center.x - diameter / 2, center.y - diameter / 2, diameter, diameter), Texture2D.whiteTexture);
            if (star.landmark)
            {
                GUI.DrawTexture(new Rect(center.x - 5, center.y - .5f, 10, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - .5f, center.y - 5, 1, 10), Texture2D.whiteTexture);
            }
        }
        DrawCenterReference(new Vector2(card.width * .5f, card.height * .5f));
        GUI.EndGroup();
        GUI.color = prior;
        GUILayout.Label("Match the stars around the cyan center mark with the survey sky's center mark. This card grants no chart knowledge or position fix.");
    }

    public static void DrawCenterReference(Vector2 center)
    {
        // Same fixed-size glyph in both views; the open center leaves nearby stars readable.
        Color prior = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(center.x - 9, center.y - 2, 6, 4), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x + 3, center.y - 2, 6, 4), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - 2, center.y - 9, 4, 6), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - 2, center.y + 3, 4, 6), Texture2D.whiteTexture);
        GUI.color = Color.cyan;
        GUI.DrawTexture(new Rect(center.x - 8, center.y - 1, 5, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x + 3, center.y - 1, 5, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - 1, center.y - 8, 2, 5), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - 1, center.y + 3, 2, 5), Texture2D.whiteTexture);
        GUI.color = prior;
    }

    public static void DrawRing(Vector2 center, float radius, Color color)
    {
        var prior = GUI.color; var matrix = GUI.matrix; GUI.color = color;
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Vector2 q = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(q.y - p.y, q.x - p.x) * Mathf.Rad2Deg, p);
            GUI.DrawTexture(new Rect(p.x, p.y, (q - p).magnitude, 1.5f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
        GUI.color = prior;
    }
}
