using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatVisualMarker : MonoBehaviour
{
    [SerializeField] private BoatVisualCategory category = BoatVisualCategory.Interior;

    public BoatVisualCategory Category => category;

#if UNITY_EDITOR
    public void EditorSetCategory(BoatVisualCategory newCategory)
    {
        category = newCategory;
    }
#endif
}