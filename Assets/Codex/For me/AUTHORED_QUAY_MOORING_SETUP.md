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

## Mooring Inspector setup — single visual rope (2026-10-06)

1. Use one authored MooringPoint2D marker on the quay and one on the boat under _Deck. Role and Rope Length have been removed; they are no longer needed.
2. Add **NodeSceneMooring** to your quay marker GameObject. Leave **Dock Root** empty: it searches that object's children including itself. Alternatively, put the controller on an organizer and assign Dock Root directly to the quay marker. No NodeHarborScene dependency.
3. Leave **Node Scene Name = NodeScene**. Assign **Rope Material** to Assets/Resources/Materials/Rope/Rope_Material.mat (the same material is also the runtime fallback).
4. Defaults: Slack 0.5, Rope Width 0.075, Maximum Attach Distance 60, sorting WorldDock order 5. Adjust sorting/order to sit in front of your authored quay as needed. Slack controls visual sag only.

The controller waits for boat spawning, finds the nearest enabled boat moor in the same scene within attach distance, and renders exactly one rope between the authored markers. If multiple quay markers are found, assign Dock Root to the single intended marker. Status describes waiting/ambiguity/tied states.

## Behavior and testing

- The rope is visual only: no physics joints, forces, hull teleporting or generated dock/marker geometry. Boat buoyancy, rotation and wave movement continue normally; the line follows both markers each LateUpdate.
- Existing moored throttle guards still keep propulsion neutral. Attempting throttle posts “Cannot throttle while docked.” Engines are not powered off; rudder remains available.
- Successful Embark releases the line and prevents it reattaching during the transition. Disabling/unloading the controller removes it. Disabling either marker clears the throttle lock.
- Loading NodeScene discovers the restored/spawned boat marker. Missing markers or a boat outside attach distance leave it untied.
- This uses existing local mooring/throttle integration. It does not add network state replication. The visual controller applies no physics on hosts or replicas.

Inspector settings were not changed automatically. Test your authored rope sorting/material, boat bobbing, blocked throttle, then Embark and normal throttle in BoatScene before committing.

## Verification

Full production runtime compilation passed. Isolated Unity Play Mode checks exercise one-marker discovery, exactly one line, no joints/teleport, endpoint tracking/sag, missing/distant/disabled markers, ambiguous quay markers, throttle lock/message limiting, controller cleanup and Embark release. These use representative state/boat hosts; your actual scene appearance and propulsion integration still need a live check.
