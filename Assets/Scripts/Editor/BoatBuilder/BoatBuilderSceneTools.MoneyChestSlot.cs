#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{
    private const string MoneyChestSlotStableId = "money_chest_slot_01";

    private static void InitializePlacedMoneyChestSlot(
        GameObject placed,
        Transform boatRoot)
    {
        if (placed == null)
            return;

        MoneyChestSecureSlot slot =
            placed.GetComponent<MoneyChestSecureSlot>() ??
            placed.GetComponentInChildren<MoneyChestSecureSlot>(true);

        if (slot == null)
        {
            Debug.LogWarning(
                "[BoatBuilder] Placed MoneyChestSlot prefab has no MoneyChestSecureSlot component.",
                placed);
            return;
        }

        Undo.RecordObject(slot, "Initialize Money Chest Slot");

        SerializedObject so = new SerializedObject(slot);

        SerializedProperty stableIdProp = so.FindProperty("stableId");
        if (stableIdProp != null)
            stableIdProp.stringValue = MoneyChestSlotStableId;

        so.ApplyModifiedPropertiesWithoutUndo();

        Collider2D col =
            slot.GetComponent<Collider2D>() ??
            slot.GetComponentInChildren<Collider2D>(true);

        if (col != null)
        {
            Undo.RecordObject(col, "Configure Money Chest Slot Collider");
            col.isTrigger = true;
            EditorUtility.SetDirty(col);
        }
        else
        {
            Debug.LogWarning(
                "[BoatBuilder] MoneyChestSlot has no Collider2D. Interaction prompts will not work until one is added.",
                placed);
        }

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            SetLayerRecursive(placed, interactableLayer);
        else
        {
            Debug.LogWarning(
                "[BoatBuilder] Could not find layer 'Interactable'. MoneyChestSlot layer was not changed.",
                placed);
        }

        Undo.RecordObject(placed, "Rename Money Chest Slot");
        placed.name = MoneyChestSlotStableId;

        EditorUtility.SetDirty(slot);
        EditorUtility.SetDirty(placed);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (boatRoot != null)
            ValidateMoneyChestSlotCount(boatRoot);
    }

    public static int CountMoneyChestSlots(Transform boatRoot)
    {
        if (boatRoot == null)
            return 0;

        MoneyChestSecureSlot[] slots =
            boatRoot.GetComponentsInChildren<MoneyChestSecureSlot>(true);

        int count = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                count++;
        }

        return count;
    }

    public static void ValidateMoneyChestSlotCount(Transform boatRoot)
    {
        if (boatRoot == null)
            return;

        MoneyChestSecureSlot[] slots =
            boatRoot.GetComponentsInChildren<MoneyChestSecureSlot>(true);

        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning(
                "[BoatBuilder] Boat has no MoneyChestSecureSlot. Replacement money chest secure-slot spawn will fall back to dock/default spawn.",
                boatRoot);
            return;
        }

        if (slots.Length > 1)
        {
            Debug.LogWarning(
                $"[BoatBuilder] Boat has {slots.Length} MoneyChestSecureSlots. Current design expects exactly one. " +
                "Selecting the first and letting future-you glare at the rest.",
                slots[0]);
        }

        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < slots.Length; i++)
        {
            MoneyChestSecureSlot slot = slots[i];
            if (slot == null)
                continue;

            if (string.IsNullOrWhiteSpace(slot.StableId))
            {
                Debug.LogWarning(
                    $"[BoatBuilder] MoneyChestSecureSlot '{slot.name}' has an empty StableId.",
                    slot);
                continue;
            }

            if (!seenIds.Add(slot.StableId))
            {
                Debug.LogWarning(
                    $"[BoatBuilder] Duplicate MoneyChestSecureSlot StableId '{slot.StableId}' found on '{slot.name}'.",
                    slot);
            }
        }
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        Transform[] trs = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < trs.Length; i++)
        {
            Transform t = trs[i];
            if (t == null)
                continue;

            Undo.RecordObject(t.gameObject, "Set Money Chest Slot Layer");
            t.gameObject.layer = layer;
            EditorUtility.SetDirty(t.gameObject);
        }
    }
}
#endif