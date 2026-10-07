using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Trusted host seam. Future network requests must authenticate the exact actor/station,
/// never accept a client-provided position, contract payload, elapsed time or completed-zone list.</summary>
public static class SurfaceSurveyAuthority
{
    private sealed class OfferSet
    {
        public SurfaceSurveyBook book;
        public List<SurfaceSurveyContract> offers;
    }
    private sealed class Reading
    {
        public GameObject actor;
        public ObservationTelescopeInteractable telescope;
        public SurfaceSurveyBook book;
        public SurfaceSurveyContract contract;
        public WorldMapKnowledgeSource source;
        public double started;
        public float duration;
        public int confirmed;
    }
    private static readonly Dictionary<string, OfferSet> offersByNode = new();
    private static readonly Dictionary<string, Reading> readings = new();
    private static bool paying;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { offersByNode.Clear(); readings.Clear(); paying = false; }
    public static SurfaceSurveyBook Book
    {
        get
        {
            var snapshot = GameState.I?.worldMapSnapshot;
            if (snapshot == null) return null;
            snapshot.surfaceSurveys ??= new SurfaceSurveyBook();
            snapshot.surfaceSurveys.EnsureDefaults(); return snapshot.surfaceSurveys;
        }
    }

    public static List<SurfaceSurveyContract> RefreshOffers(WorldMapKnowledgeSource source, string nodeId, out string reason)
    {
        reason = "Survey world or station is not ready.";
        var book = Book;
        if (!GameplayAuthority.IsAuthoritative || book == null || book.version != 1 || source == null ||
            !source.TryGetSurfaceSurveyWorld(out var field, out float sea) || !HarborTravelService.TryGetNode(nodeId, out var node))
            return new();
        var sky = UnityEngine.Object.FindFirstObjectByType<CelestialFieldSource>();
        var renderer = UnityEngine.Object.FindFirstObjectByType<CelestialSkyRenderer>();
        if (sky == null || sky.FixedSky != null || !sky.EnsureField() || sky.Field.WorldSeed != field.Seed ||
            sky.Field.WorldBounds != field.WorldBounds || renderer?.ProjectionSettings == null)
        { reason = "The gameplay sky and projection settings are needed for survey reference cards."; return new(); }
        var offers = SurfaceSurveyContractGenerator.Generate(field, sea, source.State, book, nodeId,
            node.displayName, node.position, source.SurfaceSurveySettings);
        foreach (var offer in offers)
        {
            AddOtherEndpoint(offer, source.State, field, source.SurfaceSurveySettings.maximumReach);
            BuildSkyCards(offer, sky.Field, renderer.ProjectionSettings);
        }
        offersByNode[nodeId] = new OfferSet { book = book, offers = offers };
        reason = offers.Count > 0 ? "Choose a survey job. Each has three independent reading areas." : "No suitable unknown-water jobs within this station's reach.";
        // UI receives detached data; acceptance always resolves the host's original offer.
        return offers.ConvertAll(c => c.Copy());
    }

    private static void AddOtherEndpoint(SurfaceSurveyContract contract, WorldMapKnowledgeState knowledge,
        WorldMapTopographyField field, float reach)
    {
        var graph = HarborTravelService.CurrentGraph;
        if (graph?.nodes == null) return;
        float best = reach; var topology = new WorldTopology(field.WorldBounds);
        foreach (var node in graph.nodes)
        {
            if (node == null) continue;
            string id = WorldMapStableIdUtility.BuildNodeStableId(graph.seed, node);
            if (id == contract.originNodeId || !knowledge.HasNodeMarker(id)) continue;
            float distance = topology.Delta(contract.zones[1].center, node.position).magnitude;
            if (distance < best || distance == best && string.CompareOrdinal(id, contract.otherNodeId) < 0)
            { best = distance; contract.otherNodeId = id; contract.otherNodeName = node.displayName; }
        }
    }

