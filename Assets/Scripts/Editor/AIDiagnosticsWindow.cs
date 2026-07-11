#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class AIDiagnosticsWindow : EditorWindow
{
    private Vector2 scroll;
    private bool autoRefresh = true;
    private bool showDefinitionDetails = true;
    private bool showMovementModules = true;
    private bool showRuntimeDiagnostics = true;
    private bool showThreatSources = true;
    private bool showSimulationLod = true;

    [MenuItem("Tools/AI Diagnostics")]
    public static void Open()
    {
        AIDiagnosticsWindow window = GetWindow<AIDiagnosticsWindow>();
        window.titleContent = new GUIContent("AI Diagnostics");
        window.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += Repaint;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= Repaint;
    }

    private void OnInspectorUpdate()
    {
        if (autoRefresh && EditorApplication.isPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();

        AgentController agent = ResolveSelectedAgent();

        if (agent == null)
        {
            EditorGUILayout.HelpBox(
                "Select a GameObject with an AgentController, or a child of one.",
                MessageType.Info);

            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawAgentHeader(agent);

        EditorGUILayout.Space(8);

        if (showSimulationLod)
            DrawSimulationLod(agent);

        EditorGUILayout.Space(8);

        if (showDefinitionDetails)
            DrawDefinitionDetails(agent);

        EditorGUILayout.Space(8);

        if (showMovementModules)
            DrawMovementProfile(agent);

        EditorGUILayout.Space(8);

        if (showRuntimeDiagnostics)
            DrawRuntimeDiagnostics(agent);

        EditorGUILayout.Space(8);

        if (showThreatSources)
            DrawThreatDiagnostics(agent);

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            autoRefresh = GUILayout.Toggle(
                autoRefresh,
                "Auto Refresh",
                EditorStyles.toolbarButton,
                GUILayout.Width(90));

            showDefinitionDetails = GUILayout.Toggle(
                showDefinitionDetails,
                "Definition",
                EditorStyles.toolbarButton,
                GUILayout.Width(80));

            showMovementModules = GUILayout.Toggle(
                showMovementModules,
                "Movement Profile",
                EditorStyles.toolbarButton,
                GUILayout.Width(120));

            showRuntimeDiagnostics = GUILayout.Toggle(
                showRuntimeDiagnostics,
                "Runtime",
                EditorStyles.toolbarButton,
                GUILayout.Width(80));

            showSimulationLod = GUILayout.Toggle(
                showSimulationLod,
                "LOD",
                EditorStyles.toolbarButton,
                GUILayout.Width(60));

            showThreatSources = GUILayout.Toggle(
                showThreatSources,
                "Threats",
                EditorStyles.toolbarButton,
                GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                Repaint();
        }
    }

    private static AgentController ResolveSelectedAgent()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return null;

        return selected.GetComponentInParent<AgentController>() ??
               selected.GetComponentInChildren<AgentController>(true);
    }

    private void DrawAgentHeader(AgentController agent)
    {
        EditorGUILayout.LabelField("Selected Agent", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField("Agent", agent, typeof(AgentController), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Agent Object", GUILayout.Width(140)))
                    Selection.activeObject = agent.gameObject;

                if (GUILayout.Button("Reinitialize Runtime", GUILayout.Width(160)))
                {
                    agent.ReinitializeRuntime();
                    Repaint();
                }
            }

            EditorGUILayout.LabelField("Name", agent.name);
            EditorGUILayout.LabelField("Display Name", agent.DisplayName);
            EditorGUILayout.LabelField("Kind", agent.Kind.ToString());
            EditorGUILayout.LabelField("Stable Id", agent.StableId);
            EditorGUILayout.LabelField("Node Id", agent.NodeId);

            Rigidbody2D rb = agent.GetComponent<Rigidbody2D>();
            EditorGUILayout.ObjectField("Rigidbody2D", rb, typeof(Rigidbody2D), true);

            if (rb != null && EditorApplication.isPlaying)
            {
                EditorGUILayout.Vector2Field("Current Velocity", rb.linearVelocity);
                EditorGUILayout.Toggle("Simulated", rb.simulated);
                EditorGUILayout.EnumPopup("Body Type", rb.bodyType);
                EditorGUILayout.FloatField("Gravity Scale", rb.gravityScale);
            }
        }
    }

    private void DrawSimulationLod(AgentController agent)
    {
        EditorGUILayout.LabelField("Simulation LOD", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (agent == null)
            {
                EditorGUILayout.HelpBox("No selected agent.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Agent LOD", EditorStyles.boldLabel);

            EditorGUILayout.Toggle("Allow Simulation LOD", agent.AllowSimulationLod);
            EditorGUILayout.EnumPopup("Current Agent State", agent.CurrentLodState);
            EditorGUILayout.FloatField("Reduced Tick Interval", agent.ReducedTickInterval);

            EditorGUILayout.Space(6);

            FishSchoolMember2D member = agent.GetComponent<FishSchoolMember2D>();

            if (member == null)
            {
                EditorGUILayout.HelpBox(
                    "Selected agent is not a FishSchoolMember2D. No school LOD data available.",
                    MessageType.Info);
                return;
            }

            FishSchoolController school = member.School;

            EditorGUILayout.ObjectField(
                "Fish School",
                school,
                typeof(FishSchoolController),
                true);

            if (school == null)
            {
                EditorGUILayout.HelpBox(
                    "Fish has no school assigned.",
                    MessageType.Warning);
                return;
            }

            FishSchoolSimulationLod schoolLod =
                school.GetComponent<FishSchoolSimulationLod>();

            EditorGUILayout.ObjectField(
                "School LOD",
                schoolLod,
                typeof(FishSchoolSimulationLod),
                true);

            if (schoolLod == null)
            {
                EditorGUILayout.HelpBox(
                    "School has no FishSchoolSimulationLod component.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("School LOD", EditorStyles.boldLabel);

            EditorGUILayout.EnumPopup("Current School State", schoolLod.CurrentState);

            EditorGUILayout.ObjectField(
                "Target",
                schoolLod.Target,
                typeof(Transform),
                true);

            EditorGUILayout.FloatField(
                "Last Distance To Target",
                schoolLod.LastDistanceToTarget);

            EditorGUILayout.FloatField(
                "Check Interval",
                schoolLod.CheckInterval);

            SimulationLodDistanceBands bands = schoolLod.Bands;

            if (bands != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Distance Bands", EditorStyles.boldLabel);

                EditorGUILayout.FloatField("Full Radius", bands.fullRadius);
                EditorGUILayout.FloatField("Reduced Radius", bands.reducedRadius);
                EditorGUILayout.FloatField("Frozen Radius", bands.frozenRadius);
                EditorGUILayout.FloatField("Hysteresis", bands.hysteresis);
            }

            EditorGUILayout.Space(6);

            if (agent.CurrentLodState != schoolLod.CurrentState)
            {
                EditorGUILayout.HelpBox(
                    $"Agent LOD is {agent.CurrentLodState}, but school LOD is {schoolLod.CurrentState}. This can happen briefly during transitions, but if it persists, the school is not applying LOD to this member.",
                    MessageType.Warning);
            }
        }
    }

    private void DrawDefinitionDetails(AgentController agent)
    {
        EditorGUILayout.LabelField("Definition / Behavior Set", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            AgentDefinition definition = agent.Definition;

            EditorGUILayout.ObjectField(
                "Definition",
                definition,
                typeof(AgentDefinition),
                false);

            if (definition == null)
            {
                EditorGUILayout.HelpBox(
                    "Agent has no AgentDefinition. No behavior set can be created.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Definition Id", definition.Id);
            EditorGUILayout.LabelField("Definition Name", definition.DisplayName);
            EditorGUILayout.LabelField("Kind", definition.Kind.ToString());

            AgentBehaviorSetDefinition behaviorSet = definition.BehaviorSet;

            EditorGUILayout.ObjectField(
                "Behavior Set",
                behaviorSet,
                typeof(AgentBehaviorSetDefinition),
                false);

            if (behaviorSet == null)
            {
                EditorGUILayout.HelpBox(
                    "Definition has no BehaviorSet.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.ObjectField(
                "Brain",
                behaviorSet.Brain as Object,
                typeof(Object),
                false);

            EditorGUILayout.ObjectField(
                "Movement",
                behaviorSet.Movement as Object,
                typeof(Object),
                false);

            EditorGUILayout.ObjectField(
                "Interaction",
                behaviorSet.Interaction as Object,
                typeof(Object),
                false);
        }
    }

    private void DrawMovementProfile(AgentController agent)
    {
        EditorGUILayout.LabelField("Available Movement Behaviors", EditorStyles.boldLabel);

        AgentDefinition definition = agent.Definition;
        AgentBehaviorSetDefinition behaviorSet = definition != null ? definition.BehaviorSet : null;

        if (behaviorSet == null || behaviorSet.Movement == null)
        {
            EditorGUILayout.HelpBox(
                "No movement definition assigned.",
                MessageType.Warning);
            return;
        }

        Object movementObject = behaviorSet.Movement as Object;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField(
                "Movement Definition",
                movementObject,
                typeof(Object),
                false);

            CompositeAgentMovementDefinition composite =
                behaviorSet.Movement as CompositeAgentMovementDefinition;

            if (composite == null)
            {
                EditorGUILayout.HelpBox(
                    "Movement definition is not CompositeAgentMovementDefinition. Runtime diagnostics may be limited.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Composite Movement", composite.name);

            IReadOnlyList<AgentMovementModuleSlot> modules = composite.Modules;

            int enabledCount = 0;

            if (modules != null)
            {
                for (int i = 0; i < modules.Count; i++)
                {
                    if (modules[i] != null && modules[i].Enabled && modules[i].Module != null)
                        enabledCount++;
                }
            }

            if (enabledCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Composite movement has module slots, but none are enabled. This agent will not create any movement module runtimes.",
                    MessageType.Warning);
            }

            if (modules == null || modules.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Composite movement has no modules assigned. This agent will not move. Stunningly quiet, but not ideal.",
                    MessageType.Warning);
                return;
            }

            DrawModuleHeader();

            for (int i = 0; i < modules.Count; i++)
            {
                AgentMovementModuleSlot slot = modules[i];

                if (slot == null)
                {
                    DrawModuleProfileRow(i, "<null slot>", false, 0, 0f, null);
                    continue;
                }

                DrawModuleProfileRow(
                    i,
                    slot.ModuleLabel,
                    slot.Enabled,
                    slot.Priority,
                    slot.Strength,
                    slot.Module);
            }
        }
    }

    private void DrawModuleHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("#", EditorStyles.boldLabel, GUILayout.Width(28));
            GUILayout.Label("Module", EditorStyles.boldLabel, GUILayout.MinWidth(180));
            GUILayout.Label("Enabled", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Priority", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Strength", EditorStyles.boldLabel, GUILayout.Width(70));
        }
    }

    private void DrawModuleProfileRow(
        int index,
        string label,
        bool enabled,
        int priority,
        float strength,
        Object moduleObject)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(index.ToString(), GUILayout.Width(28));

            EditorGUILayout.ObjectField(
                moduleObject != null ? moduleObject : null,
                typeof(Object),
                false,
                GUILayout.MinWidth(180));

            GUILayout.Label(enabled ? "Yes" : "No", GUILayout.Width(60));
            GUILayout.Label(priority.ToString(), GUILayout.Width(60));
            GUILayout.Label(strength.ToString("0.###"), GUILayout.Width(70));
        }

        if (moduleObject == null)
        {
            EditorGUILayout.HelpBox(
                $"Movement module slot {index} is missing its module asset.",
                MessageType.Warning);
        }
    }

    private void DrawRuntimeDiagnostics(AgentController agent)
    {
        EditorGUILayout.LabelField("Runtime Movement Diagnostics", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Runtime diagnostics are available in Play Mode.",
                    MessageType.Info);
                return;
            }

            IAgentMovementRuntime movementRuntime = agent.MovementRuntime;

            if (movementRuntime == null)
            {
                EditorGUILayout.HelpBox(
                    agent.HasMovementDefinition
                        ? "Agent has a movement definition, but no movement runtime. Runtime likely initialized before the definition/behavior set was ready. Click Reinitialize Runtime."
                        : "Agent has no movement runtime because no movement definition is assigned.",
                    MessageType.Warning);

                if (agent.HasMovementDefinition)
                {
                    if (GUILayout.Button("Reinitialize Runtime"))
                    {
                        agent.ReinitializeRuntime();
                        Repaint();
                    }
                }

                return;
            }

            EditorGUILayout.LabelField("Movement Runtime", movementRuntime.GetType().Name);

            if (!agent.TryGetMovementDiagnostics(out AgentMovementDiagnosticsSnapshot snapshot) ||
                snapshot == null)
            {
                EditorGUILayout.HelpBox(
                    "Movement runtime does not provide diagnostics. If this should be composite movement, verify BehaviorSet.Movement uses CompositeAgentMovementDefinition.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Movement Definition", snapshot.movementDefinitionName);
            EditorGUILayout.LabelField("Runtime Type", snapshot.runtimeTypeName);
            EditorGUILayout.Toggle("Has Rigidbody", snapshot.hasRigidbody);

            EditorGUILayout.Vector2Field("Current Velocity", snapshot.currentVelocity);
            EditorGUILayout.Toggle("Has Final Intent", snapshot.hasFinalIntent);

            EditorGUILayout.Vector2Field("Raw Desired Velocity", snapshot.rawDesiredVelocity);
            EditorGUILayout.Vector2Field("Final Desired Velocity", snapshot.finalDesiredVelocity);
            EditorGUILayout.Vector2Field("Applied Velocity", snapshot.appliedVelocity);

            EditorGUILayout.FloatField("Active Max Speed", snapshot.activeMaxSpeed);
            EditorGUILayout.FloatField("Active Acceleration", snapshot.activeAcceleration);

            EditorGUILayout.LabelField(
                "Active Movement",
                string.IsNullOrWhiteSpace(snapshot.activeSummary)
                    ? "<none>"
                    : snapshot.activeSummary);

            if (!snapshot.hasFinalIntent)
            {
                EditorGUILayout.HelpBox(
                    snapshot.activeSummary,
                    MessageType.Warning);
            }

            EditorGUILayout.Space(6);

            DrawRuntimeModuleHeader();

            if (snapshot.modules == null || snapshot.modules.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No runtime module diagnostics were reported.",
                    MessageType.Warning);
                return;
            }

            for (int i = 0; i < snapshot.modules.Count; i++)
                DrawRuntimeModuleRow(snapshot.modules[i]);
        }
    }

    private void DrawThreatDiagnostics(AgentController agent)
    {
        EditorGUILayout.LabelField("Threat Diagnostics", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (agent == null)
            {
                EditorGUILayout.HelpBox("No selected agent.", MessageType.Info);
                return;
            }

            Rigidbody2D rb = agent.GetComponent<Rigidbody2D>();
            Vector2 agentPos = rb != null
                ? rb.position
                : (Vector2)agent.transform.position;

            EditorGUILayout.Vector2Field("Agent Position", agentPos);

            IReadOnlyList<CreatureThreatSource> sources = CreatureThreatRegistry.AllSources;

            if (sources == null || sources.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No CreatureThreatSource instances are registered. If the player has PlayerCreatureThreatSource, check that the component and GameObject are enabled.",
                    MessageType.Warning);
                return;
            }

            int activeCount = CreatureThreatRegistry.ActiveSourceCount;

            EditorGUILayout.LabelField("Registered Sources", sources.Count.ToString());
            EditorGUILayout.LabelField("Active Sources", activeCount.ToString());

            if (activeCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Threat sources exist, but none are active.",
                    MessageType.Warning);
            }

            DrawThreatHeader();

            for (int i = 0; i < sources.Count; i++)
            {
                CreatureThreatSource source = sources[i];
                if (source == null)
                    continue;

                DrawThreatRow(i, agentPos, source);
            }
        }
    }

    private void DrawThreatHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("#", EditorStyles.boldLabel, GUILayout.Width(28));
            GUILayout.Label("Source", EditorStyles.boldLabel, GUILayout.MinWidth(180));
            GUILayout.Label("Kind", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("Active", EditorStyles.boldLabel, GUILayout.Width(55));
            GUILayout.Label("Dist", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Radius", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Strength", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("In Range", EditorStyles.boldLabel, GUILayout.Width(70));
        }
    }

    private void DrawThreatRow(int index, Vector2 agentPos, CreatureThreatSource source)
    {
        float dist = Vector2.Distance(agentPos, source.Position);
        bool inRange = source.IsActiveThreat && dist <= source.Radius;

        GUIStyle style = inRange
            ? EditorStyles.boldLabel
            : EditorStyles.label;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(index.ToString(), style, GUILayout.Width(28));

            EditorGUILayout.ObjectField(
                source,
                typeof(CreatureThreatSource),
                true,
                GUILayout.MinWidth(180));

            GUILayout.Label(source.Kind.ToString(), style, GUILayout.Width(70));
            GUILayout.Label(source.IsActiveThreat ? "Yes" : "No", style, GUILayout.Width(55));
            GUILayout.Label(dist.ToString("0.00"), style, GUILayout.Width(60));
            GUILayout.Label(source.Radius.ToString("0.00"), style, GUILayout.Width(60));
            GUILayout.Label(source.Strength.ToString("0.00"), style, GUILayout.Width(70));
            GUILayout.Label(inRange ? "YES" : "No", style, GUILayout.Width(70));
        }
    }

    private void DrawRuntimeModuleHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("#", EditorStyles.boldLabel, GUILayout.Width(28));
            GUILayout.Label("Module", EditorStyles.boldLabel, GUILayout.MinWidth(160));
            GUILayout.Label("Pri", EditorStyles.boldLabel, GUILayout.Width(40));
            GUILayout.Label("Str", EditorStyles.boldLabel, GUILayout.Width(45));
            GUILayout.Label("Intent", EditorStyles.boldLabel, GUILayout.Width(55));
            GUILayout.Label("Weight", EditorStyles.boldLabel, GUILayout.Width(55));
            GUILayout.Label("Supp", EditorStyles.boldLabel, GUILayout.Width(45));
            GUILayout.Label("Velocity", EditorStyles.boldLabel, GUILayout.Width(150));
        }
    }

    private void DrawRuntimeModuleRow(AgentMovementModuleDiagnostic diag)
    {
        if (diag == null)
            return;

        bool active = diag.hasIntent && !diag.suppressedByHigherPriority && diag.intentWeight > 0f;

        GUIStyle labelStyle = active
            ? EditorStyles.boldLabel
            : EditorStyles.label;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(diag.slotIndex.ToString(), labelStyle, GUILayout.Width(28));

            string moduleLabel = !string.IsNullOrWhiteSpace(diag.moduleName)
                ? diag.moduleName
                : "<unnamed>";

            GUILayout.Label(moduleLabel, labelStyle, GUILayout.MinWidth(160));
            GUILayout.Label(diag.priority.ToString(), GUILayout.Width(40));
            GUILayout.Label(diag.strength.ToString("0.##"), GUILayout.Width(45));
            GUILayout.Label(diag.hasIntent ? "Yes" : "No", GUILayout.Width(55));
            GUILayout.Label(diag.intentWeight.ToString("0.##"), GUILayout.Width(55));
            GUILayout.Label(diag.suppressedByHigherPriority ? "Yes" : "No", GUILayout.Width(45));
            GUILayout.Label(FormatVector(diag.desiredVelocity), GUILayout.Width(150));
        }

        if (!string.IsNullOrWhiteSpace(diag.debugLabel))
        {
            EditorGUILayout.LabelField(
                "  Label",
                diag.debugLabel);
        }

        if (diag.hasIntent && diag.suppressedByHigherPriority)
        {
            EditorGUILayout.HelpBox(
                $"{diag.moduleName} has intent but is suppressed by a higher-priority suppressing module.",
                MessageType.None);
        }
    }

    private static string FormatVector(Vector2 v)
    {
        return $"({v.x:0.00}, {v.y:0.00})";
    }
}
#endif