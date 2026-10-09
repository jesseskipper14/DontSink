using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class PlayerInventoryInput : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Vector3 dropOffset = new(0.75f, 0f, 0f);

    [Header("Charged Throw")]
    [SerializeField, Min(.05f)] private float throwHoldThreshold = .2f;
    [SerializeField, Min(.05f)] private float fullChargeDuration = .8f;
    [SerializeField, Min(0f)] private float minimumThrowSpeed = 8f;
    [SerializeField, Min(0f)] private float maximumThrowSpeed = 24f;
    [SerializeField] private AnimationCurve throwChargeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField, Range(.05f, .5f)] private float throwerCollisionGraceMaxSeconds = .25f;
    [SerializeField, Range(.2f, 2f)] private float previewDuration = 1.1f;
    [SerializeField, Range(8, 48)] private int previewSampleCount = 24;

    private ItemInstance pendingItem;
    private BottomBarSlotType pendingSlot;
    private float pressedAt;
    private bool releaseRequested;
    private bool throwCandidate;
    private LineRenderer preview;
    private Material previewMaterial;
    private Vector3[] previewPoints;
    public float ThrowerGraceSeconds => Mathf.Clamp(throwerCollisionGraceMaxSeconds, .05f, .5f);

    private void Awake()
    {
        if (inventory == null) inventory = GetComponentInParent<PlayerInventory>(true);
    }

    private bool CanUseInput() => inventory != null && Time.timeScale > 0f &&
        !GameplayInputBlocker.IsBlocked && CameraManager.HasGameplayInput(this);

    private void Update()
    {
        if (!CanUseInput()) { CancelThrow(); return; }
        HandleSelection();
        if (pendingItem != null && (inventory.SelectedSlot != pendingSlot ||
            !ReferenceEquals(GetSelectedItem(), pendingItem))) CancelThrow();
        if (Input.GetKeyDown(KeyCode.Q))
        {
            pendingItem = GetSelectedItem();
            pendingSlot = inventory.SelectedSlot;
            pressedAt = Time.unscaledTime;
            releaseRequested = false;
            throwCandidate = pendingSlot == BottomBarSlotType.Hands && inventory.CanThrowHeld(pendingItem);
        }
        if (pendingItem != null && Input.GetKeyUp(KeyCode.Q)) releaseRequested = true;
    }

    // Resolve mouse aim after CameraManager's follow update, just like interaction aim.
    private void LateUpdate()
    {
        if (pendingItem == null) return;
        if (!CanUseInput() || inventory.SelectedSlot != pendingSlot ||
            !ReferenceEquals(GetSelectedItem(), pendingItem) ||
            (throwCandidate && !inventory.CanThrowHeld(pendingItem))) { CancelThrow(); return; }
        float heldTime = Time.unscaledTime - pressedAt;
        bool throwing = throwCandidate && heldTime >= Mathf.Max(.05f, throwHoldThreshold);
        float charge = Mathf.Clamp01((heldTime - Mathf.Max(.05f, throwHoldThreshold)) /
            Mathf.Max(.05f, fullChargeDuration));
        var camera = CameraManager.CameraForActor(this);
        if (camera == null) { CancelThrow(); return; }
        Vector2 renderOrigin = GetDropWorldPositionForUI();
        Vector2 mouse = camera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mouse - renderOrigin;
        if (direction.sqrMagnitude < .0001f)
            direction = transform.lossyScale.x < 0f ? Vector2.left : Vector2.right;
        direction.Normalize();
        if (releaseRequested)
        {
            string itemId = pendingItem.InstanceId;
            CancelThrow();
            if (throwing)
            {
                if (!inventory.TryThrowHeld(itemId, direction, charge, out _, out string reason))
                    Debug.LogWarning("[Throw] " + reason, this);
            }
            else if (GameplayAuthority.IsAuthoritative)
                inventory.TryDropSelected(GetDropWorldPositionForUI());
            return;
        }
        if (throwing) ShowPreview(renderOrigin, direction, charge);
    }

    private ItemInstance GetSelectedItem()
    {
        if (inventory == null) return null;
        var slot = inventory.SelectedSlot;
        if (slot >= BottomBarSlotType.Hotbar0 && slot <= BottomBarSlotType.Hotbar7)
            return inventory.GetSlot(PlayerInventory.SlotTypeToHotbarIndex(slot))?.Instance;
        return inventory.Equipment != null ? inventory.Equipment.Get(slot) : null;
    }

    public float GetThrowSpeed(float charge)
    {
        float curved = throwChargeCurve != null ? throwChargeCurve.Evaluate(Mathf.Clamp01(charge)) : charge;
        if (!WorldTopology.IsFinite(curved)) curved = 0f;
        float minimum = Mathf.Clamp(minimumThrowSpeed, 0f, 80f);
        float maximum = Mathf.Clamp(maximumThrowSpeed, minimum, 80f);
        return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(curved));
    }

    public Vector3 GetThrowWorldPosition()
    {
        Vector3 origin = GetDropWorldPositionForUI();
        Vector2 physical = PhysicsFrame2D.ToPhysics(inventory.GetComponentInParent<Rigidbody2D>(), origin);
        return new Vector3(physical.x, physical.y, origin.z);
    }

    private void ShowPreview(Vector2 origin, Vector2 direction, float charge)
    {
        if (preview == null)
        {
            var go = new GameObject("Local Throw Preview");
            go.transform.SetParent(transform, false);
            preview = go.AddComponent<LineRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader == null) { Destroy(go); return; }
            previewMaterial = new Material(shader);
            preview.sharedMaterial = previewMaterial;
            preview.useWorldSpace = true;
            preview.sortingLayerName = "BoatPlayer";
            preview.sortingOrder = 1000;
            preview.numCapVertices = 3;
        }
        int samples = Mathf.Clamp(previewSampleCount, 8, 48);
        if (previewPoints == null || previewPoints.Length != samples) previewPoints = new Vector3[samples];
        Vector2 velocity = WorldItemDropUtility.GetReleaseVelocity(inventory.gameObject, GetThrowWorldPosition(), out _) +
            direction * GetThrowSpeed(charge);
        var itemBody = pendingItem.Definition.WorldPrefab.GetComponentInChildren<Rigidbody2D>(true);
        Vector2 gravity = Physics2D.gravity * (itemBody != null ? itemBody.gravityScale : 1f);
        for (int i = 0; i < samples; i++)
        {
            float t = i * Mathf.Clamp(previewDuration, .2f, 2f) / (samples - 1);
            Vector2 p = origin + velocity * t + .5f * gravity * t * t;
            previewPoints[i] = new Vector3(p.x, p.y, transform.position.z);
        }
        preview.positionCount = samples;
        preview.SetPositions(previewPoints);
        preview.startWidth = .035f + charge * .02f;
        preview.endWidth = .015f;
        Color color = Color.Lerp(new Color(.7f, .95f, 1f, .8f), new Color(1f, .85f, .4f, .95f), charge);
        preview.startColor = color;
        color.a *= .3f;
        preview.endColor = color;
        preview.enabled = true;
    }

    private void CancelThrow()
    {
        pendingItem = null;
        releaseRequested = false;
        throwCandidate = false;
        if (preview != null) preview.enabled = false;
    }
    private void OnDisable() => CancelThrow();
    private void OnApplicationFocus(bool focused) { if (!focused) CancelThrow(); }
    private void OnApplicationPause(bool paused) { if (paused) CancelThrow(); }
    private void OnDestroy()
    {
        if (preview != null) Destroy(preview.gameObject);
        if (previewMaterial != null) Destroy(previewMaterial);
    }

    private void HandleSelection()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f) inventory.CycleSelection(-1);
        else if (scroll < -0.01f) inventory.CycleSelection(1);
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectHotbar(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectHotbar(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectHotbar(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectHotbar(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SelectHotbar(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SelectHotbar(5);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SelectHotbar(6);
        if (Input.GetKeyDown(KeyCode.Alpha8)) SelectHotbar(7);
    }
    private void SelectHotbar(int index)
    {
        if (index >= 0 && index < inventory.HotbarSlotCount)
            inventory.SetSelectedSlot(PlayerInventory.HotbarIndexToSlotType(index));
    }
    public Vector3 GetDropWorldPositionForUI() => dropPoint != null ? dropPoint.position : transform.position + dropOffset;
}
