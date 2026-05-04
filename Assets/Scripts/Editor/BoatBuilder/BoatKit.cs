using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-only kit: references to boat piece prefabs used by the Boat Builder tooling.
/// Keep this in an Editor folder so it never ships.
/// </summary>
public class BoatKit : ScriptableObject
{
    [Header("Root")]
    public GameObject BoatRootPrefab;

    [Header("Core Pieces")]
    public GameObject HullSegment;
    public GameObject Wall;
    public GameObject Hatch;
    public GameObject PilotChair;
    public GameObject CompartmentRect;
    public GameObject Deck;
    public GameObject ExteriorShell;

    [Header("Gameplay Pieces (Builder Supported)")]
    public GameObject BoatBoardObject;
    public GameObject MapTable;
    public GameObject PlayerSpawnPoint;
    public GameObject BoardedVolume;
    public GameObject Ladder;
    public GameObject Stairs;
    public GameObject Ledge;
    public GameObject TurretControllerChair;

    [Header("Hardpoints")]
    public GameObject HardpointEngine;
    public GameObject HardpointPump;
    public GameObject HardpointUtility;
    public GameObject HardpointWeapon;
    public GameObject HardpointElectronics;
    public GameObject HardpointHelm;

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