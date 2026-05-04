using UnityEngine;

[DisallowMultipleComponent]
public sealed class InstalledModuleAnchor : MonoBehaviour
{
    [Header("Anchor")]
    [SerializeField] private Transform moduleAnchor;

    public Transform ModuleAnchor => moduleAnchor != null ? moduleAnchor : transform;

#if UNITY_EDITOR
    private void Reset()
    {
        Transform found = transform.Find("ModuleAnchor");
        if (found != null)
            moduleAnchor = found;
    }
#endif
}