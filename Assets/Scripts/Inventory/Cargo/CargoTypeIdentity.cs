using UnityEngine;

[DisallowMultipleComponent]
public sealed class CargoTypeIdentity : MonoBehaviour
{
    [SerializeField] private string prefabTypeGuid;
    public string TypeGuid => prefabTypeGuid;

#if UNITY_EDITOR
    [ContextMenu("Regenerate Type Guid")]
    private void RegenerateTypeGuid()
    {
        prefabTypeGuid = System.Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(prefabTypeGuid))
        {
            prefabTypeGuid = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}