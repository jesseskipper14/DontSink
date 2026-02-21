using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatAutoRegister : MonoBehaviour
{
    private Boat _boat;
    private IBoatRegistry _registry;

    private void Awake()
    {
        _boat = GetComponent<Boat>();
        if (_boat == null)
        {
            Debug.LogError($"BoatAutoRegister on '{name}' but no Boat component found.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        _registry = GameState.I != null ? GameState.I.boatRegistry : null;

        if (_registry == null)
        {
            Debug.LogWarning($"BoatAutoRegister: No registry available yet for '{name}'. Will retry in Start().");
            return;
        }

        _registry.Register(_boat);
    }

    private void Start()
    {
        // Retry once after scene initialization in case GameState wasn't ready in OnEnable.
        if (_registry == null)
        {
            _registry = GameState.I != null ? GameState.I.boatRegistry : null;
            if (_registry != null)
                _registry.Register(_boat);
        }
    }

    private void OnDisable()
    {
        if (_registry != null)
            _registry.Unregister(_boat);
    }

    public void RefreshRegistration()
    {
        if (_registry == null)
            _registry = GameState.I != null ? GameState.I.boatRegistry : null;

        if (_registry == null || _boat == null) return;

        _registry.Unregister(_boat);
        _registry.Register(_boat);
    }

}
