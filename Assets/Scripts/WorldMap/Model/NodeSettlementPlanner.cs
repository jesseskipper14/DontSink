using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure layout/presentation planning. Call state-changing methods only on authority.</summary>
public static class NodeSettlementPlanner
{
    public static int StableSeed(string value)
    {
        unchecked { uint hash = 2166136261; foreach (char c in value ?? "") hash = (hash ^ c) * 16777619; return (int)(hash & 0x7fffffff); }
    }

    public static NodeSettlementManifest Generate(string nodeId, string archetypeId)
    {
        var rules = SettlementArchetypeRules.For(archetypeId);
        var rng = new System.Random(StableSeed(nodeId));
        var layout = new NodeSettlementManifest { nodeStableId = nodeId, archetypeId = archetypeId,
            seed = StableSeed(nodeId), visualPopulationCapacity = rules.populationCapacity, harborArrival = new Vector2(30, 0) };
        int terraceCount = rng.Next(rules.minTerraces, rules.maxTerraces + 1);
        for (int t = 0; t < terraceCount; t++)
        {
            // Room for five-body chains and head clearance. Arrival is on the canonical landward shore.
            layout.terraces.Add(new() { id = $"{nodeId}/terrace/{t}", left = 1, right = 30, y = 4 + t * (rules.maxStack * 3.4f + 1) });
            layout.connections.Add(new() { id = $"{nodeId}/access/{t}", fromTerrace = t - 1, toTerrace = t, x = 29 });
            for (int column = 0; column < (t == 0 ? 4 : 2); column++)
            {
                SettlementRole role = t == 0 && column == 3 ? SettlementRole.Surveyor :
                    t == 0 && column == 2 ? SettlementRole.Market :
                    t == 0 && column == 1 ? SettlementRole.EventReserve : ChooseRole(rules, rng);
                int stack = IsOpen(role) || role == SettlementRole.Surveyor || role == SettlementRole.Market ? 1 :
                    rng.NextDouble() < rules.stackChance ? rng.Next(2, rules.maxStack + 1) : 1;
                string parent = null;
                for (int level = 0; level < stack; level++)
                {
                    string id = $"{nodeId}/plot/{t}/{column}/{level}";
                    var plot = new SettlementPlot { id = id, parentPlotId = parent, terrace = t, level = level,
                        role = level == 0 ? role : SettlementRole.Residence, position = new Vector2(5 + column * 6, layout.terraces[t].y + level * 3.4f),
                        width = 3.8f, height = 3, familyId = $"{(level == 0 ? role : SettlementRole.Residence).ToString().ToLowerInvariant()}_{rng.Next(3)}",
                        roofFamily = rng.Next(2) == 0 ? "timber" : "metal", palette = rng.Next(7), roofPalette = rng.Next(7) };
                    // Fixed dimensions belong to the chosen primitive body family, not a plot-fit stretch.
                    plot.width = plot.role == SettlementRole.Warehouse || plot.role == SettlementRole.Industry ? 4.2f :
                        3.4f + (plot.familyId[^1] - '0') * .4f;
                    AddSockets(plot);
                    layout.plots.Add(plot);
                    parent = id;
                }
            }
        }
        Reflow(layout);
        Validate(layout, out string reason);
        if (reason != null) throw new InvalidOperationException(reason);
        return layout;
    }

    private static bool IsOpen(SettlementRole role) => role == SettlementRole.WorkYard || role == SettlementRole.Garden || role == SettlementRole.EventReserve || role == SettlementRole.TownSquare;

    private static SettlementRole ChooseRole(SettlementArchetypeRules rules, System.Random rng)
    {
        if (rng.NextDouble() < rules.openChance) return rng.Next(2) == 0 ? SettlementRole.Garden : SettlementRole.WorkYard;
        int pick = rng.Next(8);
        if (pick == 0 && rules.defensive) return SettlementRole.Defense;
        if (pick == 1 && rules.industry) return SettlementRole.Industry;
        if (pick == 2 && rules.industry) return SettlementRole.Warehouse;
        if (pick == 3) return SettlementRole.Tavern;
        if (pick == 4) return SettlementRole.Harbormaster;
        return SettlementRole.Residence;
    }

