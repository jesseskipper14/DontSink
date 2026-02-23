using UnityEngine;

[DisallowMultipleComponent]
public sealed class CargoTypeIdentity : MonoBehaviour
{
    [SerializeField, HideInInspector] private string typeGuid;
    public string TypeGuid => typeGuid;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(typeGuid))
        {
            typeGuid = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}