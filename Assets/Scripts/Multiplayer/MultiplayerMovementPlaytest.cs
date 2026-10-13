using System;
using System.Collections.Generic;
using UnityEngine;

// Reversible MP.2 fixture, not a replacement for BoatScene/NodeScene generation.
// Both peers build the same static collision geometry; only the host simulates actors.
public sealed class MultiplayerMovementPlaytest : IDisposable
{
    public static readonly Vector2 Origin = new Vector2(10000, 10000);
    private readonly GameObject root;
    private readonly Dictionary<Behaviour, bool> presentation = new();
    private readonly Material material;
    private readonly Texture2D texture;
    private readonly Sprite sprite;
    private CameraManager cameraManager;
    private readonly List<GameObject> groundObjects = new();
    public NetworkPlayerActor LocalPlayer { get; private set; }

    public MultiplayerMovementPlaytest(int capacity)
    {
        // Only suspend menu presentation. Keep physics/waves and session services alive.
        foreach (var manager in UnityEngine.Object.FindObjectsByType<CameraManager>(FindObjectsSortMode.None)) Suspend(manager);
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Suspend(canvas);
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Suspend(camera);
        foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) Suspend(listener);
        root = new GameObject("_MP2_MovementPlaytest");
        texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
        float width = Mathf.Max(64, capacity * 3 + 4);
        Ground("Deck", new Vector2(0, -.5f), new Vector2(width, 1), new Color(.35f, .29f, .19f));
        Ground("Left end", new Vector2(-width / 2, 3), new Vector2(1, 7), new Color(.25f, .29f, .32f));
        Ground("Right end", new Vector2(width / 2, 3), new Vector2(1, 7), new Color(.25f, .29f, .32f));
        Ground("Step", new Vector2(12, .5f), new Vector2(3, 1), new Color(.45f, .43f, .39f));
    }

    private void Suspend(Behaviour component)
    {
        presentation[component] = component.enabled;
        component.enabled = false;
    }

    private void Ground(string name, Vector2 offset, Vector2 size, Color color)
    {
        var ground = new GameObject(name);
        groundObjects.Add(ground);
        ground.transform.SetParent(root.transform);
        ground.transform.position = Origin + offset;
        int layer = LayerMask.NameToLayer("Ground");
        if (layer < 0) throw new InvalidOperationException("MP.2 requires the existing Ground layer.");
        ground.layer = layer;
        var collider = ground.AddComponent<BoxCollider2D>();
        collider.size = size;
        var renderer = ground.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.sortingOrder = -1;
    }

    public void Present(NetworkPlayerActor actor)
    {
        // The fixture uses unlit materials so authored night lighting cannot hide the test.
        foreach (var renderer in actor.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = material;
        if (!actor.IsOwner) return;
        LocalPlayer = actor;
        if (cameraManager == null)
        {
            var cameraObject = new GameObject("Local participant camera");
            cameraObject.SetActive(false);
            cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .13f, .19f);
            cameraObject.AddComponent<AudioListener>();
            cameraManager = cameraObject.AddComponent<CameraManager>();
            cameraManager.mainCamera = camera;
            cameraManager.defaultOrthoSize = 7;
            cameraManager.BindPlayer(actor.transform, true);
            cameraObject.transform.position = actor.transform.position + Vector3.back * 10;
            cameraObject.SetActive(true);
        }
        else cameraManager.BindPlayer(actor.transform, true);
    }

    public void UseBoatDeck()
    {
        foreach (var ground in groundObjects) ground.SetActive(false);
    }

    public void PlayerRemoved(NetworkPlayerActor actor)
    {
        if (LocalPlayer != actor) return;
        LocalPlayer = null;
        if (cameraManager != null) cameraManager.BindPlayer(null, false);
    }

    public void Dispose()
    {
        if (cameraManager != null) cameraManager.BindPlayer(null, false);
        UnityEngine.Object.Destroy(root);
        UnityEngine.Object.Destroy(material);
        UnityEngine.Object.Destroy(sprite);
        UnityEngine.Object.Destroy(texture);
        foreach (var pair in presentation) if (pair.Key != null) pair.Key.enabled = pair.Value;
        presentation.Clear();
    }
}
