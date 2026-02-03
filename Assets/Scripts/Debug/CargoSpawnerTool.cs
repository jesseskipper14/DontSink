using UnityEditor;
using UnityEngine;

public class CargoSpawnerTool : EditorWindow
{
    float mass = 20f;
    float size = 1f;
    float dropHeight = 5f;

    [MenuItem("Tools/Cargo Spawner")]
    static void Open()
    {
        GetWindow<CargoSpawnerTool>("Cargo Spawner");
    }

    void OnGUI()
    {
        GUILayout.Label("Spawn Test Cargo", EditorStyles.boldLabel);

        mass = EditorGUILayout.FloatField("Mass", mass);
        size = EditorGUILayout.FloatField("Size", size);
        dropHeight = EditorGUILayout.FloatField("Drop Height", dropHeight);

        if (GUILayout.Button("Spawn Above Selected Boat"))
        {
            Spawn();
        }
    }

    void Spawn()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Select a Boat first.");
            return;
        }

        Boat boat = Selection.activeGameObject.GetComponent<Boat>();
        if (!boat)
        {
            Debug.Log(Selection.activeGameObject);
            Debug.LogWarning("Selected object is not a Boat.");
            return;
        }

        GameObject go = new GameObject("CargoBox");
        go.transform.position =
            boat.transform.position + Vector3.up * dropHeight;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = mass;  // whatever you set in the editor
        rb.gravityScale = 1f;  // important! ensures it falls
        rb.constraints = RigidbodyConstraints2D.None;

        var cargo = go.AddComponent<Cargo>();
        cargo.mass = mass;
        cargo.size = Vector2.one * size;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = cargo.size;
        col.isTrigger = false;

        Debug.Log($"[CargoSpawner] Spawned cargo ({mass}kg)");
    }
}