    private static void AddSockets(SettlementPlot plot)
    {
        SettlementSocketKind npc = plot.role == SettlementRole.Defense ? SettlementSocketKind.Guard :
            plot.role == SettlementRole.Market ? SettlementSocketKind.Merchant :
            plot.role == SettlementRole.Industry || plot.role == SettlementRole.Warehouse || plot.role == SettlementRole.WorkYard ? SettlementSocketKind.Worker : SettlementSocketKind.Civilian;
        // Shape-family authored socket template; no random physics scatter on each visit.
        for (int i = 0; i < 3; i++)
        {
            plot.sockets.Add(new() { id = $"{plot.id}/person/{i}", kind = npc, offset = new Vector2(-1 + i, .05f), threshold = (i + 1) / 4f });
            plot.sockets.Add(new() { id = $"{plot.id}/display/{i}", kind = plot.role == SettlementRole.Market ? SettlementSocketKind.Food :
                plot.role == SettlementRole.Garden ? SettlementSocketKind.Garden : SettlementSocketKind.Merchandise,
                offset = new Vector2(-1 + i, .35f), threshold = (i + 1) / 4f });
        }
        plot.sockets.Add(new() { id = $"{plot.id}/decoration", kind = SettlementSocketKind.Decoration, offset = new Vector2(0, plot.IsOpen ? .75f : 2.4f), threshold = .45f });
        if (plot.role == SettlementRole.EventReserve) plot.sockets.Add(new() { id = $"{plot.id}/quest", kind = SettlementSocketKind.Quest });
    }

    public static bool Validate(NodeSettlementManifest layout, out string reason)
    {
        reason = null;
        if (layout == null || (layout.version != 1 && layout.version != 2 && layout.version != 3) || layout.terraces == null || layout.plots == null || layout.connections == null || layout.terraces.Count == 0)
        { reason = $"Missing or unsupported settlement manifest (version {layout?.version}, node '{layout?.nodeStableId}', terraces {layout?.terraces?.Count}, plots {layout?.plots?.Count}). Existing layouts were not regenerated."; return false; }
        var reachable = new HashSet<int> { -1 };
        bool progress;
        do
        {
            progress = false;
            foreach (var c in layout.connections)
            {
                if (c == null || c.toTerrace < 0 || c.toTerrace >= layout.terraces.Count || c.fromTerrace < -1 || c.fromTerrace >= layout.terraces.Count) continue;
                var upper = layout.terraces[c.toTerrace];
                bool onBoth = c.x >= upper.left && c.x <= upper.right &&
                    (c.fromTerrace == -1 || c.x >= layout.terraces[c.fromTerrace].left && c.x <= layout.terraces[c.fromTerrace].right);
                if (onBoth && reachable.Contains(c.fromTerrace) && reachable.Add(c.toTerrace)) progress = true;
            }
        } while (progress);
        if (reachable.Count != layout.terraces.Count + 1) { reason = "Settlement has an unreachable terrace."; return false; }
        var ids = new HashSet<string>();
        var byId = new Dictionary<string, SettlementPlot>();
        int surveyors = 0, markets = 0;
        foreach (var p in layout.plots)
        {
            if (p == null || string.IsNullOrEmpty(p.id) || !ids.Add(p.id) || p.terrace < 0 || p.terrace >= layout.terraces.Count)
            { reason = "Settlement plot identity or terrace is invalid."; return false; }
            var terrace = layout.terraces[p.terrace];
            if (p.width <= 0 || p.height <= 0 || p.position.x - p.width / 2 < terrace.left || p.position.x + p.width / 2 + .7f > terrace.right)
            { reason = $"Plot {p.id} has no safe footprint/access clearance."; return false; }
            if (p.level > 0 && (!byId.TryGetValue(p.parentPlotId ?? "", out var parent) || parent.level + 1 != p.level || parent.terrace != p.terrace || parent.position.x != p.position.x))
            { reason = $"Plot {p.id} has an invalid stack support."; return false; }
            byId.Add(p.id, p);
            if (p.role == SettlementRole.Surveyor) surveyors++;
            if (p.role == SettlementRole.Market) markets++;
            foreach (var socket in p.sockets)
                if (socket == null || string.IsNullOrEmpty(socket.id) || !ids.Add(socket.id))
                { reason = $"Duplicate/missing socket on {p.id}."; return false; }
        }
        if (surveyors != 1 || markets != 1) { reason = "Settlement requires exactly one Surveyor station and market."; return false; }
        return true;
    }

