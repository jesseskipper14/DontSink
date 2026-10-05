using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Simple physical lamp switch. No power/fuel consumption in this first version.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D), typeof(BoxCollider2D))]
public sealed class BasicLamp : MonoBehaviour, IInteractable, IInteractPromptProvider,
    IInteractionLabelProvider, IInteractionPromptDisplayPolicyProvider
{
    [SerializeField] private bool startsOn = true;
    [SerializeField, Min(.1f)] private float maxUseDistance = 1.5f;
    [SerializeField] private int interactionPriority = 80;
    [Tooltip("Optional separate switch position. Blank uses this object.")]
    [SerializeField] private Transform promptAnchor;
    [Tooltip("Optional fixture sprite and replacement artwork while switched off.")]
    [SerializeField] private SpriteRenderer fixtureRenderer;
    [SerializeField] private Sprite offSprite;
    private Sprite onSprite;
    private Light2D lamp;
    private float onIntensity;
    public bool IsOn { get; private set; }
    public int InteractionPriority => interactionPriority;

    private void Reset()
    {
        // Defaults only when the user adds/resets this component in the Inspector.
        var light = GetComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.intensity = 1f;
        light.color = new Color(1f, .9f, .7f);
        light.pointLightInnerRadius = 1f;
        light.pointLightOuterRadius = 8f;
        light.pointLightInnerAngle = 360f;
        light.pointLightOuterAngle = 360f;
        var layers = SortingLayer.layers;
        var ids = new int[layers.Length];
        for (int i = 0; i < ids.Length; i++) ids[i] = layers[i].id;
        light.targetSortingLayers = ids;
        GetComponent<BoxCollider2D>().isTrigger = true;
        fixtureRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        lamp = GetComponent<Light2D>();
        onIntensity = lamp.intensity;
        if (fixtureRenderer == null) fixtureRenderer = GetComponent<SpriteRenderer>();
        if (fixtureRenderer != null) onSprite = fixtureRenderer.sprite;
        ApplyState(startsOn);
    }

    private void OnEnable()
    {
        if (lamp != null) ApplyState(IsOn);
    }

    private void OnDisable()
    {
        if (lamp != null) lamp.intensity = 0f;
        EnvironmentDepthLighting.Instance?.RefreshNow();
    }

    public bool CanInteract(in InteractContext context) =>
        GameplayAuthority.IsAuthoritative && context.InteractorGO != null && lamp != null &&
        Vector2.Distance(context.Origin, GetPromptAnchor().position) <= maxUseDistance;

    public void Interact(in InteractContext context)
    {
        if (CanInteract(context)) ApplyState(!IsOn);
    }

    private void ApplyState(bool on)
    {
        IsOn = on;
        if (lamp != null)
        {
            if (!on && lamp.intensity > 0f) onIntensity = lamp.intensity;
            // Leave a zero-intensity light registered. Removing the last light
            // from a URP sorting batch can select the full-bright unlit fallback.
            lamp.enabled = true;
            lamp.intensity = on ? onIntensity : 0f;
        }
        if (fixtureRenderer != null && offSprite != null) fixtureRenderer.sprite = on ? onSprite : offSprite;
        EnvironmentDepthLighting.Instance?.RefreshNow();
    }

    public string GetPromptVerb(in InteractContext context) => IsOn ? "Turn Off" : "Turn On";
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
    public string GetInteractionLabel(in InteractContext context) => "Lamp";
    public bool ShouldShowHoverLabel(in InteractContext context) => CanInteract(context);
}
