#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{

    // Backward-compatible overload for any older editor call sites.
    public static void GetRequiredPiecesStatus(
        Transform boatRoot,
        out bool hasBoatBoardObject,
        out bool hasMapTable,
        out bool hasMoneyChestSlot,
        out int spawnPointCount,
        out bool hasBoardedVolume)
    {
        GetRequiredPiecesStatus(
            boatRoot,
            out hasBoatBoardObject,
            out hasMapTable,
            out hasMoneyChestSlot,
            out spawnPointCount,
            out hasBoardedVolume,
            out _);
    }

    public static void GetRequiredPiecesStatus(
        Transform boatRoot,
        out bool hasBoatBoardObject,
        out bool hasMapTable,
        out bool hasMoneyChestSlot,
        out int spawnPointCount,
        out bool hasBoardedVolume,
        out bool hasItemContainmentZone)
    {
        hasBoatBoardObject = false;
        hasMapTable = false;
        hasMoneyChestSlot = false;
        hasBoardedVolume = false;
        hasItemContainmentZone = false;
        spawnPointCount = 0;

        if (boatRoot == null)
            return;

        hasBoatBoardObject =
            boatRoot.GetComponentsInChildren<BoatBoardingInteractable>(true).Any();

        hasMapTable =
            boatRoot.GetComponentsInChildren<MapTableInteractable>(true).Any();

        hasMoneyChestSlot =
            boatRoot.GetComponentsInChildren<MoneyChestSecureSlot>(true).Any();

        hasBoardedVolume =
            boatRoot.GetComponentsInChildren<BoatBoardedVolume>(true).Any();

        hasItemContainmentZone =
            boatRoot.GetComponentsInChildren<BoatItemContainmentZone>(true).Any();

        Transform[] trs = boatRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            string n = trs[i].name;
            if (string.Equals(n, "PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase))
            {
                spawnPointCount++;
            }
        }
    }

    public static void GetRequiredBoatSystemsStatus(
        Transform boatRoot,
        out bool hasBoat,
        out bool hasRootRigidbody,
        out bool hasBuoyancy,
        out bool hasGhostCollisionProxy,
        out bool hasBoatItemRegistry,
        out bool hasVisualStateController)
    {
        hasBoat = false;
        hasRootRigidbody = false;
        hasBuoyancy = false;
        hasGhostCollisionProxy = false;
        hasBoatItemRegistry = false;
        hasVisualStateController = false;

        if (boatRoot == null)
            return;

        // These are root/runtime infrastructure rather than authored "pieces".
        // Keep them visible in Builder validation without trying to auto-add
        // potentially configuration-heavy systems.
        hasBoat =
            boatRoot.GetComponent<Boat>() != null;

        hasRootRigidbody =
            boatRoot.GetComponent<Rigidbody2D>() != null;

        hasBuoyancy =
            boatRoot.GetComponent<BuoyancyPolygonForce>() != null;

        hasGhostCollisionProxy =
            boatRoot.GetComponent<GhostCollisionProxy>() != null;

        hasBoatItemRegistry =
            boatRoot.GetComponent<BoatItemRegistry>() != null;

        hasVisualStateController =
            boatRoot.GetComponent<BoatVisualStateController>() != null ||
            boatRoot.GetComponentInChildren<BoatVisualStateController>(true) != null;
    }

    public static bool TryGetFirstMissingRequiredTool(
        Transform boatRoot,
        out BoatBuilderWindow.Tool tool)
    {
        tool = default;

        if (boatRoot == null)
            return false;

        GetRequiredPiecesStatus(
            boatRoot,
            out bool hasBoard,
            out bool hasMap,
            out bool hasMoneyChestSlot,
            out int spawnCount,
            out bool hasVol,
            out bool hasItemContainmentZone);

        if (!hasVol)
        {
            tool = BoatBuilderWindow.Tool.BoardedVolume;
            return true;
        }

        if (!hasItemContainmentZone)
        {
            tool = BoatBuilderWindow.Tool.ItemContainmentZone;
            return true;
        }

        if (spawnCount < Mathf.Max(1, _ctx.RequiredPlayerSpawnPoints))
        {
            tool = BoatBuilderWindow.Tool.PlayerSpawnPoint;
            return true;
        }

        if (!hasBoard)
        {
            tool = BoatBuilderWindow.Tool.BoatBoardObject;
            return true;
        }

        if (!hasMap)
        {
            tool = BoatBuilderWindow.Tool.MapTable;
            return true;
        }

        if (!hasMoneyChestSlot)
        {
            tool = BoatBuilderWindow.Tool.MoneyChestSlot;
            return true;
        }

        return false;
    }

    private static bool HandleRequiredDuplicateBlock(Transform boatRoot, BoatBuilderWindow.Tool tool)
    {
        switch (tool)
        {
            case BoatBuilderWindow.Tool.BoardedVolume:
                {
                    var existing = boatRoot.GetComponentInChildren<BoatBoardedVolume>(true);
                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning("[BoatBuilder] BoardedVolume already exists under boat root. Selecting existing instead.");
                        return true;
                    }
                    break;
                }
            case BoatBuilderWindow.Tool.ItemContainmentZone:
                {
                    var existing =
                        boatRoot.GetComponentInChildren<BoatItemContainmentZone>(true);

                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning(
                            "[BoatBuilder] BoatItemContainmentZone already exists under boat root. Selecting existing instead.");
                        return true;
                    }

                    break;
                }

            case BoatBuilderWindow.Tool.BoatBoardObject:
                {
                    var existing = boatRoot.GetComponentInChildren<BoatBoardingInteractable>(true);
                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning("[BoatBuilder] BoatBoardObject already exists under boat root. Selecting existing instead.");
                        return true;
                    }
                    break;
                }
            case BoatBuilderWindow.Tool.MapTable:
                {
                    var existing = boatRoot.GetComponentInChildren<MapTableInteractable>(true);
                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning("[BoatBuilder] MapTable already exists under boat root. Selecting existing instead.");
                        return true;
                    }
                    break;
                }
            case BoatBuilderWindow.Tool.PlayerSpawnPoint:
                {
                    int count = CountSpawnPoints(boatRoot);
                    int req = Mathf.Max(1, _ctx.RequiredPlayerSpawnPoints);
                    if (count >= req)
                    {
                        Debug.LogWarning($"[BoatBuilder] PlayerSpawnPoint count is already {count} (required {req}). Placing more is allowed but usually unnecessary.");
                    }
                    break;
                }
            case BoatBuilderWindow.Tool.MoneyChestSlot:
                {
                    MoneyChestSecureSlot existing =
                        boatRoot.GetComponentInChildren<MoneyChestSecureSlot>(true);

                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning("[BoatBuilder] MoneyChestSlot already exists under boat root. Selecting existing instead.");
                        return true;
                    }

                    break;
                }
        }

        return false;
    }

    private static int CountSpawnPoints(Transform boatRoot)
    {
        if (boatRoot == null) return 0;
        var trs = boatRoot.GetComponentsInChildren<Transform>(true);
        int c = 0;
        for (int i = 0; i < trs.Length; i++)
        {
            var n = trs[i].name;
            if (string.Equals(n, "PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase))
            {
                c++;
            }
        }
        return c;
    }
}
#endif
