#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class BoatBuilderWindow : EditorWindow
{
    private const string Prefs_Grid = "BoatBuilder.Grid";
    private const string Prefs_Z = "BoatBuilder.ZPlane";
    private const string Prefs_SelectedTool = "BoatBuilder.Tool";
    private const string Prefs_KIT_GUID = "BoatBuilder.KitGuid";
    private const string Prefs_AutoParent = "BoatBuilder.AutoParent";
    private const string Prefs_SnapOnPlace = "BoatBuilder.SnapOnPlace";

    public enum Tool
    {
        HullSegment = 0,
        Wall = 1,
        Hatch = 2,
        PilotChair = 3,
        CompartmentRect = 4,
        Deck = 5, // NEW
    }

    private BoatKit _kit;
    private Tool _tool;
    private float _grid = 1f;
    private float _zPlane = 0f;
    private bool _autoParent = true;
    private bool _snapOnPlace = true;

    [MenuItem("Tools/Boat Builder/Window")]
    public static void Open()
    {
        var w = GetWindow<BoatBuilderWindow>("Boat Builder");
        w.minSize = new Vector2(340, 240);
        w.Show();
    }

    private void OnEnable()
    {
        _grid = Mathf.Max(0.01f, EditorPrefs.GetFloat(Prefs_Grid, 1f));
        _zPlane = EditorPrefs.GetFloat(Prefs_Z, 0f);
        _tool = (Tool)EditorPrefs.GetInt(Prefs_SelectedTool, 0);
        _autoParent = EditorPrefs.GetBool(Prefs_AutoParent, true);
        _snapOnPlace = EditorPrefs.GetBool(Prefs_SnapOnPlace, true);

        // Restore last-used kit (if any)
        var guid = EditorPrefs.GetString(Prefs_KIT_GUID, "");
        if (!string.IsNullOrEmpty(guid))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            _kit = AssetDatabase.LoadAssetAtPath<BoatKit>(path);
        }

        BoatBuilderSceneTools.Attach();
        SyncToSceneTools();
    }

    private void OnDisable()
    {
        Persist();
        SyncToSceneTools();
    }

    private void Persist()
    {
        EditorPrefs.SetFloat(Prefs_Grid, _grid);
        EditorPrefs.SetFloat(Prefs_Z, _zPlane);
        EditorPrefs.SetInt(Prefs_SelectedTool, (int)_tool);
        EditorPrefs.SetBool(Prefs_AutoParent, _autoParent);
        EditorPrefs.SetBool(Prefs_SnapOnPlace, _snapOnPlace);

        if (_kit != null)
        {
            var path = AssetDatabase.GetAssetPath(_kit);
            var guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(Prefs_KIT_GUID, guid);
        }
    }

    private void SyncToSceneTools()
    {
        BoatBuilderSceneTools.SetContext(new BoatBuilderSceneTools.Context
        {
            Kit = _kit,
            ActiveTool = _tool,
            GridSize = _grid,
            ZPlane = _zPlane,
            AutoParentToBoatRoot = _autoParent,
            SnapOnPlace = _snapOnPlace
        });
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _kit = (BoatKit)EditorGUILayout.ObjectField("Boat Kit", _kit, typeof(BoatKit), false);
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
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _tool = (Tool)GUILayout.Toolbar((int)_tool,
                new[] { "Hull", "Wall", "Hatch", "Chair", "Compartment", "Deck" });
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

            _autoParent = EditorGUILayout.ToggleLeft(new GUIContent("Auto-parent to BoatRoot", "Best-effort: parents under selected object or closest parent named like a boat root"), _autoParent);
            _snapOnPlace = EditorGUILayout.ToggleLeft(new GUIContent("Snap on place", "Round position to grid on placement"), _snapOnPlace);

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

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Scene Controls:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Left-click: place selected prefab");
            EditorGUILayout.LabelField("• Right-click or Esc: cancel placement");
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Notes", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• This tool assumes a 2D plane at Z Plane.");
            EditorGUILayout.LabelField("• If parenting feels wrong, select your Boat root before placing.");
        }
    }
}
#endif
