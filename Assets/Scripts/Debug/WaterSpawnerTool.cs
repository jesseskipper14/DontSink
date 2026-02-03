//using UnityEditor;
//using UnityEngine;

//public class WaterSpawnerTool : EditorWindow
//{
//    float spawnRate = 20f;
//    float dropSize = 0.2f;
//    bool spraying;

//    [MenuItem("Tools/Debug Water Spawner")]
//    public static void Open()
//    {
//        GetWindow<WaterSpawnerTool>("Water Spawner");
//    }

//    void OnGUI()
//    {
//        GUILayout.Label("Debug Water Rain", EditorStyles.boldLabel);

//        spawnRate = EditorGUILayout.Slider("Drops / second", spawnRate, 1f, 100f);
//        dropSize = EditorGUILayout.Slider("Drop size", dropSize, 0.05f, 0.5f);

//        GUILayout.Space(10);
//        GUILayout.Label("Hold LEFT mouse in Scene View to spawn water");
//    }

//    void OnEnable()
//    {
//        SceneView.duringSceneGui += OnSceneGUI;
//    }

//    void OnDisable()
//    {
//        SceneView.duringSceneGui -= OnSceneGUI;
//    }

//    void OnSceneGUI(SceneView view)
//    {
//        Event e = Event.current;

//        if (e.type == EventType.MouseDown && e.button == 0)
//            spraying = true;

//        if (e.type == EventType.MouseUp && e.button == 0)
//            spraying = false;

//        if (!spraying) return;

//        if (e.type == EventType.Repaint)
//        {
//            float dt = Time.deltaTime;
//            int count = Mathf.CeilToInt(spawnRate * dt);

//            for (int i = 0; i < count; i++)
//                SpawnDrop(e.mousePosition);
//        }
//    }

//    void SpawnDrop(Vector2 mousePos)
//    {
//        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
//        Vector3 pos = ray.origin;
//        pos.z = 0f;

//        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Quad);
//        drop.name = "DebugWater";
//        drop.transform.position = pos;
//        drop.transform.localScale = Vector3.one * dropSize;

//        var rend = drop.GetComponent<MeshRenderer>();
//        rend.material = new Material(Shader.Find("Unlit/Color"));
//        rend.material.color = new Color(0.2f, 0.4f, 1f, 0.7f);

//        DestroyImmediate(drop.GetComponent<Collider>());

//        drop.AddComponent<DebugWaterDrop>();
//    }
//}