    public static SettlementVisitSnapshot Evaluate(MapNodeState state, NodeSettlementManifest layout, bool writeHistory)
    {
        writeHistory &= GameplayAuthority.IsAuthoritative;
        var visit = new SettlementVisitSnapshot { population = Mathf.Clamp01(state.population / Mathf.Max(1, layout.visualPopulationCapacity)),
            prosperity = Rating(state, NodeStatId.Prosperity), stability = Rating(state, NodeStatId.Stability),
            trade = Rating(state, NodeStatId.TradeRating), security = Rating(state, NodeStatId.Security),
            food = Mathf.Clamp01((state.GetStat(NodeStatId.FoodBalance).value + 4) / 8) };
        if (state.Flags != null) visit.flags.AddRange(state.Flags);
        if (state.ActiveBuffs != null)
            foreach (var buff in state.ActiveBuffs)
                if (!buff.IsExpired && buff.buff != null) visit.activeBuffIds.Add(buff.buff.buffId);
        var developed = new Dictionary<string, bool>();
        foreach (var plot in layout.plots)
        {
            float threshold = plot.level == 0 ? .08f : .2f + plot.level * .15f;
            bool parentReady = plot.level == 0 || developed.TryGetValue(plot.parentPlotId, out bool ready) && ready;
            bool grow = plot.IsRequired || parentReady && visit.population >= threshold && (plot.level == 0 || visit.prosperity >= .15f + plot.level * .1f);
            bool existed = plot.everDeveloped || grow;
            bool occupied = plot.IsRequired || existed && visit.population >= threshold - (plot.occupied ? .05f : 0) && visit.stability >= .08f;
            bool service = plot.IsRequired || existed && visit.prosperity >= (plot.serviceActive ? .35f : .45f) && visit.trade >= .2f;
            if (plot.damage != SettlementDamage.None) occupied = service = false;
            if (writeHistory) { plot.everDeveloped = existed; plot.occupied = occupied; plot.serviceActive = service; }
            developed[plot.id] = existed && plot.damage != SettlementDamage.Destroyed;
            float activity = plot.role == SettlementRole.Defense ? visit.security :
                plot.role == SettlementRole.Market || plot.role == SettlementRole.Warehouse || plot.role == SettlementRole.Industry || plot.role == SettlementRole.WorkYard ? visit.trade : visit.population * (.25f + visit.stability * .75f);
            var pv = new SettlementPlotVisit { plotId = plot.id, developed = existed, occupied = occupied,
                serviceActive = service, damage = plot.damage, condition = visit.prosperity * .6f + visit.stability * .4f,
                activity = activity, dressing = visit.prosperity };
            foreach (var socket in plot.sockets)
            {
                float fill = socket.kind switch {
                    SettlementSocketKind.Civilian => visit.population * (.25f + visit.stability * .75f),
                    SettlementSocketKind.Merchant or SettlementSocketKind.Worker => visit.trade,
                    SettlementSocketKind.Guard => visit.security,
                    SettlementSocketKind.Food => visit.trade * visit.food,
                    SettlementSocketKind.Garden => visit.food,
                    SettlementSocketKind.Decoration => visit.prosperity,
                    SettlementSocketKind.Quest => 0,
                    _ => activity };
                if ((occupied || plot.IsOpen) && plot.damage == SettlementDamage.None && fill >= socket.threshold) pv.activeSockets.Add(socket.id);
            }
            visit.plots.Add(pv);
        }
        return visit;
    }

    private static float Rating(MapNodeState state, NodeStatId id) => Mathf.Clamp01(state.GetStat(id).value / 4);

    public static bool TrySetDamage(MapNodeState state, string plotId, SettlementDamage damage)
    {
        if (!GameplayAuthority.IsAuthoritative || state?.settlement?.plots == null) return false;
        var plot = state.settlement.plots.Find(p => p.id == plotId);
        if (plot == null) return false;
        plot.damage = damage;
        return true;
    }

    public static NodeSettlementManifest Ensure(MapNodeRuntime runtime)
    {
        if (runtime == null || runtime.State == null) return null;
        if (IsUninitialized(runtime.State.settlement) && GameplayAuthority.IsAuthoritative)
            runtime.State.settlement = Generate(runtime.StableId, runtime.NodeArchetypeId);
        else if (runtime.State.settlement?.version == 1 && GameplayAuthority.IsAuthoritative && Validate(runtime.State.settlement, out _))
            Reflow(runtime.State.settlement);
        else if (runtime.State.settlement?.version == 2 && GameplayAuthority.IsAuthoritative && Validate(runtime.State.settlement, out _))
            NormalizeHouseStacks(runtime.State.settlement);
        return runtime.State.settlement;
    }

