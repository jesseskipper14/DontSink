using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public sealed class BoatIdentity : MonoBehaviour
{
    [SerializeField, HideInInspector] private string boatGuid;

    public string BoatGuid => boatGuid;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(boatGuid))
            GenerateNewGuid(recordUndo: false, log: false);
    }

    [ContextMenu("Boat Identity/Regenerate Boat Guid")]
    private void EditorRegenerateBoatGuid()
    {
        GenerateNewGuid(recordUndo: true, log: true);
    }

    [ContextMenu("Boat Identity/Copy Boat Guid To Clipboard")]
    private void EditorCopyBoatGuid()
    {
        GUIUtility.systemCopyBuffer = boatGuid ?? "";

        Debug.Log(
            $"[BoatIdentity:{name}] Copied BoatGuid='{boatGuid}' to clipboard.",
            this);
    }

    [ContextMenu("Boat Identity/Print Boat Guid")]
    private void EditorPrintBoatGuid()
    {
        Debug.Log(
            $"[BoatIdentity:{name}] BoatGuid='{boatGuid}'",
            this);
    }

    private void GenerateNewGuid(bool recordUndo, bool log)
    {
        if (recordUndo)
            Undo.RecordObject(this, "Regenerate Boat Guid");

        string oldGuid = boatGuid;
        boatGuid = System.Guid.NewGuid().ToString("N");

        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);

        if (gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(gameObject.scene);

        if (log)
        {
            Debug.Log(
                $"[BoatIdentity:{name}] Regenerated BoatGuid old='{oldGuid}' new='{boatGuid}'",
                this);
        }
    }
#endif
}