    private static void BuildSkyCards(SurfaceSurveyContract contract, CelestialField field, CelestialSkyProjectionSettings settings)
    {
        contract.celestialVersion = field.Identity.generatorVersion;
        contract.celestialConfigHash = field.Identity.configHash;
        var objects = new List<CelestialObject>();
        foreach (var zone in contract.zones)
        {
            field.Query(CelestialSkyProjection.BuildVisibleWorldRect(field.WorldBounds, zone.center, settings), objects);
            objects.Sort((a, b) => { int c = b.Kind.CompareTo(a.Kind); if (c == 0) c = b.Brightness01.CompareTo(a.Brightness01); return c != 0 ? c : string.CompareOrdinal(a.StableId, b.StableId); });
            foreach (var star in objects)
            {
                if (star.Kind != CelestialObjectKind.AmbientStar && star.Kind != CelestialObjectKind.LandmarkStar) continue;
                if (!CelestialSkyProjection.TryProjectToViewport(field.WorldBounds, zone.center, star.WorldPosition,
                    settings, out var point, out _) || !SurfaceSurveyUI.TryProjectReference(point, out _)) continue;
                zone.sky.Add(new SurfaceSurveySkyPoint { viewport = point, brightness = star.Brightness01,
                    landmark = star.Kind == CelestialObjectKind.LandmarkStar,
                    color = settings.ResolveColor(star.ColorClass, star.Kind == CelestialObjectKind.LandmarkStar ? settings.landmarkColorSaturation : settings.ambientColorSaturation) });
                if (zone.sky.Count >= 64) break;
            }
        }
    }

    public static bool TryAccept(WorldMapKnowledgeSource source, string nodeId, string offerId, out string reason)
    {
        reason = "Survey offer is unavailable or stale.";
        var book = Book;
        if (!GameplayAuthority.IsAuthoritative || book == null || book.version != 1 || source == null ||
            !offersByNode.TryGetValue(nodeId, out var set) || !ReferenceEquals(set.book, book) ||
            !source.TryGetSurfaceSurveyWorld(out var field, out float sea)) return false;
        var offer = set.offers.Find(c => c.id == offerId);
        if (offer == null || !offer.IsEndpoint(nodeId) || !offer.IsRegistered(field) || book.Find(offerId) != null) return false;
        int active = book.contracts.FindAll(c => c != null && !c.rewardIssued).Count;
        if (active >= Mathf.Clamp(source.SurfaceSurveySettings.maximumActiveContracts, 1, 8))
        { reason = "Finish an active survey before accepting another."; return false; }
        foreach (var zone in offer.zones)
            if (!SurfaceSurveyContractGenerator.IsUnknownWater(field, sea, source.State, zone.center))
            { reason = "These offered waters are already charted. Refresh Survey Work."; return false; }
        book.contracts.Add(offer.Copy()); set.offers.Remove(offer);
        reason = "Survey accepted. Use the deployed Telescope's Survey action at the instructed reading areas.";
        return true;
    }

    public static bool TryTurnIn(GameObject requester, WorldMapKnowledgeSource source, string nodeId, string contractId, out string reason)
    {
        reason = "Survey is not ready for turn-in at this station.";
        var book = Book; var contract = book?.Find(contractId);
        if (!GameplayAuthority.IsAuthoritative || book?.version != 1 || paying || requester == null || source == null || contract == null ||
            contract.rewardIssued || !contract.IsComplete || !contract.IsEndpoint(nodeId) ||
            !source.TryGetSurfaceSurveyWorld(out var field, out _) || !contract.IsRegistered(field)) return false;
        var inventory = CelestialChartPaperConsumption.ResolveInventory(requester);
        var definition = Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart");
        if (inventory == null || definition == null) { reason = "Chart carrier or inventory unavailable."; return false; }
        var item = ItemInstance.Create(definition); item.SetCartographicChart(contract.reward);
        paying = true; contract.rewardIssued = true;
        try
        {
            bool added;
            try { added = inventory.TryAddInstance(item); }
            catch (Exception error)
            {
                // Inventory observers can throw after the item was committed. Never issue a second reward.
                added = CartographicChartIntegration.Collect(requester).Exists(c => c.Item.InstanceId == item.InstanceId);
                Debug.LogException(error);
            }
            if (!added) { contract.rewardIssued = false; reason = "Make room for the processed survey chart first."; return false; }
            reason = "Processed survey chart issued. Integrate it at the Mapping Table."; return true;
        }
        finally { paying = false; }
    }

