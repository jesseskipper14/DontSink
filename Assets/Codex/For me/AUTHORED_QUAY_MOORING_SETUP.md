# Authored quay seabed and mooring — revised setup

2026-10-05 — Bosun 🍌. This replaces the rejected automatic-harbor approach. Only seabed shaping and connecting authored moors are implemented. You own the quay, land elevation, dock geometry, boat spawn placement and all marker sprites. No saved scene, prefab or material was edited by this pass.

## Seabed Inspector setup

On the existing **SeaFloor / NodeGroundGenerator2D**:

1. Enable **Use Quay Seabed**. It is off by default, so existing scenes retain their old generation.
2. Start with **Quay Drop Length = 12**, **Quay Drop Depth = 25**, **Quay Curve Samples = 48**. These are ground-local units. The drop starts at the existing **Island Length** shoreline. Align your authored quay with that location.
3. The default **Quay Depth Curve** drops steeply early, rounds out near the foot, and joins the existing gentler slope. You can edit the curve: X is normalized distance through the drop; Y is normalized depth. Keep endpoints (0,0) and (1,1), with flat end tangents, to preserve smooth joins.
4. Existing **Slope Length / Slope Drop** control the gentler slope after that foot. The extra quay depth is added to the existing drop: final base Y = Land Y − Quay Drop Depth − Slope Drop. There is no flat dredged rectangle or automatic boat-size adjustment. Allow enough World Width for the drop plus the gentler slope.
5. Existing slope deformation adds subtle natural variation, faded out at the joins. Extra samples resolve the sharp curve without a single hard vertical edge.

Continue tuning **Land Y** or your authored ground placement yourself. Generated town placement already uses that datum; authored buildings and anchors remain yours to position. No new town-height manager was added.

## Mooring Inspector setup

1. Place your single “tie here” sprite twice on the authored dock/quay. Add **MooringPoint2D** to each marker. Set one **Role = Aft**, the other **Role = Forward**. Keep them together under an authored dock/moors root.
2. Place that sprite twice as children of the boat prefab. Add **MooringPoint2D**, again one Aft and one Forward. The boat parent identifies them as boat moors; no tags or special marker prefab are needed. You choose their positions.
3. Add **NodeSceneMooring** to a NodeScene scene object. Assign **Dock Root** to the dock/moors root; if it is on that root, leave the field empty. Leave **Node Scene Name = NodeScene** unless your scene is named differently.
4. Assign **Rope Material** to the existing `Assets/Resources/Materials/Rope/Rope_Material.mat`. Defaults: Slack 0.5, Rope Width 0.075, Maximum Attach Distance 60, sorting WorldDock order 5. Set sprite sorting yourself.
5. Each dock moor has an optional **Rope Length**. Zero measures current separation plus controller Slack when tying. For repeatable length across save/reload, set a fixed positive length after tuning. A line shorter than the current separation is refused instead of pulling the boat violently into place.

The controller waits for the spawned boat, finds one matching pair of enabled boat moors near the dock, and ties exactly two ropes. It never creates marker sprites, moves the boat, edits the dock or creates seabed geometry. Missing/duplicate roles and unsuitable length are reported in its **Status** field. Disable the controller to remove both ropes/restraints immediately.

## Behavior and testing

- Automatic max-distance joints follow the same physical approach as the existing tether constraint, with small visual sag. Winch payload/inventory/break behavior is not involved, and existing winch code was not modified.
- Buoyancy and waves remain active. Fore/aft placement and slack determine how much bobbing/rotation is allowed; keep the two boat anchors well separated.
- Throttle stays neutral while tied. Keyboard, presets and direct thrust are blocked with “Cannot throttle while docked.” Engines are not powered off; rudder remains available.
- Successful Embark releases the ropes immediately before the scene change. Disabling/unloading the controller also cleans them up.
- Existing boat spawn/save restore is untouched. Loading NodeScene discovers the restored moors and recreates the ropes. Fixed authored rope lengths avoid remeasuring slack on each reload. If an old save overlaps your newly authored quay, adjust/test spawn and save placement yourself; this code does not teleport the hull.
- Only the existing authoritative gameplay mode creates physical joints. Replica mode renders the ropes; authority loss/gain removes/recreates constraints. No network transport or mooring-state replication protocol was added.

Test the curve first, then place moors and test both ropes with your actual boat/waves. Check walking/boarding around your authored quay, saved docked reload, missing/disabled moors, controller disable, and Embark followed by normal throttle. Commit after these checks pass.

## Files / verification

New: `MooringPoint2D.cs`, `NodeSceneMooring.cs` under Assets/Scripts/Travel/Travel, plus their .meta files.

Modified: NodeGroundGenerator2D (optional curve), BoatPilotingSimulation and ThrottleForce (moored throttle guard), SceneTransitionController (Embark release). BoatSpawner, NodeSettlementScene, resource generation, existing tether scripts and authored assets were not changed.

Full production compilation passed. Isolated Unity Play Mode checks cover unchanged land, smooth/deep curve joins, exact old-profile restoration when disabled, two discovered ropes without generated markers or boat teleporting, retained bobbing with restrained drift, authority changes, throttle rejection and release. The isolated test uses representative boat/engine/state hosts and sinusoidal forcing; actual scene art, waves, buoyancy and boarding still need your playtest.