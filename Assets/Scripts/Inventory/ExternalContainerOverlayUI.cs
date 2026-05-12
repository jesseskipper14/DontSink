using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public sealed class ExternalContainerOverlayUI : MonoBehaviour, IEscapeClosable
{
    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private Transform slotRoot;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private PlayerInventoryUI owner;
    [SerializeField] private InventoryDragController dragController;

    [Header("Close")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private bool closeOnKey = true;
    [SerializeField] private bool closeWhenSourceDestroyed = true;

    [Header("Escape Routing")]
    [SerializeField] private bool closeViaGlobalEscapeRouter = true;
    [SerializeField] private int escapePriority = 800;

    [System.NonSerialized] private ItemInstance _containerItem;
    [System.NonSerialized] private ItemContainerState _state;
    private Transform _sourceTransform;
    [SerializeField] private float _autoCloseDistance = -1f;
    private bool _isOpen;

    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Vector2 screenOffset = new Vector2(0, 150f);

    private Camera _camera;
    private Canvas _canvas;

    public int EscapePriority => escapePriority;
    public bool IsEscapeOpen => _isOpen;
    public bool IsOpen => _isOpen;
    public ItemInstance CurrentContainer => _containerItem;

    private void Awake()
    {
        _camera = Camera.main;
        _canvas = GetComponentInParent<Canvas>();

        if (root == null)
        {
            Debug.LogWarning("[ExternalContainerOverlayUI] Root is not assigned. Assign ExternalInventoryPanel in the inspector.", this);
        }


        if (dragController == null)
            dragController = GetComponentInParent<InventoryDragController>(true);

         if (owner == null)
            owner = GetComponentInParent<PlayerInventoryUI>(true);

        if (panelRect == null && root != null)
            panelRect = root.GetComponent<RectTransform>();

        if (playerTransform == null)
        {
            PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInventory != null)
                playerTransform = playerInventory.transform;
        }

        SetVisible(false);
    }

    private void OnDisable()
    {
        UnbindState();

        if (EscapeCloseRegistry.I != null)
            EscapeCloseRegistry.I.Unregister(this);
    }

    private void Update()
    {
        if (!_isOpen)
            return;

        if (!closeViaGlobalEscapeRouter && closeOnKey && Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        if (closeWhenSourceDestroyed && _sourceTransform == null)
        {
            Close();
            return;
        }

        if (_sourceTransform != null && playerTransform != null && _autoCloseDistance > 0f)
        {
            float dist = Vector2.Distance(playerTransform.position, _sourceTransform.position);
            if (dist > _autoCloseDistance)
            {
                Close();
                return;
            }
        }

        UpdatePosition();
    }

    public void Open(
        string title,
        ItemInstance containerItem,
        Transform sourceTransform = null,
        float autoCloseDistance = -1f)
    {
        if (containerItem == null || !containerItem.IsContainer)
        {
            Debug.LogWarning("[ExternalContainerOverlayUI] Open called with invalid container item.");
            return;
        }

        ItemContainerState state = containerItem.ContainerState;
        if (state == null)
        {
            Debug.LogWarning("[ExternalContainerOverlayUI] Container has no state.");
            return;
        }

        if (_containerItem != containerItem)
        {
            UnbindState();

            _containerItem = containerItem;
            _state = state;

            BindState();
        }

        _sourceTransform = sourceTransform;
        _autoCloseDistance = autoCloseDistance;

        Rebuild();

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(title) ? "Container" : title;

        SetVisible(true);
        _isOpen = true;

        if (closeViaGlobalEscapeRouter)
        {
            EscapeCloseRegistry registry = EscapeCloseRegistry.GetOrFind();
            if (registry != null)
                registry.Register(this);
        }
    }

    public void Close()
    {
        if (!_isOpen)
            return;

        Clear();
        UnbindState();

        _state = null;
        _containerItem = null;
        _sourceTransform = null;
        _autoCloseDistance = -1f;
        _isOpen = false;

        if (EscapeCloseRegistry.I != null)
            EscapeCloseRegistry.I.Unregister(this);

        SetVisible(false);
    }

    private void BindState()
    {
        if (_state != null)
            _state.Changed += HandleStateChanged;
    }

    private void UnbindState()
    {
        if (_state != null)
            _state.Changed -= HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        if (!_isOpen)
            return;

        RefreshSlots();
    }

    private void Rebuild()
    {
        Clear();

        if (_state == null || slotRoot == null || slotPrefab == null)
            return;

        for (int i = 0; i < _state.SlotCount; i++)
        {
            InventorySlotUI slotUI = Instantiate(slotPrefab, slotRoot);

            if (owner != null)
                slotUI.SetOwner(owner);

            if (dragController != null)
                slotUI.SetDragController(dragController);

            ExternalInventorySlotBinding binding = new ExternalInventorySlotBinding(_containerItem, i);
            slotUI.Bind(binding);
        }
    }

    private void Clear()
    {
        if (slotRoot == null)
            return;

        for (int i = slotRoot.childCount - 1; i >= 0; i--)
            Destroy(slotRoot.GetChild(i).gameObject);
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    private void UpdatePosition()
    {
        if (!_isOpen || _sourceTransform == null || panelRect == null || _camera == null || _canvas == null)
            return;

        Vector3 screenPos = _camera.WorldToScreenPoint(_sourceTransform.position);

        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _camera,
            out localPos
        );

        panelRect.anchoredPosition = localPos + screenOffset;
    }

    private void RefreshSlots()
    {
        if (slotRoot == null)
            return;

        for (int i = 0; i < slotRoot.childCount; i++)
        {
            InventorySlotUI slotUI = slotRoot.GetChild(i).GetComponent<InventorySlotUI>();
            if (slotUI != null)
                slotUI.Refresh();
        }
    }

    public bool CloseFromEscape()
    {
        if (!_isOpen)
            return false;

        Close();
        return true;
    }
}