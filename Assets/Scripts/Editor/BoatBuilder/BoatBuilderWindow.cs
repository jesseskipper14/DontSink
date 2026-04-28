#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class BoatBuilderWindow : EditorWindow
{
    private const string Prefs_Grid = "BoatBuilder.Grid";
    private const string Prefs_Z = "BoatBuilder.ZPlane";
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
    }

    private BoatKit _kit;
    private Transform _boatRootOverride;

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

        var guid = EditorPrefs.GetString(Prefs_KIT_GUID, "");
        if (!string.IsNullOrEmpty(guid))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            _kit = AssetDatabase.LoadAssetAtPath<BoatKit>(path);
        }

        TryRestoreBoatRootOverride();

        BoatBuilderSceneTools.Attach();
        SyncToSceneTools();
    }

    private void OnDisable()
    {
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
        });
    }

    private static readonly GUIContent[] ToolTabs =
    {
        new GUIContent("Hull"),
        new GUIContent("Wall"),
        new GUIContent("Hatch"),
        new GUIContent("Chair"),
        new GUIContent("Compartment"),
        new GUIContent("Deck"),
        new GUIContent("Ladder"),
        new GUIContent("Stairs"),
        new GUIContent("Ledge"),
        new GUIContent("Board"),
        new GUIContent("Map"),
        new GUIContent("Spawn"),
        new GUIContent("Volume"),
        new GUIContent("Hardpoint"),
        new GUIContent("Shell"),
    };

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

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
                        SceneView.RepaintAll();
                    }
                    else
                    {
                        Debug.LogWarning("[BoatBuilder] No selection to use as Boat Root.");
                    }
                }

                if (GUILayout.Button("Clear Fixed Root"))
                {    if (GUILayout.Button("Select BoatRoot"))

                    _boatRootOverride = null;
                    Persist();
                    SyncToSceneTools();
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

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();

            _tool = (Tool)GUILayout.Toolbar((int)_tool, ToolTabs);

            if (EditorGUI.EndChangeCheck())
            {
                Persist();
                SyncToSceneTools();
                SceneView.RepaintAll();
            }

            EditorGUI.BeginChangeCheck();

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

            _enforceRequired = EditorGUILayout.ToggleLeft(
                new GUIContent("Enforce required pieces", "Prevents duplicate placement for unique required pieces and shows validation warnings"),
                _enforceRequired);

            if (_tool == Tool.Hardpoint)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Hardpoint Authoring", EditorStyles.boldLabel);

                _hardpointType = (HardpointType)EditorGUILayout.EnumPopup(
                    new GUIContent("Hardpoint Type", "Sets the placed hardpoint's runtime type"),
                    _hardpointType);

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
                    {
                        _stairAscendRight = true;
                    }

                    if (GUILayout.Toggle(!_stairAscendRight, "Ascend Left", EditorStyles.miniButtonRight) == true && _stairAscendRight)
                    {
                        _stairAscendRight = false;
                    }
                }

                if (GUILayout.Button("Flip Stair Orientation"))
                {
                    _stairAscendRight = !_stairAscendRight;
                }

                EditorGUILayout.HelpBox(
                    "Controls whether placed stairs rise from left to right or right to left.",
                    MessageType.Info);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Persist();
                SyncToSceneTools();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Snap Selection (Ctrl/Cmd+Shift+G)", GUILayout.Height(28)))
                    BoatBuilderSceneTools.SnapSelectionNow();

                if (GUILayout.Button(BoatBuilderSceneTools.IsPlacementEnabled ? "Disable Placement" : "Enable Placement", GUILayout.Height(28)))
                    BoatBuilderSceneTools.TogglePlacement();
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Required Pieces (best effort)", EditorStyles.boldLabel);

                var root = BoatBuilderSceneTools.PeekBestBoatRoot();
                if (root == null)
                {
                    EditorGUILayout.HelpBox("Assign a Fixed Boat Root or select a Boat root to validate required pieces.", MessageType.Info);
                }
                else
                {
                    BoatBuilderSceneTools.GetRequiredPiecesStatus(root, out var hasBoard, out var hasMap, out var spawnCount, out var hasVolume);

                    DrawCheck("BoatBoardObject", hasBoard);
                    DrawCheck("MapTable", hasMap);
                    DrawCheck($"PlayerSpawnPoint x4 (found {spawnCount})", spawnCount >= 4);
                    DrawCheck("BoardedVolume", hasVolume);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select BoatRoot"))
                            Selection.activeTransform = root;

                        if (GUILayout.Button("Rebuild Compartments"))
                            BoatBuilderSceneTools.RebuildCompartmentsFromBoatRoot(root);

                        if (GUILayout.Button("Repair All Spans"))
                            SpanRepairUtility.RepairAllSpansUnderRoot(root);

                        if (GUILayout.Button("Place First Missing"))
                        {
                            if (BoatBuilderSceneTools.TryGetFirstMissingRequiredTool(root, out var missing))
                            {
                                _tool = missing;
                                Persist();
                                SyncToSceneTools();
                                BoatBuilderSceneTools.EnablePlacement();
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Scene Controls:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(" Left-click: place selected prefab");
            EditorGUILayout.LabelField(" Right-click or Esc: cancel placement");
        }

        EditorGUILayout.Space(6);

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
}
#endif