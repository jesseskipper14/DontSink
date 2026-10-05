using System;
using System.Collections.Generic;
using UnityEngine;

public enum SettlementRole { Residence, Market, Surveyor, Warehouse, Industry, Defense, WorkYard, Garden, TownSquare, EventReserve, Harbormaster, Tavern, Harbor }
public enum SettlementDamage { None, Condemned, Ruined, Destroyed }
public enum SettlementSocketKind { Civilian, Merchant, Worker, Guard, Merchandise, Food, Garden, Decoration, Quest }

/// <summary>Saved identity and history, never a serialized scene hierarchy.</summary>
[Serializable]
public sealed class NodeSettlementManifest
{
    public int version = 3;
    public string nodeStableId, archetypeId;
    public int seed;
    public float visualPopulationCapacity;
    public Vector2 harborArrival;
    public List<SettlementTerrace> terraces = new();
    public List<SettlementConnection> connections = new();
    public List<SettlementPlot> plots = new();

    public NodeSettlementManifest Copy() => JsonUtility.FromJson<NodeSettlementManifest>(JsonUtility.ToJson(this));
}

[Serializable]
public sealed class SettlementTerrace
{
    public string id;
    public float left, right, y;
}

[Serializable]
public sealed class SettlementConnection
{
    public string id;
    public int fromTerrace, toTerrace;
    public float x;
}

[Serializable]
public sealed class SettlementPlot
{
    public string id, familyId, parentPlotId, roofFamily;
    public SettlementRole role;
    public int terrace, level, palette, roofPalette;
    public Vector2 position;
    public float width, height;
    public bool everDeveloped, occupied, serviceActive;
    public SettlementDamage damage;
    public List<SettlementSocket> sockets = new();
    public bool IsOpen => role == SettlementRole.WorkYard || role == SettlementRole.Garden ||
        role == SettlementRole.TownSquare || role == SettlementRole.EventReserve;
    public bool IsRequired => role == SettlementRole.Surveyor || role == SettlementRole.Market;
}

[Serializable]
public sealed class SettlementSocket
{
    public string id;
    public SettlementSocketKind kind;
    public Vector2 offset;
    public float threshold;
}

/// <summary>Equivalent data definitions for the first shape-art pass; asset overrides are optional.</summary>
[Serializable]
public sealed class SettlementArchetypeRules
{
    public int minTerraces = 2, maxTerraces = 3, maxStack = 2;
    public float openChance = .25f, stackChance = .35f, populationCapacity = 600;
    public bool industry = true, defensive;

    public static SettlementArchetypeRules For(string archetype)
    {
        string id = (archetype ?? "").ToLowerInvariant();
        if (id.Contains("farming")) return new() { minTerraces = 1, maxTerraces = 2, maxStack = 1, openChance = .55f, stackChance = 0, populationCapacity = 350, industry = false };
        if (id.Contains("fishing")) return new() { minTerraces = 1, maxTerraces = 2, maxStack = 2, openChance = .45f, stackChance = .12f, populationCapacity = 350, industry = false };
        if (id.Contains("fortress")) return new() { minTerraces = 3, maxTerraces = 4, maxStack = 3, openChance = .15f, stackChance = .4f, populationCapacity = 1000, defensive = true };
        if (id.Contains("shipyard") || id.Contains("trade_hub")) return new() { minTerraces = 2, maxTerraces = 3, maxStack = 3, openChance = .2f, stackChance = .35f, populationCapacity = 900 };
        return new();
    }
}

public sealed class SettlementPlotVisit
{
    public string plotId;
    public bool developed, occupied, serviceActive;
    public SettlementDamage damage;
    public float condition, activity, dressing;
    public List<string> activeSockets = new();
}

/// <summary>Immutable for the visit: rendering doesn't read live simulation stats.</summary>
public sealed class SettlementVisitSnapshot
{
    public float population, prosperity, stability, security, trade, food;
    public List<string> flags = new(), activeBuffIds = new();
    public List<SettlementPlotVisit> plots = new();
}
