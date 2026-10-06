using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-owned reconstruction. No saved prefab or Inspector wiring is changed.</summary>
[DisallowMultipleComponent]
public sealed partial class NodeSettlementScene : MonoBehaviour
{
    public static NodeSettlementScene Current { get; private set; }
    public NodeSettlementManifest Layout { get; private set; }
    public SettlementVisitSnapshot Visit { get; private set; }
    public string Status { get; private set; } = "Waiting for node state";
    public bool showDebugAnchors;
    private NodeGroundGenerator2D _ground;
    private Transform _root;
    private Sprite _shape;
    private Texture2D _texture;
    private Material _material;
    private NodeSettlementManifest _loadedLayout;
    private SettlementVisitSnapshot _loadedVisit;
    private GameObject _ladderPrefab, _marketPrefab, _npcPrefab;
    private AgentDefinition _townNpcDefinition;
    private readonly Dictionary<SettlementRole, List<Transform>> _anchors = new();
    private readonly Dictionary<string, Transform> _sockets = new();
    private static readonly Color[] Palette = {
        new(.65f,.3f,.24f), new(.28f,.48f,.57f), new(.72f,.61f,.32f),
        new(.38f,.56f,.36f), new(.53f,.42f,.64f), new(.73f,.51f,.37f), new(.58f,.65f,.66f) };