    // Prototype geometry migration: retain permanent IDs, family choices and all history.
    // Land occupies X=0..120; the last 18 units remain clear for authored harbor shops.
    private static void Reflow(NodeSettlementManifest layout)
    {
        var rng = new System.Random(layout.seed);
        layout.harborArrival = new Vector2(120, 0);
        var oldTerraces = new List<SettlementTerrace>(layout.terraces);
        var originalGroups = new List<List<SettlementPlot>>();
        for (int t = 0; t < oldTerraces.Count; t++)
        {
            var bases = layout.plots.FindAll(p => p.terrace == t && p.level == 0);
            bases.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            originalGroups.Add(bases);
        }
        layout.terraces.Clear(); layout.connections.Clear();
        var placed = new List<SettlementPlot>();
        int district = 0;
        for (int source = 0; source < oldTerraces.Count; source++)
        for (int group = 0; group < (source == 0 ? 1 : (originalGroups[source].Count + 1) / 2); group++)
        {
            var bases = source == 0 ? originalGroups[source] : originalGroups[source].GetRange(group * 2, Mathf.Min(2, originalGroups[source].Count - group * 2));
            bool west = district % 2 == 0;
            var terrace = new SettlementTerrace { id = oldTerraces[source].id + (group == 0 ? "" : "/east"),
                left = source == 0 ? 4 : west ? 5 + rng.Next(0, 5) : 53 + rng.Next(0, 4),
                right = source == 0 ? 102 : west ? 48 + rng.Next(0, 4) : 98 + rng.Next(0, 5), y = 0 };
            if (source > 0)
            {
                foreach (var lower in placed)
                    if (lower.position.x + lower.width / 2 >= terrace.left && lower.position.x - lower.width / 2 <= terrace.right)
                        terrace.y = Mathf.Max(terrace.y, lower.position.y + (lower.IsOpen ? 1 : lower.height));
                terrace.y += 2 + (float)rng.NextDouble() * 2;
                district++;
            }
            int t = layout.terraces.Count;
            layout.terraces.Add(terrace);
            layout.connections.Add(new SettlementConnection { id = oldTerraces[source].id + "/access/" + group,
                fromTerrace = -1, toTerrace = t, x = terrace.right - .85f });
            float stride = (terrace.right - terrace.left) / bases.Count;
            for (int col = 0; col < bases.Count; col++)
            {
                var root = bases[col];
                float x = terrace.left + stride * (col + .5f);
                if (!root.IsRequired) x += (float)(rng.NextDouble() - .5) * 2;
                var chain = root;
                float y = terrace.y;
                while (chain != null)
                {
                    chain.width = chain.IsRequired ? 19 : 7.2f + (chain.familyId[^1] - '0') * .7f;
                    chain.height = chain.IsRequired ? 4.5f : 6f;
                    chain.position = new Vector2(x, y);
                    chain.terrace = t;
                    foreach (var socket in chain.sockets)
                    {
                        socket.offset.x *= 2;
                        if (socket.kind == SettlementSocketKind.Decoration && !chain.IsOpen) socket.offset.y = chain.height - .6f;
                    }
                    y += chain.height;
                    placed.Add(chain);
                    chain = layout.plots.Find(p => p.parentPlotId == chain.id);
                }
            }
        }
        layout.version = 3;
    }

    private static void NormalizeHouseStacks(NodeSettlementManifest layout)
    {
        foreach (var plot in layout.plots) if (!plot.IsRequired) plot.height = 6;
        foreach (var plot in layout.plots)
            if (plot.level > 0)
            {
                var parent = layout.plots.Find(p => p.id == plot.parentPlotId);
                plot.position = new Vector2(parent.position.x, parent.position.y + parent.height);
            }
        layout.version = 3;
    }

    // Unity inline serialization cannot reliably preserve null custom-class fields.
    // Older saves may therefore contain an empty shell rather than a missing manifest.
    // A manifest with any identity/layout data must remain intact, even if invalid.
    public static bool IsUninitialized(NodeSettlementManifest layout) => layout == null ||
        (layout.version == 0 || layout.version == 1 || layout.version == 2 || layout.version == 3) &&
        string.IsNullOrEmpty(layout.nodeStableId) && string.IsNullOrEmpty(layout.archetypeId) &&
        layout.seed == 0 && layout.visualPopulationCapacity == 0 && layout.harborArrival == Vector2.zero &&
        (layout.terraces == null || layout.terraces.Count == 0) &&
        (layout.plots == null || layout.plots.Count == 0) &&
        (layout.connections == null || layout.connections.Count == 0);
}

