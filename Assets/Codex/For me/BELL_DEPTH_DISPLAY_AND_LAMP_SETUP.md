# Diving bell depth display + proof-of-concept lamp

2026-10-04 — Bosun 🍌

Added two components. No saved prefab, scene, material, layer or Inspector settings were edited. This is a bell fixture for testing, not a portable item or powered module.

## Depth display setup

1. Open `Assets/Resources/Prefabs/Items/Tetherable/BasicDivingBell.prefab` in Prefab Mode. Create a child named `DepthDisplay` at the desired position on the interior panel.
2. Add **DivingBellDepthDisplay**. It requires/adds a world-space **TextMeshPro** component (not TextMeshProUGUI or a Canvas). Select the LiberationSans SDF font if it is not already assigned. Set text alignment, font size and RectTransform size to suit the panel; position/scale it yourself.
3. Depth Point is optional: blank measures at the bell root. Assign the bell's bottom opening transform if you prefer the reading at that point. It measures physical vertical distance from the exterior ocean's sampled wave surface, clamped to zero above water. It deliberately does not measure from the trapped-air waterline inside the bell.
4. Air Volume and Presentation resolve from this bell's parent components. You can explicitly assign them. The display uses a private runtime copy of the font material with the unlit TMP Distance Field shader; it does not change a shared font asset. Display Color defaults to pale green.
5. Interior Sorting Order Offset defaults to +1 relative to the bell interior. The previous +110 overlay field is no longer used; existing prefab values do not carry forward into this new field. The label follows the bell's docked/deployed sorting context. It stays visible without providing light to surrounding objects.

## Lamp setup — E toggle

1. Create a child `Lamp` inside the same bell. Assign your sprite to a SpriteRenderer and add **BasicLamp**. Required **Light2D** and **BoxCollider2D** components are added by Unity.
2. Adding/resetting BasicLamp initializes Point Light2D defaults: warm color, intensity 1, inner radius 1, outer radius 8 physical units, full-circle 360-degree illumination, and all current sorting layers. It sets the box collider to a trigger. These are component Reset defaults, not changes Bosun has made to your prefab.
3. Resize the trigger to the fixture/switch. Put it on an interaction layer already used by the bell's other control buttons so the player's Interactor2D sees it. It should have no separate Rigidbody2D and no solid collider.
4. Add the fixture's SpriteRenderer to **DivingBellVisualPresentation → Interior Renderers**, so its artwork follows the bell's sorting changes. The Point Light2D itself can target all sorting layers for this test. Ensure the camera renders the lamp GameObject's layer too.
5. Keep **Starts On** enabled initially. Aim at the fixture and press E within the default 1.5-unit use range to toggle. Optional Prompt Anchor allows a separate switch position. Optional Off Sprite replaces the same fixture artwork while off; leave it empty to keep one sprite.
6. Light settings are edited on **Light2D** itself: adjust radius, intensity and color there. Keep it as a Point light for compatibility with the ocean/terrain/bell-water depth sampler. A spotlight is also possible by adjusting inner/outer angles and orienting local +Y toward the beam.

## Limits and tests

The lamp is always powered, uses no inventory definition or fuel, and resets to Starts On when recreated. Its on/off state is not save-persistent or network-replicated in this proof of concept. Toggling respects GameplayAuthority, so non-authoritative clients do not mutate its state. Existing interaction filtering allows fixtures parented to the occupied bell.

Production C# compilation passes. The actual prefab layout and E interaction need your live test after wiring. At 100m/200m/300m, check the readout with lamp off, then toggle the lamp and compare bell/player sprites, internal water, seabed and rope. Outside the lamp radius the deep world should remain dark; the readout remains visible either way. Return to the surface and verify daylight recovers. Commit after this check passes.

## Follow-up fixes: lamp off and readout layering

EnvironmentDepthLighting no longer allows a duplicate scene service to steal its singleton binding before being destroyed. The component bound to ServiceRoot's brightness service reclaims ownership each update. This prevents a surviving ambient driver from treating missing singleton state as full ambient brightness.

BasicLamp now switches intensity to zero while retaining the Point Light2D registration, restores its prior intensity on E-on, and removes illumination when the BasicLamp component is disabled. Shader lighting refreshes immediately on a switch/control-disable. Disabling Light2D or the entire fixture is filtered out by the lighting scan. This does not change saved Inspector settings.

Depth text uses normal bell-interior order +1, a transparent world material queue, and depth testing. Its unlit material remains independent of illumination, but foreground artwork/water can cover or tint it like other physical panel content. Tune Interior Sorting Order Offset if your panel artwork uses other offsets; avoid the old +110 override.

Validation: full production C# compile; isolated production-component tests for duplicate ownership, zero ambient after duplicate destruction, E-off/on and all three Inspector disable paths; rendered bell-water pixels and eight D3D11 shader compilations pass. Harness service/Light2D hosts are adapted. Readout occlusion and the assembled scene still require a live check. At 300m or deeper with no other lamps, switch the lamp off; at 100m some ambient is intentionally retained.

