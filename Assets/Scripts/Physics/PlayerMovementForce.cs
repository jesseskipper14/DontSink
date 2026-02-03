using UnityEngine;

public class PlayerIntentForce : MonoBehaviour, IForceProvider
{
    [Header("Intent")]
    [Tooltip("Maximum force the player can request")]
    public float intentForce = 15f;

    [Header("Settings")]
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 100; // low priority on purpose

    private IForceBody body;

    void Awake()
    {
        body = GetComponent<IForceBody>();
        if (body == null)
        {
            Debug.LogError("PlayerIntentForce requires IForceBody.");
            enabled = false;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        float input = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(input) < 0.01f)
            return;

        // Intent only: world-space for now
        // This WILL be projected onto ground later
        Vector2 intentDirection = Vector2.right * input;

        body.AddForce(intentDirection * intentForce);
    }
}
