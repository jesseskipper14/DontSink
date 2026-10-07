using UnityEngine;

namespace MiniGames
{
    public sealed partial class SurveyorCartridge
    {
        private bool showSoundings;
        private void DrawSoundings()
        {
            GUILayout.Label("Sounding Charts — process evidence into seafloor charts");
            var carried = CartographicChartIntegration.Collect(context.Actor);
            int count = 0;
            foreach (var entry in carried)
            {
                var chart = entry.Item.CartographicChart;
                if (chart?.kind != CartographicChartKind.SoundingEvidence) continue;
                count++;
                GUILayout.Label($"{chart.title}\nBottom reading: {chart.sounding?.measuredDepthMeters:0.0} m");
                bool prior = GUI.enabled;
                GUI.enabled = prior && !closed && IsInRange() && GameplayAuthority.IsAuthoritative;
                if (GUILayout.Button("Process Sounding Chart") && IsInRange())
                    SoundingCartographyService.TryProcess(context.Actor, knowledge, entry.Item.InstanceId, out note);
                GUI.enabled = prior;
            }
            if (count == 0) GUILayout.Label("No unprocessed Sounding Charts carried. Record bottom readings with the sounding line and Charting Paper.");
            GUILayout.Label("Processing does not reveal the map. Integrate the resulting chart at the Mapping Table. Depths remain hidden until surface geography is known.");
        }
    }
}
