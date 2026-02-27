using UnityEngine;

[DisallowMultipleComponent]
public sealed class CargoItemIdentity : MonoBehaviour
{
    private string _instanceGuid;
    public string InstanceGuid => _instanceGuid;

    public string TypeGuid
    {
        get
        {
            var t = GetComponent<CargoTypeIdentity>()
                 ?? GetComponentInParent<CargoTypeIdentity>()
                 ?? GetComponentInChildren<CargoTypeIdentity>(true);
            return t != null ? t.TypeGuid : null;
        }
    }

    public void ForceAssign(string instanceGuid, string typeGuidIgnored)
    {
        _instanceGuid = instanceGuid;
        // ignore typeGuid, because TypeGuid is derived from CargoTypeIdentity
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(_instanceGuid))
            _instanceGuid = System.Guid.NewGuid().ToString("N");
    }
}