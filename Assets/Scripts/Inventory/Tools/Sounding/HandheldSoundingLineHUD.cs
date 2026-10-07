using UnityEngine;

/// <summary>
/// Minimal direct readout for the handheld sounding line.
/// Presentation only. All state comes from HandheldSoundingLineController.
/// </summary>
[DisallowMultipleComponent]
public sealed class HandheldSoundingLineHUD :
    MonoBehaviour
{
    [SerializeField] private HandheldSoundingLineController controller;

    [Header("World Follow")]
    [Tooltip(
        "World transform the HUD follows. Defaults to the sounding-line controller/player.")]
    [SerializeField] private Transform playerAnchor;

    [Tooltip(
        "World-space offset from the player. Default places the panel about five Unity units above Steve.")]
    [SerializeField]
    private Vector3 playerWorldOffset =
        new Vector3(0f, 5f, 0f);

    [SerializeField] private Camera worldCamera;

    [Tooltip("Keep the panel visible if the player approaches the edge of the screen.")]
    [SerializeField] private bool clampToScreen = true;

    [SerializeField, Min(0f)] private float screenEdgePadding = 8f;

    [Header("Layout")]
    [SerializeField, Min(180f)] private float width = 300f;
    [SerializeField, Min(120f)] private float height = 192f;

    private GUIStyle _titleStyle;
    private GUIStyle _readoutStyle;
    private GUIStyle _statusStyle;

    public bool IsReadoutVisible
    {
        get
        {
            ResolveRefs();
            return isActiveAndEnabled && controller != null && CameraManager.HasGameplayInput(controller) &&
                controller.ShouldShowReadout && (worldCamera != null || CameraManager.CameraForActor(controller) != null);
        }
    }

    private void Reset()
    {
        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnGUI()
    {
        ResolveRefs();

        if (!IsReadoutVisible)
        {
            return;
        }

        Camera cam =
            worldCamera != null
                ? worldCamera
                : CameraManager.CameraForActor(controller);

        Transform anchor =
            playerAnchor != null
                ? playerAnchor
                : controller.transform;

        if (cam == null ||
            anchor == null)
        {
            return;
        }

        Vector3 screenPoint =
            cam.WorldToScreenPoint(
                anchor.position +
                playerWorldOffset);

        // Behind the camera.
        if (screenPoint.z < 0f)
            return;

        EnsureStyles();

        // IMGUI Y runs down from the top, while WorldToScreenPoint Y runs up
        // from the bottom.
        float panelHeight = Mathf.Max(height, 192f);
        float panelX =
            screenPoint.x -
            width * 0.5f;

        float panelY =
            Screen.height -
            screenPoint.y -
            panelHeight * 0.5f;

        if (clampToScreen)
        {
            panelX =
                Mathf.Clamp(
                    panelX,
                    screenEdgePadding,
                    Mathf.Max(
                        screenEdgePadding,
                        Screen.width -
                        width -
                        screenEdgePadding));

            panelY =
                Mathf.Clamp(
                    panelY,
                    screenEdgePadding,
                    Mathf.Max(
                        screenEdgePadding,
                        Screen.height -
                        panelHeight -
                        screenEdgePadding));
        }

        Rect panel =
            new Rect(
                panelX,
                panelY,
                width,
                panelHeight);

        panel.height = Mathf.Max(panel.height, 192f);
        GUI.Box(
            panel,
            GUIContent.none);

        GUI.Label(
            new Rect(
                panel.x + 10f,
                panel.y + 7f,
                panel.width - 20f,
                20f),
            "SOUNDING LINE",
            _titleStyle);

        string ropeText =
            controller.IsDeployed
                ? $"ROPE OUT: {controller.RopeOutMeters:0.0} / {controller.AvailableLineMeters:0.0} m"
                : $"LINE LOADED: {controller.AvailableLineMeters:0.0} m";

        GUI.Label(
            new Rect(
                panel.x + 10f,
                panel.y + 31f,
                panel.width - 20f,
                22f),
            ropeText,
            _readoutStyle);

        GUI.Label(
            new Rect(
                panel.x + 10f,
                panel.y + 54f,
                panel.width - 20f,
                panel.height - 126f),
            controller.StatusText + "\n" + ResolveControlHint(),
            _statusStyle);
        DrawChartAction(panel);
    }

    private void DrawChartAction(Rect panel)
    {
        bool prior = GUI.enabled;
        GUI.enabled = prior && controller.CanRecordSounding;
        if (GUI.Button(new Rect(panel.x + 10, panel.yMax - 62, panel.width - 20, 25), "Record sounding — 1 Charting Paper"))
            controller.TryRecordSounding(out _);
        GUI.enabled = prior;
        GUI.Label(new Rect(panel.x + 10, panel.yMax - 35, panel.width - 20, 32), controller.SoundingChartNote ?? "Process recorded charts at a Surveyor.");
    }

    private string ResolveControlHint()
    {
        var source = controller.GetComponent<LocalHandheldSoundingLineIntentSource>();
        string key = source != null && source.UseKey != KeyCode.None ? source.UseKey.ToString().ToUpperInvariant() : "G";
        return controller.ControlHintText.Replace("PRESS G", "PRESS " + key);
    }

    private void EnsureStyles()
    {
        if (_titleStyle == null)
        {
            _titleStyle =
                new GUIStyle(
                    GUI.skin.label)
                {
                    fontStyle =
                        FontStyle.Bold,

                    alignment =
                        TextAnchor.MiddleCenter
                };
        }

        if (_readoutStyle == null)
        {
            _readoutStyle =
                new GUIStyle(
                    GUI.skin.label)
                {
                    alignment =
                        TextAnchor.MiddleCenter
                };
        }

        if (_statusStyle == null)
        {
            _statusStyle =
                new GUIStyle(
                    GUI.skin.label)
                {
                    fontStyle =
                        FontStyle.Bold,

                    alignment =
                        TextAnchor.MiddleCenter,

                    wordWrap =
                        true
                };
        }

    }

    private void ResolveRefs()
    {
        if (controller == null)
        {
            controller =
                GetComponent<HandheldSoundingLineController>();

            if (controller == null)
            {
                controller =
                    GetComponentInParent<HandheldSoundingLineController>();
            }
        }

        if (playerAnchor == null &&
            controller != null)
        {
            playerAnchor =
                controller.transform;
        }

    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        width =
            Mathf.Max(
                180f,
                width);

        height =
            Mathf.Max(
                120f,
                height);

        screenEdgePadding =
            Mathf.Max(
                0f,
                screenEdgePadding);
    }
#endif
}
