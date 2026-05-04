#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{

    private static void AutoFitBoardedVolume(Transform boatRoot, GameObject placed, float padding, float extraUp, float extraDown)
        {
            if (boatRoot == null || placed == null)
                return;

            var box = placed.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                Debug.LogWarning("[BoatBuilder] BoardedVolume placed, but no BoxCollider2D found to auto-fit.", placed);
                return;
            }

            var renderers = boatRoot.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && r.gameObject != placed)
                .ToArray();

            if (renderers.Length == 0)
            {
                Debug.LogWarning("[BoatBuilder] Could not auto-fit BoardedVolume: no renderers found under boat root.", boatRoot);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            bounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
            bounds.min += new Vector3(0f, -extraDown, 0f);
            bounds.max += new Vector3(0f, extraUp, 0f);

            Undo.RecordObject(placed.transform, "Auto-fit BoardedVolume");
            Undo.RecordObject(box, "Resize BoardedVolume");

            placed.transform.position = new Vector3(bounds.center.x, bounds.center.y, placed.transform.position.z);

            Vector2 localSize = boatRoot.InverseTransformVector(bounds.size);
            box.offset = Vector2.zero;
            box.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));

            EditorUtility.SetDirty(placed.transform);
            EditorUtility.SetDirty(box);
        }

    private static Transform ResolvePlacementParentForTool(
        BoatBuilderWindow.Tool tool,
        Transform boatRoot)
        {
            if (boatRoot == null)
                return null;

            return tool switch
            {
                // Exterior shell / visual occluder
                BoatBuilderWindow.Tool.ExteriorShell =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorShell),

                // Hull/body structural visuals
                BoatBuilderWindow.Tool.HullSegment =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Hull),

                BoatBuilderWindow.Tool.Wall =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Hull),

                // Interior simulation/visuals
                BoatBuilderWindow.Tool.CompartmentRect =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Interior),

                BoatBuilderWindow.Tool.Ladder =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Interior),

                BoatBuilderWindow.Tool.Stairs =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Interior),

                BoatBuilderWindow.Tool.Ledge =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Interior),

                // Exterior deck objects
                BoatBuilderWindow.Tool.Deck =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorDeck),

                BoatBuilderWindow.Tool.Hatch =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorDeck),

                BoatBuilderWindow.Tool.PilotChair =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorDeck),

                // Volumes/triggers
                BoatBuilderWindow.Tool.BoardedVolume =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Volume),

                // Gameplay helpers/interactables
                BoatBuilderWindow.Tool.PlayerSpawnPoint =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

                BoatBuilderWindow.Tool.BoatBoardObject =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

                BoatBuilderWindow.Tool.MapTable =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

                BoatBuilderWindow.Tool.Hardpoint =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

                BoatBuilderWindow.Tool.TurretControllerChair =>
                    GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

                _ => boatRoot
            };
        }

    private static Transform GetOrCreateBoatCategoryRoot(Transform boatRoot, BoatVisualCategory category)
        {
            if (boatRoot == null)
                return null;

            string childName = category switch
            {
                BoatVisualCategory.ExteriorShell => "_Exterior",
                BoatVisualCategory.Interior => "_Interior",
                BoatVisualCategory.ExteriorDeck => "_Deck",
                BoatVisualCategory.Gameplay => "_Gameplay",
                BoatVisualCategory.Volume => "_Volumes",
                BoatVisualCategory.AlwaysVisible => "_AlwaysVisible",
                BoatVisualCategory.Hull => "_Hull",
                _ => "_Misc"
            };

            Transform existing = boatRoot.Find(childName);
            if (existing != null)
                return existing;

            GameObject go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, $"Create boat category root {childName}");

            Transform t = go.transform;
            Undo.SetTransformParent(t, boatRoot, $"Parent {childName} to BoatRoot");

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(boatRoot.gameObject.scene);

            return t;
        }

    private static void InitializePlacedVisualMarker(GameObject placed, BoatVisualCategory category)
        {
            if (placed == null)
                return;

            BoatVisualMarker marker = placed.GetComponent<BoatVisualMarker>();
            if (marker == null)
            {
                marker = placed.AddComponent<BoatVisualMarker>();
                Undo.RegisterCreatedObjectUndo(marker, "Add Boat Visual Marker");
            }

            Undo.RecordObject(marker, "Configure Boat Visual Marker");
            marker.EditorSetCategory(category);

            EditorUtility.SetDirty(marker);
        }

    private static void InitializePlacedStair(GameObject placed, bool ascendRight)
        {
            if (placed == null)
                return;

            StairSlopeAuthoring stair = placed.GetComponent<StairSlopeAuthoring>();
            if (stair == null)
            {
                Debug.LogWarning("[BoatBuilder] Placed stair prefab has no StairSlopeAuthoring.", placed);
                return;
            }

            Undo.RecordObject(stair, "Configure Stair Orientation");

            SerializedObject stairSO = new SerializedObject(stair);
            SerializedProperty ascendProp = stairSO.FindProperty("ascendRight");

            if (ascendProp != null)
            {
                ascendProp.boolValue = ascendRight;
                stairSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(stair);
            }

            // Force immediate re-apply in editor so collider/mesh/trigger update now.
            stair.Apply();

            Selection.activeGameObject = placed;
        }

    public static void AutoFitBoatGeometryFromVisualRenderers(Transform boatRoot)
        {
            if (boatRoot == null)
            {
                Debug.LogWarning("[BoatBuilder] Cannot auto-fit boat geometry: BoatRoot is null.");
                return;
            }

            Boat boat = boatRoot.GetComponent<Boat>();
            if (boat == null)
                boat = boatRoot.GetComponentInParent<Boat>();

            if (boat == null)
            {
                Debug.LogWarning($"[BoatBuilder] Cannot auto-fit boat geometry: no Boat component found for '{boatRoot.name}'.", boatRoot);
                return;
            }

            boat.EditorAutoFitGeometryFromVisualRenderers();

            EditorUtility.SetDirty(boat);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
}
#endif
