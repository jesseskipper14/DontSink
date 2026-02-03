using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SortingLayerSetter : MonoBehaviour
{
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    void Start()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.sortingLayerName = sortingLayerName;
        mr.sortingOrder = sortingOrder;
    }
}
