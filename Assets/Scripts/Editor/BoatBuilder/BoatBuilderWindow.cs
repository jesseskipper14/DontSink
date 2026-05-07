#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BoatBuilderWindow : EditorWindow
{
    private const string Prefs_Grid = "BoatBuilder.Grid";
    private const string Prefs_Z = "BoatBuilder.ZPlane";
    private const string Prefs_HardpointStartingModuleGuid = "BoatBuilder.HardpointStartingModuleGuid";
    private const string Prefs_SelectedTool = "BoatBuilder.Tool";
    private const string Prefs_KIT_GUID = "BoatBuilder.KitGuid";
    private const string Prefs_BoatRootGlobalId = "BoatBuilder.BoatRootGlobalId";
    private const string Prefs_AutoParent = "BoatBuilder.AutoParent";
    private const string Prefs_SnapOnPlace = "BoatBuilder.SnapOnPlace";
    private const string Prefs_ShowPreview = "BoatBuilder.ShowPreview";
    private const string Prefs_EnforceRequired = "BoatBuilder.EnforceRequired";
    private const string Prefs_HardpointType = "BoatBuilder.HardpointType";
    private const string Prefs_HardpointIdPrefix = "BoatBuilder.HardpointIdPrefix";
    private const string Prefs_HardpointAutoMount = "BoatBuilder.HardpointAutoMount";
    private const string Prefs_HardpointRenameObject = "BoatBuilder.HardpointRenameObject";
    private const string Prefs_StairAscendRight = "BoatBuilder.StairAscendRight";
    private const string Prefs_HideShellWhileEditing = "BoatBuilder.HideShellWhileEditing";
    private const string Prefs_VisibilityZoneMode = "BoatBuilder.VisibilityZoneMode";
    private const string Prefs_VisibilityZonePriority = "BoatBuilder.VisibilityZonePriority";
    private const string Prefs_VisibilityZoneUseDefaultPriority = "BoatBuilder.VisibilityZoneUseDefaultPriority";

    public enum Tool
    {
        HullSegment = 0,
        Wall = 1,
        Hatch = 2,
        PilotChair = 3,
        CompartmentRect = 4,
        Deck = 5,
        Ladder = 6,
        Stairs = 7,
        Ledge = 8,

        BoatBoardObject = 9,
        MapTable = 10,
        PlayerSpawnPoint = 11,
        BoardedVolume = 12,

        Hardpoint = 13,
        ExteriorShell = 14,
        TurretControllerChair = 15,
        Door = 16,
        BoatVisibilityZone = 17,
    }

    private struct ActionButtonDef
    {
        public string Label;
        public System.Action Action;

        public ActionButtonDef(string label, System.Action action)
        {
            Label = label;
            Action = action;
        }
    }

    private BoatKit _kit;
    private Transform _boatRootOverride;
    private ModuleDefinition _hardpointStartingModuleDefinition;

    private Tool _tool;
    private float _grid = 0.5f;
    private float _zPlane = 0f;
    private bool _autoParent = true;
    private bool _snapOnPlace = true;

    private bool _showPreview = true;
    private bool _enforceRequired = true;

    private HardpointType _hardpointType = HardpointType.Engine;
    private string _hardpointIdPrefix = "hardpoint";
    private bool _hardpointAutoCreateMountPoint = true;
    private bool _hardpointRenameObjectToId = true;
    private bool _stairAscendRight = true;
    private bool _hideShellWhileEditing;

    private BoatVisibilityMode _visibilityZoneMode = BoatVisibilityMode.BoardedInterior;
    private int _visibilityZonePriority = 100;
    private bool _visibilityZoneUseDefaultPriority = true;

    private Vector2 _scroll;

    [MenuItem("Tools/Boat Builder/Window")]
    public static void Open()
    {
        var w = GetWindow<BoatBuilderWindow>("Boat Builder");
        w.minSize = new Vector2(420, 390);
        w.Show();
    }

    private void OnEnable()
    {
        _grid = Mathf.Max(0.01f, EditorPrefs.GetFloat(Prefs_Grid, 0.5f));
        _zPlane = EditorPrefs.GetFloat(Prefs_Z, 0f);
        _tool = (Tool)EditorPrefs.GetInt(Prefs_SelectedTool, 0);
        _autoParent = EditorPrefs.GetBool(Prefs_AutoParent, true);
        _snapOnPlace = EditorPrefs.GetBool(Prefs_SnapOnPlace, true);
        _showPreview = EditorPrefs.GetBool(Prefs_ShowPreview, true);
        _enforceRequired = EditorPrefs.GetBool(Prefs_EnforceRequired, true);

        _hardpointType = (HardpointType)EditorPrefs.GetInt(Prefs_HardpointType, (int)HardpointType.Engine);
        _hardpointIdPrefix = EditorPrefs.GetString(Prefs_HardpointIdPrefix, "hardpoint");
        _hardpointAutoCreateMountPoint = EditorPrefs.GetBool(Prefs_HardpointAutoMount, true);
        _hardpointRenameObjectToId = EditorPrefs.GetBool(Prefs_HardpointRenameObject, true);
        _stairAscendRight = EditorPrefs.GetBool(Prefs_StairAscendRight, true);

        _visibilityZoneMode = (BoatVisibilityMode)EditorPrefs.GetInt(
            Prefs_VisibilityZoneMode,
            (int)BoatVisibilityMode.BoardedInterior);

        _visibilityZonePriority = EditorPrefs.GetInt(
            Prefs_VisibilityZonePriority,
            BoatVisibilityZone.GetDefaultPriority(_visibilityZoneMode));

        _visibilityZoneUseDefaultPriority = EditorPrefs.GetBool(
            Prefs_VisibilityZoneUseDefaultPriority,
            true);

        string startingModuleGuid = EditorPrefs.GetString(Prefs_HardpointStartingModuleGuid, "");
        if (!string.IsNullOrWhiteSpace(startingModuleGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(startingModuleGuid);
            _hardpointStartingModuleDefinition = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(path);
        }

        var guid = EditorPrefs.GetString(Prefs_KIT_GUID, "");
        if (!string.IsNullOrEmpty(guid))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            _kit = AssetDatabase.LoadAssetAtPath<BoatKit>(path);
        }

        TryRestoreBoatRootOverride();

        BoatBuilderSceneTools.Attach();
        SyncToSceneTools();

        Selection.selectionChanged -= HandleSelectionChanged;
        Selection.selectionChanged += HandleSelectionChanged;

        TryRefreshStateFromSelection();
        ApplyShellVisibilityToCurrentRoot();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= HandleSelectionChanged;

        // Restore shell visibility when closing the builder so the scene does not get left in a weird editing state.
        Transform root = BoatBuilderSceneTools.PeekBestBoatRoot();
        if (root != null)
            BoatBuilderSceneTools.SetExteriorShellEditorHidden(root, false);

        Persist();
        SyncToSceneTools();
    }

    private void TryRestoreBoatRootOverride()
    {
        _boatRootOverride = null;

        string id = EditorPrefs.GetString(Prefs_BoatRootGlobalId, "");

        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!GlobalObjectId.TryParse(id, out GlobalObjectId globalId))
            return;

        Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);

        if (obj is GameObject go)
            _boatRootOverride = go.transform;
        else if (obj is Component c)
            _boatRootOverride = c.transform;
    }

    private void Persist()
    {
        EditorPrefs.SetFloat(Prefs_Grid, _grid);
        EditorPrefs.SetFloat(Prefs_Z, _zPlane);
        EditorPrefs.SetInt(Prefs_SelectedTool, (int)_tool);
        EditorPrefs.SetBool(Prefs_AutoParent, _autoParent);
        EditorPrefs.SetBool(Prefs_SnapOnPlace, _snapOnPlace);
        EditorPrefs.SetBool(Prefs_ShowPreview, _showPreview);
        EditorPrefs.SetBool(Prefs_EnforceRequired, _enforceRequired);

        EditorPrefs.SetInt(Prefs_HardpointType, (int)_hardpointType);
        EditorPrefs.SetString(Prefs_HardpointIdPrefix, _hardpointIdPrefix ?? "hardpoint");
        EditorPrefs.SetBool(Prefs_HardpointAutoMount, _hardpointAutoCreateMountPoint);
        EditorPrefs.SetBool(Prefs_HardpointRenameObject, _hardpointRenameObjectToId);
        EditorPrefs.SetBool(Prefs_StairAscendRight, _stairAscendRight);
        EditorPrefs.SetBool(Prefs_HideShellWhileEditing, _hideShellWhileEditing);

        EditorPrefs.SetInt(Prefs_VisibilityZoneMode, (int)_visibilityZoneMode);
        EditorPrefs.SetInt(Prefs_VisibilityZonePriority, _visibilityZonePriority);
        EditorPrefs.SetBool(Prefs_VisibilityZoneUseDefaultPriority, _visibilityZoneUseDefaultPriority);

        if (_hardpointStartingModuleDefinition != null)
        {
            string path = AssetDatabase.GetAssetPath(_hardpointStartingModuleDefinition);
            string guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(Prefs_HardpointStartingModuleGuid, guid);
        }
        else
        {
            EditorPrefs.DeleteKey(Prefs_HardpointStartingModuleGuid);
        }

        if (_kit != null)
        {
            var path = AssetDatabase.GetAssetPath(_kit);
            var guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(Prefs_KIT_GUID, guid);
        }

        if (_boatRootOverride != null)
        {
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(_boatRootOverride.gameObject);
            EditorPrefs.SetString(Prefs_BoatRootGlobalId, id.ToString());
        }
        else
        {
            EditorPrefs.DeleteKey(Prefs_BoatRootGlobalId);
        }
    }

    private void GenerateNewBoatRoot()
    {
        string baseName = "BoatRoot";
        string finalName = baseName;
        int index = 1;

        while (GameObject.Find(finalName) != null)
        {
            index++;
            finalName = $"{baseName}_{index:00}";
        }

        GameObject root;

        if (_kit != null && _kit.BoatRootPrefab != null)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(_kit.BoatRootPrefab))
                root = (GameObject)PrefabUtility.InstantiatePrefab(_kit.BoatRootPrefab);
            else
                root = Instantiate(_kit.BoatRootPrefab);

            if (root == null)
            {
                Debug.LogWarning("[BoatBuilder] Failed to instantiate BoatRootPrefab. Falling back to empty BoatRoot.");
                root = new GameObject(finalName);
                Undo.RegisterCreatedObjectUndo(root, "Generate Boat Root");
            }
            else
            {
                Undo.RegisterCreatedObjectUndo(root, "Generate Boat Root From Prefab");
            }
        }
        else
        {
            root = new GameObject(finalName);
            Undo.RegisterCreatedObjectUndo(root, "Generate Boat Root");
        }

        root.name = finalName;
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        _boatRootOverride = root.transform;

        Selection.activeTransform = _boatRootOverride;
        EditorGUIUtility.PingObject(root);

        Persist();
        SyncToSceneTools();
        SceneView.RepaintAll();

        Debug.Log($"[BoatBuilder] Generated new boat root: {finalName}", root);
    }

    private void SyncToSceneTools()
    {
        BoatBuilderSceneTools.SetContext(new BoatBuilderSceneTools.Context
        {
            Kit = _kit,
            BoatRootOverride = _boatRootOverride,
            HardpointStartingModuleDefinition = _hardpointStartingModuleDefinition,

            ActiveTool = _tool,
            GridSize = _grid,
            ZPlane = _zPlane,
            AutoParentToBoatRoot = _autoParent,
            SnapOnPlace = _snapOnPlace,
            ShowSnapPreview = _showPreview,
            EnforceRequiredPieces = _enforceRequired,
            BoardedVolumePadding = 0f,
            BoardedVolumeExtraUp = 5.0f,
            BoardedVolumeExtraDown = 0f,
            RequiredPlayerSpawnPoints = 4,

            SelectedHardpointType = _hardpointType,
            HardpointIdPrefix = string.IsNullOrWhiteSpace(_hardpointIdPrefix) ? "hardpoint" : _hardpointIdPrefix.Trim(),
            HardpointAutoCreateMountPoint = _hardpointAutoCreateMountPoint,
            HardpointRenameObjectToId = _hardpointRenameObjectToId,
            StairAscendRight = _stairAscendRight,

            SelectedVisibilityZoneMode = _visibilityZoneMode,
            VisibilityZonePriority = _visibilityZoneUseDefaultPriority
                ? BoatVisibilityZone.GetDefaultPriority(_visibilityZoneMode)
                : _visibilityZonePriority,
        });
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);

        DrawBoatRootSection();

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawToolPicker();

            EditorGUI.BeginChangeCheck();

            DrawPlacementSettings();
            DrawToolSpecificSettings();

            if (EditorGUI.EndChangeCheck())
            {
                Persist();
                SyncToSceneTools();
                ApplyShellVisibilityToCurrentRoot();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6);

            DrawPlacementButtons();

            EditorGUILayout.Space(6);

            DrawRequiredPiecesAndActions();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Scene Controls:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(" Left-click: place selected prefab");
            EditorGUILayout.LabelField(" Right-click or Esc: cancel placement");
        }

        EditorGUILayout.Space(6);

        DrawNotesSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawBoatRootSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();

            _kit = (BoatKit)EditorGUILayout.ObjectField(
                "Boat Kit",
                _kit,
                typeof(BoatKit),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                Persist();
                SyncToSceneTools();
            }

            if (_kit == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a BoatKit asset (Tools > Boat Builder > Create BoatKit Asset).",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();

            _boatRootOverride = (Transform)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Fixed Boat Root",
                    "When assigned, all placed pieces are parented directly under this root while Auto-parent is enabled."),
                _boatRootOverride,
                typeof(Transform),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                Persist();
                SyncToSceneTools();
                ApplyShellVisibilityToCurrentRoot();
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate New Boat Root"))
                {
                    GenerateNewBoatRoot();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection As Boat Root"))
                {
                    if (Selection.activeTransform != null)
                    {
                        _boatRootOverride = Selection.activeTransform;
                        Persist();
                        SyncToSceneTools();
                        ApplyShellVisibilityToCurrentRoot();
                        SceneView.RepaintAll();
                    }
                    else
                    {
                        Debug.LogWarning("[BoatBuilder] No selection to use as Boat Root.");
                    }
                }

                if (GUILayout.Button("Clear Fixed Root"))
                {
                    _boatRootOverride = null;
                    Persist();
                    SyncToSceneTools();
                    ApplyShellVisibilityToCurrentRoot();
                    SceneView.RepaintAll();
                }
            }

            if (_boatRootOverride == null)
            {
                EditorGUILayout.HelpBox(
                    "No Fixed Boat Root assigned. Builder will fall back to auto-detection by root name. For sane parenting, select your BoatRoot and click Use Selection As Boat Root.",
                    MessageType.Warning);
            }
        }
    }

    private void DrawToolPicker()
    {
        EditorGUILayout.LabelField("Piece Type", EditorStyles.boldLabel);

        DrawToolGroup("Structure", new[]
        {
            Tool.HullSegment,
            Tool.Wall,
            Tool.Deck,
            Tool.ExteriorShell,
            Tool.CompartmentRect
        });

        DrawToolGroup("Access / Movement", new[]
        {
            Tool.Hatch,
            Tool.Door,
            Tool.Ledge,
            Tool.Ladder,
            Tool.Stairs
        });

        DrawToolGroup("Gameplay", new[]
        {
            Tool.PilotChair,
            Tool.BoatBoardObject,
            Tool.MapTable,
            Tool.PlayerSpawnPoint,
            Tool.BoardedVolume,
            Tool.BoatVisibilityZone,
            Tool.Hardpoint,
            Tool.TurretControllerChair
        });
    }

    private void DrawToolGroup(string label, Tool[] tools)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        const float buttonWidth = 110f;
        const float buttonHeight = 22f;
        const float spacing = 4f;

        // Approximate available width inside the helpbox/window content area.
        float availableWidth = Mathf.Max(120f, position.width - 40f);

        int perRow = Mathf.Max(1, Mathf.FloorToInt((availableWidth + spacing) / (buttonWidth + spacing)));

        int index = 0;
        while (index < tools.Length)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int rowCount = Mathf.Min(perRow, tools.Length - index);

                for (int i = 0; i < rowCount; i++)
                {
                    Tool tool = tools[index++];
                    bool selected = _tool == tool;

                    Color oldColor = GUI.backgroundColor;
                    if (selected)
                        GUI.backgroundColor = new Color(0.45f, 0.65f, 1f, 1f);

                    if (GUILayout.Button(
                        GetToolLabel(tool),
                        EditorStyles.miniButton,
                        GUILayout.Width(buttonWidth),
                        GUILayout.Height(buttonHeight)))
                    {
                        _tool = tool;
                        Persist();
                        SyncToSceneTools();
                        SceneView.RepaintAll();
                        GUI.FocusControl(null);
                    }

                    GUI.backgroundColor = oldColor;
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(2f);
        }

        EditorGUILayout.Space(3);
    }

    private static string GetToolLabel(Tool tool)
    {
        return tool switch
        {
            Tool.HullSegment => "Hull",
            Tool.Wall => "Wall",
            Tool.Hatch => "Hatch",
            Tool.Door => "Door",
            Tool.PilotChair => "Chair",
            Tool.CompartmentRect => "Compartment",
            Tool.Deck => "Deck",
            Tool.Ladder => "Ladder",
            Tool.Stairs => "Stairs",
            Tool.Ledge => "Ledge",
            Tool.BoatBoardObject => "Board",
            Tool.MapTable => "Map",
            Tool.PlayerSpawnPoint => "Spawn",
            Tool.BoardedVolume => "Volume",
            Tool.BoatVisibilityZone => "Visibility Zone",
            Tool.Hardpoint => "Hardpoint",
            Tool.TurretControllerChair => "Turret Chair",
            Tool.ExteriorShell => "Shell",
            _ => tool.ToString()
        };
    }

    private void DrawPlacementSettings()
    {
        _grid = EditorGUILayout.FloatField(new GUIContent("Grid Size", "World units"), _grid);
        _grid = Mathf.Max(0.01f, _grid);

        _zPlane = EditorGUILayout.FloatField(new GUIContent("Z Plane", "Where clicks place objects (2D usually 0)"), _zPlane);

        _autoParent = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto-parent to BoatRoot", "Parents under Fixed Boat Root if assigned, otherwise auto-detected BoatRoot."),
            _autoParent);

        _snapOnPlace = EditorGUILayout.ToggleLeft(
            new GUIContent("Snap on place", "Round position to grid on placement"),
            _snapOnPlace);

        _showPreview = EditorGUILayout.ToggleLeft(
            new GUIContent("Snap preview ghost", "Shows a green ghost of the selected prefab at the snapped placement position"),
            _showPreview);

        _hideShellWhileEditing = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "Hide exterior shell while editing",
                "Temporarily hides the boat exterior shell in Scene View using editor Scene Visibility. Does not disable renderers or GameObjects."),
            _hideShellWhileEditing);

        _enforceRequired = EditorGUILayout.ToggleLeft(
            new GUIContent("Enforce required pieces", "Prevents duplicate placement for unique required pieces and shows validation warnings"),
            _enforceRequired);
    }

    private void DrawToolSpecificSettings()
    {
        if (_tool == Tool.Hardpoint)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Hardpoint Authoring", EditorStyles.boldLabel);

            _hardpointType = (HardpointType)EditorGUILayout.EnumPopup(
                new GUIContent("Hardpoint Type", "Sets the placed hardpoint's runtime type"),
                _hardpointType);

            _hardpointStartingModuleDefinition = DrawCompatibleModuleDefinitionPopup(
                "Starting Module",
                _hardpointStartingModuleDefinition,
                _hardpointType);

            if (_hardpointStartingModuleDefinition != null &&
                !_hardpointStartingModuleDefinition.CanInstallOn(_hardpointType))
            {
                EditorGUILayout.HelpBox(
                    $"Selected module '{_hardpointStartingModuleDefinition.DisplayName}' does not allow hardpoint type '{_hardpointType}'.",
                    MessageType.Warning);
            }

            _hardpointIdPrefix = EditorGUILayout.TextField(
                new GUIContent("ID Prefix", "Base prefix used to generate IDs like engine_01"),
                _hardpointIdPrefix);

            _hardpointAutoCreateMountPoint = EditorGUILayout.ToggleLeft(
                new GUIContent("Auto-create MountPoint", "Creates/assigns a MountPoint child if missing"),
                _hardpointAutoCreateMountPoint);

            _hardpointRenameObjectToId = EditorGUILayout.ToggleLeft(
                new GUIContent("Rename object to ID", "Renames the placed hardpoint GameObject to the generated hardpoint ID"),
                _hardpointRenameObjectToId);

            EditorGUILayout.HelpBox(
                "Hardpoint prefab is selected from BoatKit based on the chosen Hardpoint Type. " +
                "Placed objects are post-configured so their runtime Hardpoint data matches the editor selection.",
                MessageType.Info);
        }

        if (_tool == Tool.Stairs)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Stair Authoring", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Orientation");

                if (GUILayout.Toggle(_stairAscendRight, "Ascend Right", EditorStyles.miniButtonLeft) != _stairAscendRight)
                    _stairAscendRight = true;

                if (GUILayout.Toggle(!_stairAscendRight, "Ascend Left", EditorStyles.miniButtonRight) == true && _stairAscendRight)
                    _stairAscendRight = false;
            }

            if (GUILayout.Button("Flip Stair Orientation"))
                _stairAscendRight = !_stairAscendRight;

            EditorGUILayout.HelpBox(
                "Controls whether placed stairs rise from left to right or right to left.",
                MessageType.Info);
        }

        if (_tool == Tool.BoatVisibilityZone)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Visibility Zone Authoring", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _visibilityZoneMode = (BoatVisibilityMode)EditorGUILayout.EnumPopup(
                new GUIContent("Mode", "Visibility mode applied while player overlaps this zone."),
                _visibilityZoneMode);

            _visibilityZoneUseDefaultPriority = EditorGUILayout.ToggleLeft(
                new GUIContent("Use default priority for mode", "Uses BoatVisibilityZone.GetDefaultPriority(mode)."),
                _visibilityZoneUseDefaultPriority);

            using (new EditorGUI.DisabledScope(_visibilityZoneUseDefaultPriority))
            {
                _visibilityZonePriority = EditorGUILayout.IntField(
                    new GUIContent("Priority", "Higher priority wins when zones overlap."),
                    _visibilityZonePriority);
            }

            if (EditorGUI.EndChangeCheck() && _visibilityZoneUseDefaultPriority)
                _visibilityZonePriority = BoatVisibilityZone.GetDefaultPriority(_visibilityZoneMode);

            EditorGUILayout.HelpBox(
                "Place trigger zones inside the boat. Highest-priority overlapping zone controls the boat visibility mode.",
                MessageType.Info);
        }
    }

    private void DrawPlacementButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Snap Selection (Ctrl/Cmd+Shift+G)", GUILayout.Height(28)))
                BoatBuilderSceneTools.SnapSelectionNow();

            if (GUILayout.Button(BoatBuilderSceneTools.IsPlacementEnabled ? "Disable Placement" : "Enable Placement", GUILayout.Height(28)))
                BoatBuilderSceneTools.TogglePlacement();
        }
    }

    private void DrawRequiredPiecesAndActions()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Required Pieces (best effort)", EditorStyles.boldLabel);

            var root = BoatBuilderSceneTools.PeekBestBoatRoot();
            if (root == null)
            {
                EditorGUILayout.HelpBox("Assign a Fixed Boat Root or select a Boat root to validate required pieces.", MessageType.Info);
                return;
            }

            BoatBuilderSceneTools.GetRequiredPiecesStatus(root, out var hasBoard, out var hasMap, out var spawnCount, out var hasVolume);

            DrawCheck("BoatBoardObject", hasBoard);
            DrawCheck("MapTable", hasMap);
            DrawCheck($"PlayerSpawnPoint x4 (found {spawnCount})", spawnCount >= 4);
            DrawCheck("BoardedVolume", hasVolume);

            int hardpointControllerWarnings = BoatBuilderSceneTools.CountHardpointControllerWarnings(root);
            DrawCheck($"Controllable hardpoints wired (warnings {hardpointControllerWarnings})", hardpointControllerWarnings == 0);

            DrawBuilderActionButtons(root);
        }
    }

    private void DrawBuilderActionButtons(Transform root)
    {
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Boat Root / Selection", EditorStyles.miniBoldLabel);
        DrawWrappedActionButtons(
            new ActionButtonDef("Select BoatRoot", () =>
            {
                Selection.activeTransform = root;
            }),
            new ActionButtonDef("Place First Missing", () =>
            {
                if (BoatBuilderSceneTools.TryGetFirstMissingRequiredTool(root, out var missing))
                {
                    _tool = missing;
                    Persist();
                    SyncToSceneTools();
                    BoatBuilderSceneTools.EnablePlacement();
                }
            })
        );

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField("Geometry / Topology", EditorStyles.miniBoldLabel);
        DrawWrappedActionButtons(
            new ActionButtonDef("Auto-fit Geometry", () =>
            {
                BoatBuilderSceneTools.AutoFitBoatGeometryFromVisualRenderers(root);
            }),
            new ActionButtonDef("Generate Topology", () =>
            {
                CompartmentTopologyGenerator.GenerateFromBoatRoot(root);
            }),
            new ActionButtonDef("Rebuild Compartments", () =>
            {
                BoatBuilderSceneTools.RebuildCompartmentsFromBoatRoot(root);
            }),
            new ActionButtonDef("Repair All Spans", () =>
            {
                int floorCount = SpanRepairUtility.RepairAllSpansUnderRoot(root);
                int wallCount = WallRepairUtility.RepairAllWallSpansUnderRoot(root);

                Debug.Log(
                    $"[BoatBuilder] Repair All complete. Floor spans repaired={floorCount}, wall spans repaired={wallCount}.",
                    root);
            })
        );

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField("Hardpoints / Modules", EditorStyles.miniBoldLabel);
        DrawWrappedActionButtons(
            new ActionButtonDef("Validate Controllers", () =>
            {
                BoatBuilderSceneTools.LogHardpointControllerWarnings(root);
            }),
            new ActionButtonDef("Link Turret Controller", () =>
            {
                BoatBuilderSceneTools.LinkSelectedTurretWithController();
            }),
            new ActionButtonDef("Apply Starting Module", () =>
            {
                BoatBuilderSceneTools.ApplyStartingModuleToSelectedHardpoints();
            }),
            new ActionButtonDef("Install Selected", () =>
            {
                BoatBuilderSceneTools.InstallSelectedStartingModules();
            }),
            new ActionButtonDef("Uninstall Selected", () =>
            {
                BoatBuilderSceneTools.UninstallSelectedModules();
            }),
            new ActionButtonDef("Install All Starting Modules", () =>
            {
                BoatBuilderSceneTools.InstallStartingModulesUnderRoot(root);
            })
        );
    }

    private void DrawNotesSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Notes", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(" Tool places objects on the Z Plane.");
            EditorGUILayout.LabelField(" Fixed Boat Root prevents accidental nested placement.");
        }
    }

    private static void DrawCheck(string label, bool ok)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var icon = ok ? "✅" : "❌";
            EditorGUILayout.LabelField($"{icon} {label}");
        }
    }

    private static ModuleDefinition DrawCompatibleModuleDefinitionPopup(
        string label,
        ModuleDefinition current,
        HardpointType hardpointType)
    {
        List<ModuleDefinition> modules = FindAllModuleDefinitionsCompatibleWith(hardpointType);

        List<string> names = new List<string>();
        names.Add("(None)");

        int selectedIndex = 0;

        for (int i = 0; i < modules.Count; i++)
        {
            ModuleDefinition module = modules[i];
            string displayName = module != null ? module.DisplayName : "Missing Module";
            names.Add(displayName);

            if (module == current)
                selectedIndex = i + 1;
        }

        int nextIndex = EditorGUILayout.Popup(label, selectedIndex, names.ToArray());

        if (nextIndex <= 0)
            return null;

        return modules[nextIndex - 1];
    }

    private static List<ModuleDefinition> FindAllModuleDefinitionsCompatibleWith(HardpointType hardpointType)
    {
        List<ModuleDefinition> results = new List<ModuleDefinition>();

        string[] guids = AssetDatabase.FindAssets("t:ModuleDefinition");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ModuleDefinition module = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(path);

            if (module == null)
                continue;

            if (!module.CanInstallOn(hardpointType))
                continue;

            results.Add(module);
        }

        results.Sort((a, b) =>
            string.Compare(
                a != null ? a.DisplayName : "",
                b != null ? b.DisplayName : "",
                System.StringComparison.OrdinalIgnoreCase));

        return results;
    }

    private void DrawWrappedActionButtons(params ActionButtonDef[] buttons)
    {
        const float buttonWidth = 180f;
        const float buttonHeight = 22f;
        const float spacing = 4f;

        float availableWidth = Mathf.Max(120f, position.width - 50f);
        int perRow = Mathf.Max(1, Mathf.FloorToInt((availableWidth + spacing) / (buttonWidth + spacing)));

        int index = 0;
        while (index < buttons.Length)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int rowCount = Mathf.Min(perRow, buttons.Length - index);

                for (int i = 0; i < rowCount; i++)
                {
                    ActionButtonDef def = buttons[index++];

                    if (GUILayout.Button(def.Label, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                        def.Action?.Invoke();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(2f);
        }
    }

    private void HandleSelectionChanged()
    {
        if (this == null)
            return;

        bool changed = TryRefreshStateFromSelection();

        if (changed)
        {
            Persist();
            SyncToSceneTools();
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private bool TryRefreshStateFromSelection()
    {
        bool changed = false;

        Hardpoint selectedHardpoint = FindSelectedComponentInParents<Hardpoint>();
        if (selectedHardpoint != null)
        {
            if (_tool != Tool.Hardpoint)
            {
                _tool = Tool.Hardpoint;
                changed = true;
            }

            if (_hardpointType != selectedHardpoint.HardpointType)
            {
                _hardpointType = selectedHardpoint.HardpointType;
                changed = true;
            }

            if (_hardpointStartingModuleDefinition != selectedHardpoint.StartingModuleDefinition)
            {
                _hardpointStartingModuleDefinition = selectedHardpoint.StartingModuleDefinition;
                changed = true;
            }

            string inferredPrefix = InferHardpointPrefix(
                selectedHardpoint.HardpointId,
                selectedHardpoint.HardpointType);

            if (!string.IsNullOrWhiteSpace(inferredPrefix) &&
                _hardpointIdPrefix != inferredPrefix)
            {
                _hardpointIdPrefix = inferredPrefix;
                changed = true;
            }

            return changed;
        }

        TurretControlStation selectedStation = FindSelectedComponentInParents<TurretControlStation>();
        if (selectedStation != null)
        {
            if (_tool != Tool.TurretControllerChair)
            {
                _tool = Tool.TurretControllerChair;
                changed = true;
            }

            return changed;
        }

        BoatVisibilityZone selectedZone = FindSelectedComponentInParents<BoatVisibilityZone>();
        if (selectedZone != null)
        {
            if (_tool != Tool.BoatVisibilityZone)
            {
                _tool = Tool.BoatVisibilityZone;
                changed = true;
            }

            if (_visibilityZoneMode != selectedZone.Mode)
            {
                _visibilityZoneMode = selectedZone.Mode;
                changed = true;
            }

            if (_visibilityZonePriority != selectedZone.Priority)
            {
                _visibilityZonePriority = selectedZone.Priority;
                _visibilityZoneUseDefaultPriority =
                    _visibilityZonePriority == BoatVisibilityZone.GetDefaultPriority(_visibilityZoneMode);
                changed = true;
            }

            return changed;
        }

        return false;
    }

    private static T FindSelectedComponentInParents<T>() where T : Component
    {
        UnityEngine.Object[] selected = Selection.objects;

        if (selected == null)
            return null;

        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] is GameObject go)
            {
                T found = go.GetComponentInParent<T>();
                if (found != null)
                    return found;
            }
            else if (selected[i] is Component c)
            {
                T found = c.GetComponentInParent<T>();
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static string InferHardpointPrefix(string hardpointId, HardpointType fallbackType)
    {
        if (!string.IsNullOrWhiteSpace(hardpointId))
        {
            string trimmed = hardpointId.Trim();

            int underscore = trimmed.LastIndexOf('_');
            if (underscore > 0 && underscore < trimmed.Length - 1)
            {
                string suffix = trimmed.Substring(underscore + 1);
                if (int.TryParse(suffix, out _))
                    return trimmed.Substring(0, underscore);
            }
        }

        return fallbackType switch
        {
            HardpointType.Engine => "engine",
            HardpointType.Pump => "pump",
            HardpointType.Utility => "utility",
            HardpointType.Weapon => "weapon",
            HardpointType.Electronics => "electronics",
            HardpointType.Helm => "helm",
            _ => "hardpoint"
        };
    }

    private void ApplyShellVisibilityToCurrentRoot()
    {
        Transform root = BoatBuilderSceneTools.PeekBestBoatRoot();
        if (root == null)
            return;

        BoatBuilderSceneTools.SetExteriorShellEditorHidden(root, _hideShellWhileEditing);
    }
}
#endif