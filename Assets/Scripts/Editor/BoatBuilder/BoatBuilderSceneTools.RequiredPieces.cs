#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{

    public static void GetRequiredPiecesStatus(
            Transform boatRoot,
            out bool hasBoatBoardObject,
            out bool hasMapTable,
            out int spawnPointCount,
            out bool hasBoardedVolume)
        {
            hasBoatBoardObject = false;
            hasMapTable = false;
            hasBoardedVolume = false;
            spawnPointCount = 0;

            if (boatRoot == null) return;

            hasBoatBoardObject = boatRoot.GetComponentsInChildren<BoatBoardingInteractable>(true).Any();
            hasMapTable = boatRoot.GetComponentsInChildren<MapTableInteractable>(true).Any();
            hasBoardedVolume = boatRoot.GetComponentsInChildren<BoatBoardedVolume>(true).Any();

            var trs = boatRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var n = trs[i].name;
                if (string.Equals(n, "PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase))
                {
                    spawnPointCount++;
                }
            }
        }

    public static bool TryGetFirstMissingRequiredTool(Transform boatRoot, out BoatBuilderWindow.Tool tool)
        {
            tool = default;
            if (boatRoot == null) return false;

            GetRequiredPiecesStatus(boatRoot, out var hasBoard, out var hasMap, out var spawnCount, out var hasVol);

            if (!hasVol) { tool = BoatBuilderWindow.Tool.BoardedVolume; return true; }
            if (spawnCount < Mathf.Max(1, _ctx.RequiredPlayerSpawnPoints)) { tool = BoatBuilderWindow.Tool.PlayerSpawnPoint; return true; }
            if (!hasBoard) { tool = BoatBuilderWindow.Tool.BoatBoardObject; return true; }
            if (!hasMap) { tool = BoatBuilderWindow.Tool.MapTable; return true; }

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
