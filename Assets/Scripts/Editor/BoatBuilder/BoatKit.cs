using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-only kit: references to the 5 boat prefabs.
/// Keep this in an Editor folder so it never ships.
/// </summary>
public class BoatKit : ScriptableObject
{
    [Header("Required Prefabs")]
    public GameObject HullSegment;
    public GameObject Wall;
    public GameObject Hatch;
    public GameObject PilotChair;
    public GameObject CompartmentRect;
    public GameObject Deck;

#if UNITY_EDITOR
    [MenuItem("Tools/Boat Builder/Create BoatKit Asset")]
    private static void CreateAsset()
    {
        var asset = CreateInstance<BoatKit>();
        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/BoatKit.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
#endif
}
