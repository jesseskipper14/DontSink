using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MiniGames
{
    /// <summary>Presentation only. Never creates state, grants chart knowledge or evaluates simulation.</summary>
    public static class NodeLocalInformationReport
    {
        private static readonly NodeStatId[] Stats = { NodeStatId.Prosperity, NodeStatId.Stability,
            NodeStatId.Security, NodeStatId.FoodBalance, NodeStatId.TradeRating, NodeStatId.DockRating };
        private static readonly string[] Labels = { "Prosperity", "Stability", "Security", "Food Balance", "Trade Rating", "Dock Rating" };

        public static string Build(string nodeId, string name, MapNodeState state, int nodeIndex,
            IReadOnlyList<WorldMapEventInstance> events, bool precise = false, ResourceCatalog resources = null)
        {
            if (state == null || state.NodeId != nodeId) return "Current settlement information is unavailable.";
            var text = new StringBuilder(1024);
            text.AppendLine(string.IsNullOrWhiteSpace(name) ? "Settlement" : name).AppendLine("Current local report").AppendLine();
            text.Append("Population: ").AppendLine(Number(state.population, precise, true));
            for (int index = 0; index < Stats.Length; index++)
            {
                var id = Stats[index];
                text.Append(Labels[index]).Append(": ");
                text.Append(state.TryGetStat(id, out var stat) ? Number(stat.value, precise) : "Unavailable");
                text.AppendLine(id == NodeStatId.FoodBalance ? "  (−4 to 4)" : "  (0 to 4)");
            }

            text.AppendLine().AppendLine("Civic conditions");
            bool any = false;
            if (state.Flags != null)
                foreach (string flag in state.Flags)
                    if (!string.IsNullOrWhiteSpace(flag)) { text.AppendLine(HumanName(flag)); any = true; }
            if (!any) text.AppendLine("No special conditions reported.");

            text.AppendLine().AppendLine("Active effects");
            any = false;
            if (state.ActiveBuffs != null)
                foreach (var effect in state.ActiveBuffs)
                {
                    if (effect.buff == null || effect.IsExpired) continue;
                    var buff = effect.buff;
                    text.Append(string.IsNullOrWhiteSpace(buff.displayName) ? HumanName(buff.buffId) : buff.displayName);
                    if (effect.stacks > 1) text.Append(" ×").Append(effect.stacks);
                    text.Append(" — ").Append(Number(effect.RemainingHours, false)).AppendLine(" hours remaining");
                    string target = buff.target.kind == NodeValueTargetKind.Stat ? HumanName(buff.target.statId.ToString()) :
                        buff.target.kind == NodeValueTargetKind.OptionalBuildingRating ? HumanName(buff.target.buildingId.ToString()) : HumanName(buff.target.kind.ToString());
                    float change = effect.GetAccelThisTick();
                    text.Append("  ").Append(target).Append(": ").Append(change >= 0 ? "+" : "")
                        .Append(change.ToString(precise ? "R" : "0.###", CultureInfo.InvariantCulture)).AppendLine(" per hour");
                    any = true;
                }
            if (!any) text.AppendLine("No active effects.");

            text.AppendLine().AppendLine("Current events");
            any = false;
            if (events != null && nodeIndex >= 0)
                foreach (var ev in events)
                {
                    if (ev.sourceNodeId != nodeIndex || ev.isResolved || ev.def == null || !ev.def.isVisibleToPlayer) continue;
                    text.Append(string.IsNullOrWhiteSpace(ev.def.displayName) ? HumanName(ev.def.eventId) : ev.def.displayName)
                        .Append(" — ").Append(Number(ev.RemainingHours, false)).AppendLine(" hours remaining");
                    any = true;
                }
            if (!any) text.AppendLine(events == null || nodeIndex < 0 ? "Event reports are unavailable." : "No public events reported.");

            text.AppendLine().AppendLine("Shortages and surpluses");
            text.AppendLine("Market pressure: negative means shortage; positive means surplus.");
            any = false;
            if (state.ResourcePressures != null)
            {
                var keys = new List<string>(state.ResourcePressures.Keys);
                keys.Sort(System.StringComparer.Ordinal);
                foreach (string key in keys)
                {
                    float pressure = state.ResourcePressures[key].value;
                    // The 0–4 pressure scale includes small fluctuations; show clear conditions.
                    if (System.Math.Abs(pressure) < 1) continue;
                    var definition = resources != null ? resources.GetOrNull(key) : null;
                    string itemName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName) ? definition.displayName : HumanName(key);
                    text.Append(itemName).Append(pressure < 0 ? ": shortage (" : ": surplus (+")
                        .Append(Number(pressure, precise)).AppendLine(")");
                    any = true;
                }
            }
            if (!any) text.AppendLine("No significant shortages or surpluses reported.");
            return text.ToString();
        }

        private static string Number(float value, bool precise, bool population = false) =>
            value.ToString(precise ? "R" : population ? "0" : "0.0", CultureInfo.InvariantCulture);

        private static string HumanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed";
            var words = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '_' || c == '-') words.Append(' ');
                else
                {
                    if (i > 0 && char.IsUpper(c) && char.IsLower(value[i - 1])) words.Append(' ');
                    words.Append(c);
                }
            }
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words.ToString().ToLowerInvariant());
        }
    }
}
