using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class GameplayAuthorityDebugSetter : MonoBehaviour
{
    [SerializeField] private bool authoritative = true;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool logEvenIfUnchanged = false;

    private void Awake()
    {
        if (applyOnAwake)
            GameplayAuthority.SetAuthoritativeForDebug(authoritative, logEvenIfUnchanged);
    }

    [ContextMenu("Apply Authority Setting")]
    public void Apply()
    {
        GameplayAuthority.SetAuthoritativeForDebug(authoritative, logEvenIfUnchanged: true);
    }
}