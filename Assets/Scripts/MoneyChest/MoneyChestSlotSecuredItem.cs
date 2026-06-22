using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItem))]
public sealed class MoneyChestSlotSecuredItem : MonoBehaviour
{
    [Header("Runtime State")]
    [SerializeField] private bool isSecured;
    [SerializeField] private string slotStableId;
    [SerializeField] private Vector2 securedLocalPosition;
    [SerializeField] private float securedLocalRotationZ;

    [Header("Behavior")]
    [SerializeField] private bool lockPhysics = true;

    [Tooltip("Hook for later if we decide money chests should be affected by impacts.")]
    [SerializeField] private bool immuneToSecuringImpacts = true;

    private Boat _boat;
    private MoneyChestSecureSlot _slot;
    private Rigidbody2D _rb;

    public bool IsSecured => isSecured;
    public string SlotStableId => slotStableId;
    public Vector2 SecuredLocalPosition => securedLocalPosition;
    public float SecuredLocalRotationZ => securedLocalRotationZ;
    public bool ImmuneToSecuringImpacts => immuneToSecuringImpacts;

    private void Awake()
    {
        CacheRefs();
    }

    private void LateUpdate()
    {
        if (!isSecured)
            return;

        ApplySecuredTransform();
    }

    public void SecureToSlot(
        Boat boat,
        MoneyChestSecureSlot slot,
        string stableId,
        Vector2 localPosition,
        float localRotationZ)
    {
        CacheRefs();

        _boat = boat;
        _slot = slot;

        isSecured = true;
        slotStableId = stableId;
        securedLocalPosition = localPosition;
        securedLocalRotationZ = localRotationZ;

        LockPhysics();

        ApplySecuredTransform();
    }

    public void RestoreSecuredState(
        Boat boat,
        MoneyChestSecureSlot slot,
        string stableId,
        Vector2 localPosition,
        float localRotationZ)
    {
        SecureToSlot(
            boat,
            slot,
            stableId,
            localPosition,
            localRotationZ);
    }

    public void ClearSecuredState()
    {
        isSecured = false;
        slotStableId = null;
        securedLocalPosition = Vector2.zero;
        securedLocalRotationZ = 0f;
        _slot = null;

        UnlockPhysics();
    }

    private void ApplySecuredTransform()
    {
        Transform root = null;

        if (_slot != null)
            root = _slot.ChestAnchorOrSelf;

        if (root == null && _boat != null)
        {
            transform.position = _boat.transform.TransformPoint(securedLocalPosition);
            transform.rotation = _boat.transform.rotation * Quaternion.Euler(0f, 0f, securedLocalRotationZ);
            return;
        }

        if (root == null)
            return;

        transform.SetPositionAndRotation(root.position, root.rotation);
    }

    private void CacheRefs()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (_boat == null)
        {
            BoatOwnedItem owned = GetComponent<BoatOwnedItem>();
            if (owned != null && owned.IsOwnedByBoat)
            {
                Boat[] boats = FindObjectsByType<Boat>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int i = 0; i < boats.Length; i++)
                {
                    if (boats[i] != null && boats[i].BoatInstanceId == owned.OwningBoatInstanceId)
                    {
                        _boat = boats[i];
                        break;
                    }
                }
            }
        }

        if (_slot == null && !string.IsNullOrWhiteSpace(slotStableId))
            _slot = MoneyChestSecureSlot.FindByStableId(slotStableId);
    }

    private void LockPhysics()
    {
        if (!lockPhysics)
            return;

        CacheRefs();

        if (_rb == null)
            return;

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.simulated = true;
    }

    private void UnlockPhysics()
    {
        if (!lockPhysics)
            return;

        CacheRefs();

        if (_rb == null)
            return;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.simulated = true;
        _rb.WakeUp();
    }
}