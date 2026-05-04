using System.Collections.Generic;
using UnityEngine;
using MiniGames;

public sealed class ModuleCartridge : IMiniGameCartridge, IOverlayRenderable
{
    private readonly Hardpoint _hardpoint;

    private MiniGameContext _ctx;
    private bool _requestedClose;

    private PlayerInventory _playerInventory;
    private PlayerEquipment _playerEquipment;

    private string _uiNote;

    private static readonly BottomBarSlotType[] EquipmentSlots =
    {
        BottomBarSlotType.Hands,
        BottomBarSlotType.Head,
        BottomBarSlotType.Feet,
        BottomBarSlotType.Toolbelt,
        BottomBarSlotType.Backpack,
        BottomBarSlotType.Body
    };

    public ModuleCartridge(Hardpoint hardpoint)
    {
        _hardpoint = hardpoint;
    }

    public void Begin(MiniGameContext context)
    {
        _ctx = context ?? new MiniGameContext();
        _requestedClose = false;
        _uiNote = null;

        _playerInventory = Object.FindFirstObjectByType<PlayerInventory>();
        _playerEquipment = Object.FindFirstObjectByType<PlayerEquipment>(FindObjectsInactive.Include);
    }

    public MiniGameResult Tick(float dt, MiniGameInput input)
    {
        if (_requestedClose)
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = "Closed",
                hasMeaningfulProgress = false
            };
        }

        if (_hardpoint == null || !_hardpoint.HasInstalledModule || _hardpoint.InstalledModule == null)
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = "Module missing",
                hasMeaningfulProgress = false
            };
        }

        return new MiniGameResult
        {
            outcome = MiniGameOutcome.None,
            quality01 = 1f,
            note = null,
            hasMeaningfulProgress = false
        };
    }

    public MiniGameResult Cancel()
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = "Cancelled",
            hasMeaningfulProgress = false
        };
    }

    public MiniGameResult Interrupt(string reason)
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = $"Interrupted: {reason}",
            hasMeaningfulProgress = false
        };
    }

    public void End()
    {
        _ctx = null;
    }

    public void DrawOverlayGUI(Rect panel)
    {
        float pad = 14f;

        InstalledModule installed = _hardpoint != null ? _hardpoint.InstalledModule : null;
        ModuleDefinition def = installed != null ? installed.Definition : null;
        EngineModule engine = installed != null ? installed.GetComponent<EngineModule>() : null;
        PumpModule pump = installed != null ? installed.GetComponent<PumpModule>() : null;
        GeneratorModule generator = installed != null ? installed.GetComponent<GeneratorModule>() : null;
        TurretModule turret = installed != null ? installed.GetComponent<TurretModule>() : null;

        string title = def != null ? def.DisplayName : "Module";
        GUI.Label(new Rect(panel.x + pad, panel.y + 10f, panel.width - 80f, 24f), $"{title} [{_hardpoint?.HardpointId}]");

        if (GUI.Button(new Rect(panel.xMax - 40f, panel.y + 8f, 28f, 24f), "X"))
        {
            _requestedClose = true;
            return;
        }

        if (_hardpoint == null || installed == null)
        {
            GUI.Label(new Rect(panel.x + pad, panel.y + 40f, panel.width - pad * 2f, 22f), "No installed module.");
            return;
        }

        float contentX = panel.x + pad;
        float contentY = panel.y + 42f;
        float contentW = panel.width - pad * 2f;
        float contentH = panel.height - 54f;

        float leftW = contentW * 0.34f;
        float middleW = contentW * 0.28f;
        float rightW = contentW - leftW - middleW - 20f;

        Rect leftRect = new Rect(contentX, contentY, leftW, contentH);
        Rect middleRect = new Rect(leftRect.xMax + 10f, contentY, middleW, contentH);
        Rect rightRect = new Rect(middleRect.xMax + 10f, contentY, rightW, contentH);

        DrawLeftColumn(leftRect, engine, pump, generator, turret);
        DrawMiddleColumn(middleRect, engine, pump, generator, turret);
        DrawRightColumn(rightRect, engine, pump, generator, turret);

        if (!string.IsNullOrWhiteSpace(_uiNote))
            GUI.Label(new Rect(panel.x + pad, panel.yMax - 26f, panel.width - pad * 2f - 40f, 20f), _uiNote);
    }

    private void DrawLeftColumn(Rect rect, EngineModule engine, PumpModule pump, GeneratorModule generator, TurretModule turret)
    {
        GUI.Box(rect, GUIContent.none);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), $"Hardpoint Type: {_hardpoint.HardpointType}");
        y += 24f;

        if (engine != null)
        {
            GUI.Label(new Rect(x, y, w, 22f), $"Engine State: {(engine.IsOn ? "ON" : "OFF")}");
            y += 28f;

            bool canRun = engine.CanRun();
            GUI.enabled = canRun;

            if (GUI.Button(new Rect(x, y, 120f, 28f), engine.IsOn ? "Turn Off" : "Turn On"))
                engine.Toggle();

            GUI.enabled = true;

            if (!canRun)
                DrawWarningLabel(new Rect(x + 130f, y + 4f, w - 130f, 22f), "No Fuel / Power");

            y += 42f;

            GUI.Label(new Rect(x, y, w, 22f), "Fuel");
            y += 24f;

            DrawFuelContainerSummary(ref y, x, w, engine.FuelContainerItem);
        }
        else if (pump != null)
        {
            GUI.Label(new Rect(x, y, w, 22f), $"Pump State: {(pump.IsOn ? "ON" : "OFF")}");
            y += 28f;

            bool canRun = pump.CanRun();
            GUI.enabled = canRun;

            if (GUI.Button(new Rect(x, y, 120f, 28f), pump.IsOn ? "Turn Off" : "Turn On"))
                pump.Toggle();

            GUI.enabled = true;

            if (!canRun)
            {
                string reason = pump.TargetCompartment == null ? "No target compartment." : "No Power";
                DrawWarningLabel(new Rect(x + 130f, y + 4f, w - 130f, 22f), reason);
            }

            y += 42f;

            GUI.Label(new Rect(x, y, w, 22f), $"Rate: {pump.PumpRatePerSecond:F2} water/sec");
            y += 24f;

            Compartment target = pump.TargetCompartment;
            GUI.Label(new Rect(x, y, w, 22f), $"Target: {(target != null ? target.name : "None")}");
            y += 24f;

            if (target != null)
            {
                GUI.Label(new Rect(x, y, w, 22f), $"Water: {target.WaterArea:F2} / {target.MaxWaterArea:F2}");
                y += 24f;

                float fill01 = target.MaxWaterArea > 0f
                    ? Mathf.Clamp01(target.WaterArea / target.MaxWaterArea)
                    : 0f;

                DrawSimpleBar(new Rect(x, y, Mathf.Min(160f, w), 10f), fill01);
            }
        }
        else if (generator != null)
        {
            GUI.Label(new Rect(x, y, w, 22f), $"Generator State: {(generator.IsOn ? "ON" : "OFF")}");
            y += 28f;

            bool canRun = generator.CanRun();
            GUI.enabled = canRun;

            if (GUI.Button(new Rect(x, y, 120f, 28f), generator.IsOn ? "Turn Off" : "Turn On"))
                generator.Toggle();

            GUI.enabled = true;

            if (!canRun)
            {
                string reason = generator.PowerState == null ? "No BoatPowerState." : "No Fuel";
                DrawWarningLabel(new Rect(x + 130f, y + 4f, w - 130f, 22f), reason);
            }

            y += 42f;

            GUI.Label(new Rect(x, y, w, 22f), $"Output: {generator.PowerGeneratedPerSecond:F1} power/sec");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), "Fuel");
            y += 24f;

            DrawFuelContainerSummary(ref y, x, w, generator.FuelContainerItem);

            BoatPowerState power = generator.PowerState;
            if (power != null)
            {
                GUI.Label(new Rect(x, y, w, 22f), $"Boat Power: {power.CurrentPower:F1} / {power.MaxPower:F1}");
                y += 24f;

                GUI.Label(new Rect(x, y, w, 22f), $"Demand: {power.CurrentDemandPerSecond:F1} / sec");
                y += 24f;

                float production = generator.IsOn ? generator.PowerGeneratedPerSecond : 0f;
                GUI.Label(new Rect(x, y, w, 22f), $"Net: {production - power.CurrentDemandPerSecond:F1} / sec");
                y += 24f;

                DrawSimpleBar(new Rect(x, y, Mathf.Min(160f, w), 10f), power.Normalized);
            }
        }
        else if (turret != null)
        {
            GUI.Label(new Rect(x, y, w, 22f), $"Turret State: {(turret.IsOn ? "ON" : "OFF")}");
            y += 28f;

            bool canRun = turret.CanRun();
            GUI.enabled = canRun;

            if (GUI.Button(new Rect(x, y, 120f, 28f), turret.IsOn ? "Turn Off" : "Turn On"))
                turret.Toggle();

            GUI.enabled = true;

            if (!canRun)
                DrawWarningLabel(new Rect(x + 130f, y + 4f, w - 130f, 22f), "No Power");

            y += 42f;

            GUI.Label(new Rect(x, y, w, 22f), $"Pitch: {turret.PitchDegrees:F1}°");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Fire Cost: {turret.FirePowerCost:F1} power");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Cooldown: {turret.FireCooldown:F2}s");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Can Fire: {(turret.CanFire() ? "YES" : "NO")}");
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 22f), "No specialized module UI yet.");
        }
    }

    private void DrawFuelContainerSummary(ref float y, float x, float w, ItemInstance fuelContainer)
    {
        if (fuelContainer == null)
        {
            GUI.Label(new Rect(x, y, w, 22f), "(No fuel container)");
            y += 24f;
            return;
        }

        GUI.Label(new Rect(x, y, w, 22f), $"Container: {fuelContainer.Definition?.DisplayName ?? "Unknown"}");
        y += 24f;

        ItemContainerState state = fuelContainer.ContainerState;
        if (state == null)
        {
            GUI.Label(new Rect(x, y, w, 22f), "(Container has no state)");
            y += 24f;
            return;
        }

        int filled = 0;
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot != null && !slot.IsEmpty && slot.Instance != null)
                filled++;
        }

        GUI.Label(new Rect(x, y, w, 22f), $"Slots Used: {filled} / {state.SlotCount}");
        y += 24f;

        GUI.Label(new Rect(x, y, w, 22f), "Summary:");
        y += 22f;

        bool any = false;
        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            any = true;
            DrawItemSummaryWithCharges(new Rect(x, y, w, 42f), slot.Instance);
            y += 44f;
        }

        if (!any)
        {
            GUI.Label(new Rect(x, y, w, 22f), "(Empty)");
            y += 24f;
        }
    }

    private void DrawMiddleColumn(Rect rect, EngineModule engine, PumpModule pump, GeneratorModule generator, TurretModule turret)
    {
        float gap = 10f;
        float sectionH = (rect.height - gap * 2f) / 3f;

        Rect durabilityRect = new Rect(rect.x, rect.y, rect.width, sectionH);
        Rect upgradesRect = new Rect(rect.x, durabilityRect.yMax + gap, rect.width, sectionH);
        Rect actionsRect = new Rect(rect.x, upgradesRect.yMax + gap, rect.width, sectionH);

        DrawPlaceholderSection(durabilityRect, "Durability", "Placeholder");
        DrawPlaceholderSection(upgradesRect, "Upgrades", "Placeholder");
        DrawActionsSection(actionsRect, engine, pump, generator, turret);
    }

    private void DrawPlaceholderSection(Rect rect, string title, string body)
    {
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 22f), title);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 32f, rect.width - 20f, 22f), body);
    }

    private void DrawActionsSection(Rect rect, EngineModule engine, PumpModule pump, GeneratorModule generator, TurretModule turret)
    {
        GUI.Box(rect, GUIContent.none);

        float x = rect.x + 10f;
        float y = rect.y + 8f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), "Actions");
        y += 30f;

        if (engine != null)
        {
            if (GUI.Button(new Rect(x, y, 140f, 28f), "Add Fuel"))
                _uiNote = TryAddFuel(engine, out string addNote) ? addNote : addNote;

            y += 36f;

            if (GUI.Button(new Rect(x, y, 140f, 28f), "Remove Fuel"))
                _uiNote = TryRemoveFuel(engine, out string removeNote) ? removeNote : removeNote;

            return;
        }

        if (pump != null)
        {
            if (GUI.Button(new Rect(x, y, 140f, 28f), pump.IsOn ? "Turn Off" : "Turn On"))
            {
                bool result = pump.Toggle();
                _uiNote = result
                    ? (pump.IsOn ? "Pump started." : "Pump stopped.")
                    : "Pump cannot start.";
            }

            y += 36f;

            if (GUI.Button(new Rect(x, y, 140f, 28f), "Resolve Target"))
            {
                pump.ResolveTargetCompartment();
                _uiNote = pump.TargetCompartment != null
                    ? $"Target: {pump.TargetCompartment.name}"
                    : "No target compartment found.";
            }

            return;
        }

        if (generator != null)
        {
            if (GUI.Button(new Rect(x, y, 140f, 28f), generator.IsOn ? "Turn Off" : "Turn On"))
            {
                bool result = generator.Toggle();
                _uiNote = result
                    ? (generator.IsOn ? "Generator started." : "Generator stopped.")
                    : "Generator cannot start: no fuel.";
            }

            y += 36f;

            if (GUI.Button(new Rect(x, y, 140f, 28f), "Add Fuel"))
                _uiNote = TryAddFuel(generator, out string addNote) ? addNote : addNote;

            y += 36f;

            if (GUI.Button(new Rect(x, y, 140f, 28f), "Remove Fuel"))
                _uiNote = TryRemoveFuel(generator, out string removeNote) ? removeNote : removeNote;

            y += 36f;

            if (GUI.Button(new Rect(x, y, 140f, 28f), "Resolve Power"))
            {
                generator.ResolveOwnership();
                _uiNote = generator.PowerState != null
                    ? "Boat power linked."
                    : "No BoatPowerState found.";
            }

            return;
        }

        if (turret != null)
        {
            if (GUI.Button(new Rect(x, y, 140f, 28f), turret.IsOn ? "Turn Off" : "Turn On"))
            {
                bool result = turret.Toggle();
                _uiNote = result
                    ? (turret.IsOn ? "Turret online." : "Turret offline.")
                    : "Turret cannot start: no power.";
            }

            y += 36f;

            if (GUI.Button(new Rect(x, y, 140f, 28f), "Test Fire"))
            {
                bool fired = turret.TryFire();
                _uiNote = fired ? "Fired." : "Cannot fire.";
            }

            y += 36f;

            GUI.Label(new Rect(x, y, w, 22f), "Ammo: Placeholder");

            return;
        }

        GUI.Label(new Rect(x, y, w, 22f), "No actions.");
    }

    private void DrawRightColumn(Rect rect, EngineModule engine, PumpModule pump, GeneratorModule generator, TurretModule turret)
    {
        if (pump != null)
        {
            GUI.Box(rect, GUIContent.none);

            float x = rect.x + 10f;
            float y = rect.y + 8f;
            float w = rect.width - 20f;

            GUI.Label(new Rect(x, y, w, 22f), "Pump Diagnostics");
            y += 28f;

            Compartment target = pump.TargetCompartment;
            if (target == null)
            {
                GUI.Label(new Rect(x, y, w, 22f), "No compartment target.");
                return;
            }

            GUI.Label(new Rect(x, y, w, 22f), $"Compartment: {target.name}");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Water Area: {target.WaterArea:F2}");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Capacity: {target.MaxWaterArea:F2}");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Available: {target.AvailableCapacity:F2}");
            return;
        }

        if (generator != null)
        {
            GUI.Box(rect, GUIContent.none);

            float x = rect.x + 10f;
            float y = rect.y + 8f;
            float w = rect.width - 20f;

            GUI.Label(new Rect(x, y, w, 22f), "Power Diagnostics");
            y += 28f;

            BoatPowerState power = generator.PowerState;
            if (power == null)
            {
                GUI.Label(new Rect(x, y, w, 22f), "No BoatPowerState linked.");
                return;
            }

            GUI.Label(new Rect(x, y, w, 22f), $"Current: {power.CurrentPower:F1}");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Max: {power.MaxPower:F1}");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Demand: {power.CurrentDemandPerSecond:F1}/sec");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Fill: {power.Normalized:P0}");
            return;
        }

        if (turret != null)
        {
            GUI.Box(rect, GUIContent.none);

            float x = rect.x + 10f;
            float y = rect.y + 8f;
            float w = rect.width - 20f;

            GUI.Label(new Rect(x, y, w, 22f), "Turret Diagnostics");
            y += 28f;

            GUI.Label(new Rect(x, y, w, 22f), $"Power Demand: {turret.PowerDemandPerSecond:F2}/sec");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Shot Cost: {turret.FirePowerCost:F1}");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), $"Can Fire: {(turret.CanFire() ? "YES" : "NO")}");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), "Ammo Type: Placeholder");
            y += 24f;

            GUI.Label(new Rect(x, y, w, 22f), "Ammo Count: Placeholder");
            return;
        }

        float gap = 10f;
        float topH = rect.height * 0.48f;
        float bottomH = rect.height - topH - gap;

        Rect containerRect = new Rect(rect.x, rect.y, rect.width, topH);
        Rect inventoryRect = new Rect(rect.x, containerRect.yMax + gap, rect.width, bottomH);

        DrawContainerSlots(containerRect, engine != null ? engine.FuelContainerItem : null);
        DrawCompatibleInventorySlots(inventoryRect, engine != null ? engine.FuelContainerItem : null);
    }

    private void DrawContainerSlots(Rect rect, ItemInstance fuelContainer)
    {
        GUI.Box(rect, GUIContent.none);

        float x = rect.x + 10f;
        float y = rect.y + 8f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), "Module Slots");
        y += 28f;

        ItemContainerState state = fuelContainer != null ? fuelContainer.ContainerState : null;
        if (state == null)
        {
            GUI.Label(new Rect(x, y, w, 22f), "(No container state)");
            return;
        }

        int columns = Mathf.Clamp(fuelContainer.Definition != null ? fuelContainer.Definition.ContainerColumnCount : 4, 1, 8);
        float cellSize = Mathf.Min(64f, (w - (columns - 1) * 6f) / columns);
        float startX = x;
        float startY = y;

        for (int i = 0; i < state.SlotCount; i++)
        {
            int col = i % columns;
            int row = i / columns;

            Rect cell = new Rect(
                startX + col * (cellSize + 6f),
                startY + row * (cellSize + 26f),
                cellSize,
                cellSize);

            InventorySlot slot = state.GetSlot(i);
            GUI.Box(cell, GUIContent.none);

            if (slot != null && !slot.IsEmpty && slot.Instance != null)
                DrawSlotContents(cell, slot.Instance);
            else
                GUI.Label(new Rect(cell.x + 4f, cell.y + 4f, cell.width - 8f, 20f), $"S{i + 1}");
        }
    }

    private void DrawCompatibleInventorySlots(Rect rect, ItemInstance fuelContainer)
    {
        GUI.Box(rect, GUIContent.none);

        float x = rect.x + 10f;
        float y = rect.y + 8f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), "Compatible Inventory");
        y += 28f;

        if (fuelContainer == null || !fuelContainer.IsContainer)
        {
            GUI.Label(new Rect(x, y, w, 22f), "(No compatible container)");
            return;
        }

        List<ItemInstance> compatible = new List<ItemInstance>(16);
        GatherCompatibleInventoryItems(fuelContainer, compatible);

        const int displayCount = 12;
        const int columns = 4;

        float cellSize = Mathf.Min(64f, (w - (columns - 1) * 6f) / columns);
        float startX = x;
        float startY = y;

        for (int i = 0; i < displayCount; i++)
        {
            int col = i % columns;
            int row = i / columns;

            Rect cell = new Rect(
                startX + col * (cellSize + 6f),
                startY + row * (cellSize + 26f),
                cellSize,
                cellSize);

            GUI.Box(cell, GUIContent.none);

            if (i < compatible.Count && compatible[i] != null)
                DrawSlotContents(cell, compatible[i]);
            else
                GUI.Label(new Rect(cell.x + 4f, cell.y + 4f, cell.width - 8f, 20f), "(Empty)");
        }
    }

    private void DrawSlotContents(Rect cell, ItemInstance item)
    {
        string shortName = item.Definition != null ? item.Definition.DisplayName : "Item";
        GUI.Label(new Rect(cell.x + 4f, cell.y + 4f, cell.width - 8f, 20f), shortName);
        GUI.Label(new Rect(cell.x + 4f, cell.y + 20f, cell.width - 8f, 18f), $"x{item.Quantity}");

        if (item.HasCharges)
            DrawChargeBar(new Rect(cell.x + 4f, cell.yMax - 12f, cell.width - 8f, 8f), item);
    }

    private void DrawItemSummaryWithCharges(Rect rect, ItemInstance item)
    {
        string itemName = item.Definition != null ? item.Definition.DisplayName : "Unknown";
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 16f), $"• {itemName} x{item.Quantity}");

        if (item.HasCharges)
            DrawChargeBar(new Rect(rect.x, rect.y + 24f, Mathf.Min(140f, rect.width), 8f), item);
    }

    private void DrawChargeBar(Rect rect, ItemInstance item)
    {
        if (!item.HasCharges || item.MaxCharges <= 0)
            return;

        float t = Mathf.Clamp01(item.CurrentCharges / (float)item.MaxCharges);

        Color prev = GUI.color;

        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        Rect fill = new Rect(rect.x, rect.y, rect.width * t, rect.height);
        GUI.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f, 0.95f), new Color(0.2f, 0.9f, 0.3f, 0.95f), t);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);

        GUI.color = prev;
        GUI.Label(new Rect(rect.x, rect.y + rect.height + 2f, rect.width, 14f), $"{item.CurrentCharges}/{item.MaxCharges}");
    }

    private void DrawSimpleBar(Rect rect, float t)
    {
        t = Mathf.Clamp01(t);

        Color prev = GUI.color;

        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = new Color(0.2f, 0.6f, 1f, 0.95f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * t, rect.height), Texture2D.whiteTexture);

        GUI.color = prev;
    }

    private void DrawWarningLabel(Rect rect, string text)
    {
        Color prev = GUI.color;
        GUI.color = Color.red;
        GUI.Label(rect, text);
        GUI.color = prev;
    }

    private void GatherCompatibleInventoryItems(ItemInstance fuelContainer, List<ItemInstance> results)
    {
        results.Clear();

        if (fuelContainer == null || !fuelContainer.IsContainer)
            return;

        if (_playerInventory != null)
        {
            for (int i = 0; i < _playerInventory.HotbarSlotCount; i++)
            {
                InventorySlot slot = _playerInventory.GetSlot(i);
                TryAddCompatible(slot != null ? slot.Instance : null, fuelContainer, results);
            }
        }

        if (_playerEquipment != null)
        {
            for (int i = 0; i < EquipmentSlots.Length; i++)
            {
                ItemInstance equipped = _playerEquipment.Get(EquipmentSlots[i]);
                TryAddCompatible(equipped, fuelContainer, results);
            }
        }
    }

    private void TryAddCompatible(ItemInstance candidate, ItemInstance fuelContainer, List<ItemInstance> results)
    {
        if (candidate == null || candidate.Definition == null)
            return;

        if (!fuelContainer.CanAcceptIntoContainer(candidate))
            return;

        if (results.Contains(candidate))
            return;

        results.Add(candidate);
    }

    private bool TryAddFuel(EngineModule engine, out string note)
    {
        if (engine == null)
        {
            note = "No engine.";
            return false;
        }

        return TryAddFuelToContainer(engine.FuelContainerItem, "Engine", out note);
    }

    private bool TryRemoveFuel(EngineModule engine, out string note)
    {
        if (engine == null)
        {
            note = "No engine.";
            return false;
        }

        return TryRemoveFuelFromContainer(engine.FuelContainerItem, out note);
    }

    private bool TryAddFuel(GeneratorModule generator, out string note)
    {
        if (generator == null)
        {
            note = "No generator.";
            return false;
        }

        return TryAddFuelToContainer(generator.FuelContainerItem, "Generator", out note);
    }

    private bool TryRemoveFuel(GeneratorModule generator, out string note)
    {
        if (generator == null)
        {
            note = "No generator.";
            return false;
        }

        return TryRemoveFuelFromContainer(generator.FuelContainerItem, out note);
    }

    private bool TryAddFuelToContainer(ItemInstance fuelContainer, string ownerName, out string note)
    {
        note = "Add Fuel failed.";

        if (fuelContainer == null || !fuelContainer.IsContainer)
        {
            note = $"{ownerName} has no valid fuel container.";
            return false;
        }

        if (_playerInventory != null)
        {
            for (int i = 0; i < _playerInventory.HotbarSlotCount; i++)
            {
                InventorySlot slot = _playerInventory.GetSlot(i);
                if (slot == null || slot.IsEmpty || slot.Instance == null)
                    continue;

                ItemInstance candidate = slot.Instance;
                if (!fuelContainer.CanAcceptIntoContainer(candidate))
                    continue;

                slot.Clear();

                if (fuelContainer.TryInsertIntoContainer(candidate, out ItemInstance remainder))
                {
                    if (remainder != null && !remainder.IsDepleted())
                        slot.Set(remainder);

                    _playerInventory.NotifyChanged();
                    note = $"Added {candidate.Definition.DisplayName}.";
                    return true;
                }

                slot.Set(candidate);
            }
        }

        if (_playerEquipment != null)
        {
            for (int i = 0; i < EquipmentSlots.Length; i++)
            {
                BottomBarSlotType slotType = EquipmentSlots[i];
                ItemInstance candidate = _playerEquipment.Get(slotType);
                if (candidate == null)
                    continue;

                if (!fuelContainer.CanAcceptIntoContainer(candidate))
                    continue;

                _playerEquipment.Remove(slotType);

                if (fuelContainer.TryInsertIntoContainer(candidate, out ItemInstance remainder))
                {
                    if (remainder != null && !remainder.IsDepleted())
                    {
                        _playerInventory?.TryAutoInsert(remainder, out _);
                    }

                    _playerInventory?.NotifyChanged();
                    note = $"Added {candidate.Definition.DisplayName}.";
                    return true;
                }

                _playerEquipment.TryPlace(slotType, candidate, out _);
            }
        }

        note = "No compatible fuel found in inventory.";
        return false;
    }

    private bool TryRemoveFuelFromContainer(ItemInstance fuelContainer, out string note)
    {
        note = "Remove Fuel failed.";

        if (_playerInventory == null)
        {
            note = "No player inventory found.";
            return false;
        }

        ItemContainerState state = fuelContainer != null ? fuelContainer.ContainerState : null;
        if (state == null)
        {
            note = "No fuel container state.";
            return false;
        }

        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            ItemInstance removed = slot.Instance;
            slot.Clear();

            if (_playerInventory.TryAutoInsert(removed, out ItemInstance remainder))
            {
                if (remainder != null && !remainder.IsDepleted())
                {
                    slot.Set(remainder);
                    note = "Inventory had no room for all removed fuel.";
                    _playerInventory.NotifyChanged();
                    return false;
                }

                _playerInventory.NotifyChanged();
                note = $"Removed {removed.Definition.DisplayName}.";
                return true;
            }

            slot.Set(removed);
            note = "No room in inventory.";
            return false;
        }

        note = "No fuel to remove.";
        return false;
    }
}