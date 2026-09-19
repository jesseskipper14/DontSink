using UnityEngine;

/// <summary>
/// Local runtime presentation for HardpointSupportFootprint.
///
/// While the mouse is over an EMPTY hardpoint and the player has a compatible
/// module available, this draws that module's required support span:
///
///     GREEN = every support sample passes
///     RED   = at least one sample is unsupported
///
/// This component is presentation only. Hardpoint.TryInstall remains the
/// authoritative validator.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HardpointSupportFootprint))]
public sealed class HardpointSupportHoverPreview :
    MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Hardpoint hardpoint;
    [SerializeField] private HardpointSupportFootprint support;
    [SerializeField] private Camera worldCamera;

    [Header("Hover")]
    [Tooltip(
        "Colliders that count as hovering this hardpoint. If empty, non-null enabled " +
        "colliders under the Hardpoint are discovered automatically.")]
    [SerializeField] private Collider2D[] hoverColliders;

    [Header("Visual")]
    [SerializeField, Min(0.005f)]
    private float lineWidth =
        0.045f;

    [SerializeField]
    private float zOffset =
        -0.05f;

    [SerializeField]
    private Color validColor =
        new Color(
            0.15f,
            1f,
            0.25f,
            0.95f);

    [SerializeField]
    private Color invalidColor =
        new Color(
            1f,
            0.15f,
            0.10f,
            0.95f);

    [Header("Debug")]
    [SerializeField]
    private bool verboseLogging =
        false;

    private LineRenderer _line;
    private Material _runtimeMaterial;
    private PlayerInventory _inventory;

    private void Reset()
    {
        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
        EnsureLine();
        SetVisible(false);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
        {
            Destroy(
                _runtimeMaterial);
        }
    }

    private void Update()
    {
        ResolveRefs();

        if (hardpoint == null ||
            support == null ||
            hardpoint.HasInstalledModule)
        {
            SetVisible(false);
            return;
        }

        if (!IsMouseHoveringHardpoint())
        {
            SetVisible(false);
            return;
        }

        if (!TryResolveCandidateModule(
                out ModuleDefinition module) ||
            module == null ||
            !module.RequiresBoatSupport)
        {
            SetVisible(false);
            return;
        }

        bool valid =
            support.TryValidateModuleSupport(
                module,
                out string reason);

        RenderPreview(
            module.RequiredBoatSupportWidth,
            valid);

        if (verboseLogging &&
            !valid)
        {
            Debug.Log(
                $"[HardpointSupportHoverPreview:{name}] {reason}",
                this);
        }
    }

    private void RenderPreview(
        float requiredWidth,
        bool valid)
    {
        EnsureLine();

        if (_line == null)
            return;

        float width =
            Mathf.Max(
                0.001f,
                requiredWidth);

        Vector3 center =
            support.transform.TransformPoint(
                support.LocalProbeOffset);

        Vector3 halfAxis =
            support.transform.right *
            (width * 0.5f);

        Vector3 down =
            -support.transform.up *
            support.MaximumSupportDrop;

        Vector3 a =
            center -
            halfAxis;

        Vector3 b =
            center +
            halfAxis;

        Vector3 depthOffset =
            Vector3.forward *
            zOffset;

        // Closed rectangle:
        // top-left -> top-right -> bottom-right -> bottom-left -> top-left
        _line.positionCount =
            5;

        _line.SetPosition(
            0,
            a + depthOffset);

        _line.SetPosition(
            1,
            b + depthOffset);

        _line.SetPosition(
            2,
            b + down + depthOffset);

        _line.SetPosition(
            3,
            a + down + depthOffset);

        _line.SetPosition(
            4,
            a + depthOffset);

        Color color =
            valid
                ? validColor
                : invalidColor;

        _line.startColor =
            color;

        _line.endColor =
            color;

        _line.enabled =
            true;
    }

    private bool IsMouseHoveringHardpoint()
    {
        Camera cam =
            worldCamera != null
                ? worldCamera
                : Camera.main;

        if (cam == null)
            return false;

        Vector3 mouseScreen =
            Input.mousePosition;

        Vector3 mouseWorld3 =
            cam.ScreenToWorldPoint(
                mouseScreen);

        Vector2 mouseWorld =
            new Vector2(
                mouseWorld3.x,
                mouseWorld3.y);

        ResolveHoverColliders();

        if (hoverColliders == null)
            return false;

        for (int i = 0;
             i < hoverColliders.Length;
             i++)
        {
            Collider2D collider =
                hoverColliders[i];

            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (collider.OverlapPoint(
                    mouseWorld))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveCandidateModule(
        out ModuleDefinition module)
    {
        module =
            null;

        PlayerInventory inventory =
            ResolveInventory();

        if (inventory == null)
            return false;

        PlayerEquipment equipment =
            inventory.Equipment;

        BottomBarSlotType selected =
            inventory.SelectedSlot;

        if (selected >=
                BottomBarSlotType.Hotbar0 &&
            selected <=
                BottomBarSlotType.Hotbar7)
        {
            int index =
                PlayerInventory.SlotTypeToHotbarIndex(
                    selected);

            InventorySlot slot =
                inventory.GetSlot(
                    index);

            if (TryResolveCompatibleModule(
                    slot != null
                        ? slot.Instance
                        : null,
                    out module))
            {
                return true;
            }
        }
        else if (equipment != null)
        {
            if (TryResolveCompatibleModule(
                    equipment.Get(
                        selected),
                    out module))
            {
                return true;
            }
        }

        // Match HardpointInteractable's useful fallback behavior: if the
        // selected slot is not a module, look for a compatible hotbar module.
        for (int i = 0;
             i < inventory.HotbarSlotCount;
             i++)
        {
            InventorySlot slot =
                inventory.GetSlot(
                    i);

            if (TryResolveCompatibleModule(
                    slot != null
                        ? slot.Instance
                        : null,
                    out module))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveCompatibleModule(
        ItemInstance item,
        out ModuleDefinition module)
    {
        module =
            null;

        if (item == null ||
            item.Definition == null ||
            !item.Definition.IsModule)
        {
            return false;
        }

        module =
            item.Definition.ModuleDefinition;

        if (module == null ||
            hardpoint == null)
        {
            module =
                null;

            return false;
        }

        // Intentionally use lightweight compatibility. Support may be invalid;
        // that invalid state is precisely what this preview is meant to show.
        if (!hardpoint.CanInstall(
                module))
        {
            module =
                null;

            return false;
        }

        return true;
    }

    private PlayerInventory ResolveInventory()
    {
        if (_inventory != null)
            return _inventory;

        _inventory =
            FindFirstObjectByType<PlayerInventory>();

        return _inventory;
    }

    private void ResolveRefs()
    {
        if (hardpoint == null)
        {
            hardpoint =
                GetComponentInParent<Hardpoint>();

            if (hardpoint == null)
            {
                hardpoint =
                    GetComponentInChildren<Hardpoint>(
                        true);
            }
        }

        if (support == null)
        {
            support =
                GetComponent<HardpointSupportFootprint>();

            if (support == null &&
                hardpoint != null)
            {
                support =
                    hardpoint.GetComponentInChildren<HardpointSupportFootprint>(
                        true);
            }
        }

        if (worldCamera == null)
            worldCamera = Camera.main;

        ResolveHoverColliders();
    }

    private void ResolveHoverColliders()
    {
        bool hasAny =
            false;

        if (hoverColliders != null)
        {
            for (int i = 0;
                 i < hoverColliders.Length;
                 i++)
            {
                if (hoverColliders[i] != null)
                {
                    hasAny =
                        true;

                    break;
                }
            }
        }

        if (hasAny)
            return;

        Transform root =
            hardpoint != null
                ? hardpoint.transform
                : transform;

        hoverColliders =
            root.GetComponentsInChildren<Collider2D>(
                true);
    }

    private void EnsureLine()
    {
        if (_line != null)
            return;

        GameObject go =
            new GameObject(
                "RuntimeSupportFootprintPreview");

        go.transform.SetParent(
            transform,
            false);

        _line =
            go.AddComponent<LineRenderer>();

        _line.useWorldSpace =
            true;

        _line.loop =
            false;

        _line.startWidth =
            lineWidth;

        _line.endWidth =
            lineWidth;

        _line.numCapVertices =
            2;

        _line.numCornerVertices =
            2;

        _line.sortingOrder =
            10000;

        Shader shader =
            Shader.Find(
                "Sprites/Default");

        if (shader != null)
        {
            _runtimeMaterial =
                new Material(
                    shader);

            _line.sharedMaterial =
                _runtimeMaterial;
        }

        _line.enabled =
            false;
    }

    private void SetVisible(
        bool visible)
    {
        if (_line != null)
            _line.enabled = visible;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lineWidth =
            Mathf.Max(
                0.005f,
                lineWidth);
    }
#endif
}