    public static void AttachToGround(NodeGroundGenerator2D ground)
    {
        Transform context = null;
        foreach (var root in ground.gameObject.scene.GetRootGameObjects())
            if (root.name == "NodeContext") { context = root.transform; break; }
        if (context == null)
        {
            var go = new GameObject("NodeContext");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, ground.gameObject.scene);
            context = go.transform;
        }
        var view = context.Find("NodeView");
        if (view == null) { view = new GameObject("NodeView").transform; view.SetParent(context, false); }
        var town = view.Find("NodeTown");
        if (town == null) { town = new GameObject("NodeTown").transform; town.SetParent(view, false); }
        var scene = town.GetComponent<NodeSettlementScene>();
        if (scene == null) scene = town.gameObject.AddComponent<NodeSettlementScene>();
        scene._ground = ground;
    }

    private void Awake()
    {
        if (Current != null && Current != this && Current.gameObject.scene == gameObject.scene)
        { enabled = false; return; }
        Current = this;
    }

    private IEnumerator Start()
    {
        if (_ground == null) _ground = GetComponent<NodeGroundGenerator2D>();
        while (isActiveAndEnabled)
        {
            var gs = GameState.I;
            var binder = FindAnyObjectByType<WorldMapRuntimeBinder>(FindObjectsInactive.Include);
            if (gs?.player != null && binder != null && binder.IsBuilt &&
                binder.Registry.TryGetByStableId(gs.player.currentNodeId ?? "", out var runtime) && runtime != null)
            {
                Layout = NodeSettlementPlanner.Ensure(runtime);
                if (Layout != null)
                {
                    if (!NodeSettlementPlanner.Validate(Layout, out string reason))
                    { Status = reason; Debug.LogError($"[Settlement] {reason}", this); yield break; }
                    Visit = NodeSettlementPlanner.Evaluate(runtime.State, Layout, GameplayAuthority.IsAuthoritative);
                    _loadedLayout = Layout;
                    _loadedVisit = Visit;
                    ResolveNatureLatitude(runtime);
                    Build();
                    Status = $"{Layout.archetypeId}: {Layout.terraces.Count} terraces, {Layout.plots.Count} permanent plots";
                    Debug.Log($"[Settlement] {Status} | node={Layout.nodeStableId} | version={Layout.version}", this);
                    yield break;
                }
            }
            yield return null;
        }
    }

    private void Build()
    {
        float townLeft = Layout.harborArrival.x;
        foreach (var terrace in Layout.terraces) townLeft = Mathf.Min(townLeft, terrace.left);
        foreach (var plot in Layout.plots) townLeft = Mathf.Min(townLeft, plot.position.x - plot.width / 2 - 1);
        if (_ground != null) _ground.EnsureTownLandwardClearance(Layout.harborArrival.x, townLeft, 120);
        _ladderPrefab = Resources.Load<GameObject>("Prefabs/BoatKit/Ladder");
        _marketPrefab = Resources.Load<GameObject>("Prefabs/Node/Market");
        _npcPrefab = Resources.Load<GameObject>("Prefabs/Agents/NpcBase");
        _townNpcDefinition = Resources.Load<AgentDefinition>("SettlementTownNpc");
        if (_root != null) _root.gameObject.SetActive(false);
        Release(_root != null ? _root.gameObject : null);
        ReleaseNatureSprites();
        Release(_shape); Release(_texture); Release(_material);
        _anchors.Clear(); _sockets.Clear(); _material = null;
        _texture = new Texture2D(1, 1) { name = "Settlement shape", filterMode = FilterMode.Point };
        _texture.SetPixel(0, 0, Color.white); _texture.Apply();
        _shape = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader != null) _material = new Material(shader);
        _root = new GameObject($"Settlement_{Layout.nodeStableId}").transform;
        _root.SetParent(_ground != null ? _ground.transform : transform, false);
        // Manifest positions are relative to the harbor edge, not the terrain's far-left boundary.
        // NodeScene's authored ground currently spans 240 units with shore at local X=120.
        float shoreOffset = _ground != null ? _ground.islandLength - Layout.harborArrival.x : 0;
        _root.localPosition = new Vector3(shoreOffset, _ground != null ? _ground.landY : 0, 0);
        _root.SetParent(transform, true);
        BuildNature();
        foreach (var terrace in Layout.terraces)
        {
            var parent = Empty(terrace.id, _root, Vector2.zero);
            Platform("Permanent street", parent, new Vector2((terrace.left + terrace.right) / 2, terrace.y), terrace.right - terrace.left, new Color(.39f,.32f,.25f));
            if (terrace.y > 0) BuildTerraceStructure(terrace, parent);
        }
        foreach (var connection in Layout.connections)
        {
            float low = connection.fromTerrace < 0 ? 0 : Layout.terraces[connection.fromTerrace].y;
            float high = Layout.terraces[connection.toTerrace].y;
            Ladder(connection.id, _root, connection.x, low, high);
        }
        for (int i = 0; i < Layout.plots.Count; i++) BuildPlot(Layout.plots[i], Visit.plots[i]);
        BuildTownProps();
        var harbor = Empty("HarborArrival", _root, Layout.harborArrival);
        AddAnchor(SettlementRole.Harbor, harbor);
        BuildLandwardGate();
        foreach (var sprite in _root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            // Preserve NPC identification/skin colors; do not tint the shared source prefabs.
            if (sprite.GetComponentInParent<AgentController>() != null) continue;
            sprite.color = NodeTownPresentationRules.Tint(sprite.color,Layout.archetypeId);
        }
    }

    private void BuildPlot(SettlementPlot plot, SettlementPlotVisit visit)
    {
        var parent = Empty(plot.id, _root, plot.position);
        // Bounds are stationary siblings of the actors, never children of a moving NPC.
        float walkableWidth = plot.width;
        if (plot.level > 0)
        {
            var support = Layout.plots.Find(p => p.id == plot.parentPlotId);
            walkableWidth = Mathf.Min(walkableWidth, support.width + 1.2f);
        }
        var home = Empty("NpcHomeBounds", parent, new Vector2(0, 1.02f));
        var homeCollider = home.gameObject.AddComponent<BoxCollider2D>();
        homeCollider.isTrigger = true;
        homeCollider.size = new Vector2(Mathf.Max(.5f, walkableWidth - 1.6f), 3);
        var homeBounds = home.gameObject.AddComponent<AgentHomeBounds>();
        homeBounds.Configure(homeCollider);
        AddAnchor(plot.role, parent);
        // Each family supplies its own fixed shape dimensions. These aren't stretched production sprites.
        var identity = parent.gameObject.AddComponent<SettlementSemanticAnchor>();
        identity.Initialize(Layout.nodeStableId, plot.id, plot.role, visit.serviceActive);
        Color body = Palette[plot.palette % Palette.Length];
        Color faded = Color.Lerp(new Color(.25f,.24f,.22f), body, .3f + visit.condition * .7f);
        if (plot.IsOpen)
        {
            Shape(plot.role.ToString(), parent, new Vector2(0, .15f), new Vector2(plot.width, .2f),
                plot.role == SettlementRole.Garden ? new Color(.27f,.4f,.2f) : new Color(.4f,.35f,.27f), 1);
        }
        else if (!visit.developed)
        {
            Shape("Undeveloped foundation", parent, new Vector2(0, .12f), new Vector2(plot.width, .2f), body * .6f, 1);
        }
        else if (visit.damage == SettlementDamage.Destroyed)
        {
            Shape("Persistent rubble", parent, new Vector2(0, .2f), new Vector2(plot.width, .35f), new Color(.2f,.19f,.18f), 1);
        }
        else
        {
            float height = visit.damage == SettlementDamage.Ruined ? plot.height * .5f : plot.height;
            if (plot.IsRequired && visit.damage == SettlementDamage.None && _marketPrefab != null)
                MarketFacade(parent, faded);
            else
            {
            Shape(plot.familyId, parent, new Vector2(0, height / 2), new Vector2(plot.width, height), faded, 1);
            Shape("Door", parent, new Vector2(plot.width * .23f, 1.3f), new Vector2(1.15f, 2.6f), new Color(.16f,.13f,.1f), 2);
            for (int w = 0; w < 2; w++)
            {
                var window = new Vector2(-plot.width * .3f + w * plot.width * .28f, height * .66f);
                Shape(visit.occupied ? "Window" : "Boarded window", parent, window, new Vector2(1.15f,1.2f),
                    visit.occupied ? new Color(.65f,.7f,.52f) : new Color(.24f,.19f,.14f), 2);
                if (!visit.occupied) Shape("Abandonment board", parent, window, new Vector2(.8f,.15f), new Color(.5f,.37f,.22f), 3);
            }
            Shape("Permanent roof cap", parent, new Vector2(0, height + (Layout.plots.Exists(p => p.parentPlotId == plot.id) ? -.1f : .08f)), new Vector2(plot.width + .25f,.2f), Palette[plot.roofPalette % Palette.Length] * .75f, 3);
            if (!Layout.plots.Exists(p => p.parentPlotId == plot.id))
            {
                // Terminal buildings have a pitched roof, with no extra building slot/access.
                for (int side = -1; side <= 1; side += 2)
                {
                    var roof = Shape("Pitched roof", parent, new Vector2(side * plot.width / 4, height + .65f),
                        new Vector2(plot.width * .55f, .3f), Palette[plot.roofPalette % Palette.Length] * .75f, 3);
                    roof.transform.localRotation = Quaternion.Euler(0, 0, -side * 18);
                }
            }
            if (plot.roofFamily == "metal")
                for (float x = -plot.width / 2; x < plot.width / 2; x += .6f)
                    Shape("Metal roof seam", parent, new Vector2(x, height + .1f), new Vector2(.06f,.23f), new Color(.35f,.37f,.38f), 4);
            }
            if (visit.damage != SettlementDamage.None) Shape("Damage scar", parent, new Vector2(-.4f, .8f), new Vector2(.25f, 1.4f), new Color(.1f,.1f,.1f), 4);
            if (plot.role == SettlementRole.Surveyor)
            {
                Shape("Surveyor sign", parent, new Vector2(0, 3.2f), new Vector2(2.4f,.6f), new Color(.2f,.65f,.7f), 4);
                // Stable named service point, independent of ambient population.
                var surveyor = Empty("SurveyorSpawn", parent, new Vector2(1.2f, 0));
                SpawnNpc(surveyor, plot.id + "/surveyor", homeBounds);
            }
        }
        // Support owns access and persists when its future upper body is undeveloped.
        bool hasChild = Layout.plots.Exists(p => p.parentPlotId == plot.id);
        if (hasChild && visit.developed)
        {
            var support = Empty("UpperBuildingSupport", parent, Vector2.zero);
            float upperY = Layout.plots.Find(p => p.parentPlotId == plot.id).position.y - plot.position.y;
            Platform("RoofSlot platform", support, new Vector2(0, upperY), plot.width + 1.2f, Palette[plot.roofPalette % Palette.Length] * .65f);
            Ladder("Support-owned ladder", support, plot.width / 2 + .55f, 0, upperY);
        }
        var sockets = new HashSet<string>(visit.activeSockets);
        foreach (var socket in plot.sockets)
        {
            // Inactive sockets still expose stable anchors to later service/quest consumers.
            var anchor = Empty(socket.id, parent, socket.offset);
            _sockets[socket.id] = anchor;
            if (!sockets.Contains(socket.id)) continue;
            bool person = socket.kind == SettlementSocketKind.Civilian || socket.kind == SettlementSocketKind.Merchant || socket.kind == SettlementSocketKind.Worker || socket.kind == SettlementSocketKind.Guard;
            if (person)
            {
                SpawnNpc(anchor, socket.id, homeBounds);
            }
            else
            {
                if (plot.IsOpen && socket.kind == SettlementSocketKind.Decoration)
                    Shape("Banner post", parent, new Vector2(0,.35f), new Vector2(.06f,.7f), new Color(.45f,.34f,.23f), 4);
                Shape(socket.kind.ToString(), anchor, Vector2.zero, new Vector2(.55f,.35f),
                    socket.kind == SettlementSocketKind.Food ? new Color(.75f,.52f,.23f) : Palette[(plot.palette + 3) % Palette.Length], 5);
            }
        }
    }

    private Transform Empty(string name, Transform parent, Vector2 at)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at; return go.transform;
    }

    private void SpawnNpc(Transform anchor, string stableId, AgentHomeBounds bounds)
    {
        if (_npcPrefab == null) { Debug.LogError("[Settlement] Missing NpcBase prefab.", this); return; }
        var npc = Instantiate(_npcPrefab, anchor);
        npc.name = "Town NPC";
        npc.transform.localPosition = new Vector3(0, 1.02f, 0);
        npc.transform.localRotation = Quaternion.identity;
        npc.transform.localScale = Vector3.one;
        var shirt = npc.GetComponent<SpriteRenderer>();
        if (shirt != null) shirt.color = new Color(.12f, .7f, .78f);
        var agent = npc.GetComponent<AgentController>();
        if (agent != null)
        {
            if (_townNpcDefinition == null)
                Debug.LogError("[Settlement] Missing SettlementTownNpc definition.", this);
            agent.Initialize(_townNpcDefinition, stableId, Layout.nodeStableId, bounds);
            AgentRegistry.Register(agent);
        }
    }

    private GameObject Shape(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order, string sortingLayer = "WorldBuildings")
    {
        var item = Empty(name, parent, position);
        item.localScale = new Vector3(size.x, size.y, 1);
        var sprite = item.gameObject.AddComponent<SpriteRenderer>();
        sprite.sprite = _shape; sprite.color = color;
        sprite.sortingLayerName = sortingLayer; sprite.sortingOrder = order;
        if (_material != null) sprite.sharedMaterial = _material;
        return item.gameObject;
    }

    private void Platform(string name, Transform parent, Vector2 at, float width, Color color)
    {
        // Match existing docks: WorldLedge + one-way platform + HatchLedge drop-through behavior.
        var go = Empty(name, parent, at).gameObject;
        int layer = LayerMask.NameToLayer("WorldLedge"); if (layer >= 0) go.layer = layer;
        const float thickness = .4f;
        var box = go.AddComponent<BoxCollider2D>(); box.size = new Vector2(width, thickness); box.offset = new Vector2(0, -thickness / 2); box.usedByEffector = true;
        var effector = go.AddComponent<PlatformEffector2D>(); effector.useOneWay = true; effector.useOneWayGrouping = true; effector.surfaceArc = 160;
        go.AddComponent<HatchLedge>();
        Shape("Walkway", go.transform, new Vector2(0,-thickness / 2), new Vector2(width,thickness), color, 0, "WorldDock");
        Shape("Walkway top trim", go.transform, new Vector2(0,-.04f), new Vector2(width,.08f), Color.Lerp(color, Color.white,.18f), 1, "WorldDock");
    }

    private void Ladder(string id, Transform parent, float x, float bottom, float top)
    {
        if (top - bottom < .1f) return;
        if (_ladderPrefab == null)
        { Debug.LogError("[Settlement] Missing Resources/Prefabs/BoatKit/Ladder prefab.", this); return; }
        var go = Instantiate(_ladderPrefab, parent);
        go.name = id;
        // Auto-exit snaps the player's rigidbody center to the prefab's top marker.
        // Give the player's lower collider enough clearance above the destination ledge.
        float ladderBottom = bottom - .2f;
        float ladderTop = top + 1.6f;
        go.transform.localPosition = new Vector3(x, (ladderBottom + ladderTop) / 2, 0);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.GetComponent<ResizableSegment2D>().ApplyHeight(ladderTop - ladderBottom);
        go.GetComponent<LadderAutoFitAuthoring>().Apply();
    }

    private void BuildLandwardGate()
    {
        // Use the generated boundary's actual position, including Inspector padding.
        // The existing wall owns collision; this gate simply makes the stop visible.
        var wall = _ground != null ? _ground.transform.Find("BoundaryWall_Left") : null;
        if (_ground == null || wall == null || !wall.gameObject.activeInHierarchy) return;
        Vector3 worldBase = _ground.transform.TransformPoint(new Vector3(wall.localPosition.x, _ground.landY, 0));
        var gate = Empty("Landward boundary gate", _root, _root.InverseTransformPoint(worldBase));
        var collider = wall.GetComponent<BoxCollider2D>();
        float width = collider != null ? collider.size.x + .3f : 1.3f;
        Color timber = new(.31f, .23f, .15f);
        Shape("Closed gate", gate, new Vector2(0, 2.7f), new Vector2(width, 5.4f), timber, 5);
        for (int i = 0; i < 6; i++)
            Shape("Gate crossbar", gate, new Vector2(0, .45f + i * .9f), new Vector2(width + .25f, .14f), new Color(.55f,.4f,.23f), 6);
        Shape("Gate cap", gate, new Vector2(0, 5.5f), new Vector2(width + .5f, .3f), new Color(.44f,.31f,.18f), 7);
        Shape("Gate warning plate", gate, new Vector2(0, 3.3f), new Vector2(width * .7f, .65f), new Color(.78f,.61f,.25f), 7);
    }

    private void MarketFacade(Transform parent, Color tint)
    {
        var facade = Empty("Authored Market facade", parent, new Vector2(5, .4f));
        // Copy authored visuals only: duplicating vendor/sell-zone scripts would register
        // extra functional markets and cargo stores. Existing scene services stay in place.
        foreach (var source in _marketPrefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var item = Empty(source.name, facade, _marketPrefab.transform.InverseTransformPoint(source.transform.position));
            item.localRotation = Quaternion.Inverse(_marketPrefab.transform.rotation) * source.transform.rotation;
            item.localScale = source.transform.lossyScale;
            var sprite = item.gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = source.sprite; sprite.sharedMaterial = source.sharedMaterial;
            sprite.color = source.color * tint; sprite.sortingLayerID = source.sortingLayerID;
            sprite.sortingOrder = source.sortingOrder; sprite.drawMode = source.drawMode;
            sprite.size = source.size; sprite.tileMode = source.tileMode;
            sprite.flipX = source.flipX; sprite.flipY = source.flipY;
        }
    }

    private void AddAnchor(SettlementRole role, Transform anchor)
    {
        if (!_anchors.TryGetValue(role, out var list)) _anchors[role] = list = new();
        list.Add(anchor);
    }

    public bool TryGetAnchor(SettlementRole role, out Transform anchor)
    {
        anchor = null;
        if (!_anchors.TryGetValue(role, out var list) || list.Count == 0) return false;
        anchor = list[0]; return anchor != null;
    }

    public bool TryGetSocket(string socketId, out Transform anchor)
    {
        anchor = null;
        return socketId != null && _sockets.TryGetValue(socketId, out anchor) && anchor != null;
    }

    public IReadOnlyList<Transform> GetAnchors(SettlementRole role) =>
        _anchors.TryGetValue(role, out var list) ? list : System.Array.Empty<Transform>();

    [ContextMenu("Debug Preview/Rich populated town (no saved changes)")]
    private void PreviewRich() => Preview(1, 1, 1, 1, 4, false);
    [ContextMenu("Debug Preview/Poor crowded town (no saved changes)")]
    private void PreviewPoorCrowded() => Preview(1, .1f, .3f, .5f, 0, false);
    [ContextMenu("Debug Preview/Rich sparse town (no saved changes)")]
    private void PreviewRichSparse() => Preview(.1f, 1, 1, 1, 4, false);
    [ContextMenu("Debug Preview/Trade collapse (no saved changes)")]
    private void PreviewTradeCollapse() => Preview(1, .5f, .5f, 0, 0, true);
    [ContextMenu("Debug Preview/Food crisis (no saved changes)")]
    private void PreviewFoodCrisis() => Preview(.5f, .4f, .3f, .8f, -4, true);
    [ContextMenu("Debug Preview/Abandoned historical town (no saved changes)")]
    private void PreviewAbandoned() => Preview(0, 0, 0, 0, -4, true);
    [ContextMenu("Debug Preview/Return to loaded visit")]
    private void RestoreVisit()
    {
        if (_loadedLayout == null) return;
        Layout = _loadedLayout; Visit = _loadedVisit; Build();
        Status = "Loaded visit restored";
    }

    private void Preview(float population, float prosperity, float stability, float trade, float food, bool historical)
    {
        if (Layout == null || !Application.isPlaying) return;
        // Never let debug previews write shared layout/history or node simulation state.
        Layout = (_loadedLayout ?? Layout).Copy();
        if (historical) foreach (var plot in Layout.plots) plot.everDeveloped = true;
        var state = new MapNodeState(Layout.nodeStableId) { population = population * Layout.visualPopulationCapacity };
        state.ForceSetStat(NodeStatId.Prosperity, prosperity * 4);
        state.ForceSetStat(NodeStatId.Stability, stability * 4);
        state.ForceSetStat(NodeStatId.TradeRating, trade * 4);
        state.ForceSetStat(NodeStatId.FoodBalance, food);
        Visit = NodeSettlementPlanner.Evaluate(state, Layout, false);
        Build(); Status = "DEBUG preview only; saved layout, history and stats unchanged";
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugAnchors || Layout == null || _root == null) return;
        Gizmos.color = Color.cyan;
        foreach (var p in Layout.plots) Gizmos.DrawWireCube(_root.TransformPoint(p.position), new Vector3(p.width, .3f, 0));
        Gizmos.color = Color.yellow;
        foreach (var c in Layout.connections)
        {
            float low = c.fromTerrace < 0 ? 0 : Layout.terraces[c.fromTerrace].y;
            Gizmos.DrawLine(_root.TransformPoint(new Vector2(c.x, low)), _root.TransformPoint(new Vector2(c.x, Layout.terraces[c.toTerrace].y)));
        }
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        Release(_root != null ? _root.gameObject : null);
        Release(_shape);
        ReleaseNatureSprites();
        Release(_texture);
        Release(_material);
    }

    private void Release(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}
