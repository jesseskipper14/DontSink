using UnityEngine;

[DisallowMultipleComponent]
public sealed class CargoItemIdentity : MonoBehaviour
{
    [SerializeField, HideInInspector] private string instanceGuid;
    [SerializeField, HideInInspector] private string typeGuid;

    public string InstanceGuid => instanceGuid;
    public string TypeGuid => typeGuid;

    public void ForceAssign(string instanceGuid, string typeGuid)
    {
        this.instanceGuid = instanceGuid;
        this.typeGuid = typeGuid;
    }

    private void Awake()
    {
        // Runtime safety for spawned items if prefab forgot to validate in editor.
        if (string.IsNullOrEmpty(instanceGuid))
            instanceGuid = System.Guid.NewGuid().ToString("N");

        if (string.IsNullOrEmpty(typeGuid))
        {
            var t = GetComponent<CargoTypeIdentity>();
            if (t != null) typeGuid = t.TypeGuid;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(instanceGuid))
            instanceGuid = System.Guid.NewGuid().ToString("N");

        if (string.IsNullOrEmpty(typeGuid))
        {
            var t = GetComponent<CargoTypeIdentity>();
            if (t != null) typeGuid = t.TypeGuid;
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}