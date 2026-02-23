using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatIdentity : MonoBehaviour
{
    [SerializeField, HideInInspector] private string boatGuid;

    public string BoatGuid => boatGuid;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(boatGuid))
        {
            boatGuid = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}