    private static bool ValidObserver(GameObject actor, ObservationTelescopeInteractable telescope)
        => actor != null && actor.activeInHierarchy && telescope != null && telescope.isActiveAndEnabled &&
           telescope.Equipment.IsDeployed && telescope.IsValidUser(actor) && telescope.Clearance.HasClearance() &&
           actor.GetComponentInParent<BoatObservationPresentationController>()?.IsObserving(telescope) == true;

    public static bool TryBeginReading(GameObject actor, ObservationTelescopeInteractable telescope, string contractId,
        WorldMapKnowledgeSource source, out string token, out string reason)
    {
        token = null; reason = "Survey observation is unavailable.";
        var book = Book; var contract = book?.Find(contractId);
        if (!GameplayAuthority.IsAuthoritative || book?.version != 1 || !ValidObserver(actor, telescope) || contract == null ||
            contract.rewardIssued || contract.IsComplete || source == null || !source.SurfaceSurveySettings.IsValid ||
            !source.TryGetSurfaceSurveyWorld(out var field, out _) || !contract.IsRegistered(field)) return false;
        // No geographic check here: Survey remains available anywhere while a job is active.
        var sky = UnityEngine.Object.FindFirstObjectByType<CelestialFieldSource>();
        if (sky == null || sky.FixedSky != null || !sky.EnsureField() || sky.Field.WorldSeed != field.Seed ||
            sky.Field.Identity.generatorVersion != contract.celestialVersion || sky.Field.Identity.configHash != contract.celestialConfigHash)
        { reason = "This job's celestial reference does not match the current sky."; return false; }
        CancelForActor(actor);
        token = Guid.NewGuid().ToString("N");
        readings[token] = new Reading { actor = actor, telescope = telescope, book = book, contract = contract, source = source,
            started = Time.realtimeSinceStartupAsDouble, duration = Mathf.Clamp(source.SurfaceSurveySettings.observationSeconds, 3, 30) };
        reason = "Align the reticle with each horizon target and confirm."; return true;
    }

    public static float SecondsUntilTarget(string token)
        => token != null && readings.TryGetValue(token, out var reading)
            ? Mathf.Max(0, (float)(reading.started + reading.duration * (reading.confirmed + 1) / 3d - Time.realtimeSinceStartupAsDouble)) : 0;

    public static bool TryConfirmTarget(GameObject actor, ObservationTelescopeInteractable telescope, string token,
        int target, out bool finished, out string reason)
    {
        finished = false; reason = "Observation interrupted.";
        if (!GameplayAuthority.IsAuthoritative || token == null || !readings.TryGetValue(token, out var reading) ||
            reading.actor != actor || reading.telescope != telescope) return false;
        if (!ReferenceEquals(Book, reading.book) || !ValidObserver(actor, telescope) || reading.source == null ||
            !reading.source.TryGetSurfaceSurveyWorld(out var field, out _) || !reading.contract.IsRegistered(field) ||
            reading.contract.rewardIssued || reading.contract.IsComplete)
        { readings.Remove(token); return false; }
        if (target != reading.confirmed || SecondsUntilTarget(token) > 0)
        { reason = "Hold the observation a little longer before confirming."; return false; }
        reading.confirmed++;
        if (reading.confirmed < 3) { reason = "Target confirmed. Align the next target."; return true; }
        readings.Remove(token); finished = true;
        // Truth comes from the authoritative navigation service, not caller-provided coordinates.
        bool correct = WorldNavigationService.TryGetTrueWorldPosition(out var position) && reading.contract.TryRecord(position);
        reason = correct ? "Correct Survey Reading" : "Incorrect Survey Reading";
        return true;
    }
    public static void CancelReading(GameObject actor, string token)
    { if (token != null && readings.TryGetValue(token, out var reading) && reading.actor == actor) readings.Remove(token); }
    private static void CancelForActor(GameObject actor)
    {
        var remove = new List<string>(); foreach (var pair in readings) if (pair.Value.actor == null || pair.Value.actor == actor) remove.Add(pair.Key);
        foreach (var key in remove) readings.Remove(key);
    }
}
