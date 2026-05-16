#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{

    private static void InitializePlacedHardpoint(
            GameObject placed,
            Transform boatRoot,
            HardpointType selectedType,
            string idPrefix,
            bool autoCreateMountPoint,
            bool renameObjectToId,
            ModuleDefinition startingModuleDefinition)
        {
            if (placed == null)
                return;

            Hardpoint hardpoint = placed.GetComponent<Hardpoint>();
            if (hardpoint == null)
            {
                Debug.LogWarning("[BoatBuilder] Placed hardpoint prefab has no Hardpoint component.", placed);
                return;
            }

            Undo.RecordObject(hardpoint, "Configure Hardpoint");

            SerializedObject hardpointSO = new SerializedObject(hardpoint);

            SerializedProperty startingModuleProp = hardpointSO.FindProperty("startingModuleDefinition");
            if (startingModuleProp != null)
                startingModuleProp.objectReferenceValue = startingModuleDefinition;

            SerializedProperty typeProp = hardpointSO.FindProperty("hardpointType");
            if (typeProp != null)
                typeProp.enumValueIndex = (int)selectedType;

            string resolvedPrefix = ResolveHardpointPrefix(idPrefix, selectedType);
            string generatedId = GenerateNextHardpointId(boatRoot, resolvedPrefix);

            SerializedProperty idProp = hardpointSO.FindProperty("hardpointId");
            if (idProp != null)
                idProp.stringValue = generatedId;

            SerializedProperty mountProp = hardpointSO.FindProperty("mountPoint");
            if (mountProp != null)
            {
                Transform mount = placed.transform.Find("MountPoint");
                if (mount == null && autoCreateMountPoint)
                {
                    var mountGO = new GameObject("MountPoint");
                    Undo.RegisterCreatedObjectUndo(mountGO, "Create Hardpoint MountPoint");
                    mount = mountGO.transform;
                    Undo.SetTransformParent(mount, placed.transform, "Parent MountPoint");
                    mount.localPosition = Vector3.zero;
                    mount.localRotation = Quaternion.identity;
                    mount.localScale = Vector3.one;
                }

                if (mount != null)
                    mountProp.objectReferenceValue = mount;
            }

            hardpointSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hardpoint);

            var interactable = placed.GetComponent<HardpointInteractable>();
            if (interactable != null)
            {
                Undo.RecordObject(interactable, "Configure Hardpoint Interactable");
                SerializedObject interactableSO = new SerializedObject(interactable);

                SerializedProperty hpRef = interactableSO.FindProperty("hardpoint");
                if (hpRef != null)
                    hpRef.objectReferenceValue = hardpoint;

                SerializedProperty promptAnchorProp = interactableSO.FindProperty("promptAnchor");
                if (promptAnchorProp != null && promptAnchorProp.objectReferenceValue == null)
                    promptAnchorProp.objectReferenceValue = hardpoint.MountPoint;

                interactableSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(interactable);
            }

            if (renameObjectToId)
            {
                Undo.RecordObject(placed, "Rename Hardpoint");
                placed.name = generatedId;
                EditorUtility.SetDirty(placed);
            }

            Selection.activeGameObject = placed;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

    private static string ResolveHardpointPrefix(string configuredPrefix, HardpointType type)
        {
            if (!string.IsNullOrWhiteSpace(configuredPrefix) &&
                !string.Equals(configuredPrefix.Trim(), "hardpoint", StringComparison.OrdinalIgnoreCase))
            {
                return SanitizeIdToken(configuredPrefix.Trim());
            }

            return type switch
            {
                HardpointType.Engine => "engine",
                HardpointType.Pump => "pump",
                HardpointType.Utility => "utility",
                HardpointType.Storage => "storage",
                HardpointType.Weapon => "weapon",
                HardpointType.Electronics => "electronics",
                HardpointType.Helm => "helm",
                _ => "hardpoint"
            };
        }

    private static string GenerateNextHardpointId(Transform boatRoot, string prefix)
        {
            prefix = SanitizeIdToken(prefix);
            int maxFound = 0;

            if (boatRoot != null)
            {
                var existing = boatRoot.GetComponentsInChildren<Hardpoint>(true);
                foreach (var hp in existing)
                {
                    if (hp == null)
                        continue;

                    string existingId = hp.HardpointId;
                    if (string.IsNullOrWhiteSpace(existingId))
                        continue;

                    if (!existingId.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string suffix = existingId.Substring(prefix.Length + 1);
                    if (int.TryParse(suffix, out int n))
                        maxFound = Mathf.Max(maxFound, n);
                }
            }

            return $"{prefix}_{(maxFound + 1):00}";
        }

    private static string SanitizeIdToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "hardpoint";

            string s = value.Trim().ToLowerInvariant();
            s = s.Replace(" ", "_");
            return s;
        }

    public static int CountHardpointControllerWarnings(Transform boatRoot)
        {
            if (boatRoot == null)
                return 0;

            int warnings = 0;
            Hardpoint[] hardpoints = boatRoot.GetComponentsInChildren<Hardpoint>(true);

            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hp = hardpoints[i];
                if (hp == null)
                    continue;

                if (!HardpointLooksControllable(hp))
                    continue;

                if (!hp.HasAnyController())
                    warnings++;
            }

            return warnings;
        }

    public static void LogHardpointControllerWarnings(Transform boatRoot)
        {
            if (boatRoot == null)
            {
                Debug.LogWarning("[BoatBuilder] Cannot validate hardpoint controllers: BoatRoot is null.");
                return;
            }

            Hardpoint[] hardpoints = boatRoot.GetComponentsInChildren<Hardpoint>(true);
            int warnings = 0;

            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hp = hardpoints[i];
                if (hp == null)
                    continue;

                if (!HardpointLooksControllable(hp))
                    continue;

                if (hp.HasAnyController())
                    continue;

                warnings++;

                Debug.LogWarning(
                    $"[BoatBuilder] Hardpoint '{hp.HardpointId}' accepts controllable modules ({hp.GetAcceptedTypesText()}) " +
                    "but has no controller assigned.",
                    hp);
            }

            if (warnings == 0)
                Debug.Log("[BoatBuilder] Hardpoint controller validation passed.");
        }

    private static bool HardpointLooksControllable(Hardpoint hardpoint)
        {
            if (hardpoint == null)
                return false;

            HardpointType[] accepted = hardpoint.GetAcceptedTypes();

            for (int i = 0; i < accepted.Length; i++)
            {
                if (accepted[i] == HardpointType.Weapon)
                    return true;
            }

            return false;
        }

    public static void InstallStartingModulesUnderRoot(Transform boatRoot)
        {
            if (boatRoot == null)
            {
                Debug.LogWarning("[BoatBuilder] Cannot install starting modules: BoatRoot is null.");
                return;
            }

            Hardpoint[] hardpoints = boatRoot.GetComponentsInChildren<Hardpoint>(true);
            int installed = 0;
            int skipped = 0;

            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hp = hardpoints[i];
                if (hp == null)
                    continue;

                if (hp.HasInstalledModule)
                {
                    skipped++;
                    continue;
                }

                if (hp.StartingModuleDefinition == null)
                {
                    skipped++;
                    continue;
                }

                if (hp.EditorInstallStartingModule())
                    installed++;
                else
                    skipped++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[BoatBuilder] Installed starting modules. Installed={installed}, Skipped={skipped}", boatRoot);
        }

    private static Hardpoint[] GetSelectedHardpointsIncludingChildren()
        {
            UnityEngine.Object[] selected = Selection.objects;

            if (selected == null || selected.Length == 0)
                return Array.Empty<Hardpoint>();

            List<Hardpoint> hardpoints = new List<Hardpoint>();

            for (int i = 0; i < selected.Length; i++)
            {
                Hardpoint hp = null;

                if (selected[i] is GameObject go)
                    hp = go.GetComponentInParent<Hardpoint>();
                else if (selected[i] is Component c)
                    hp = c.GetComponentInParent<Hardpoint>();

                if (hp == null)
                    continue;

                if (!hardpoints.Contains(hp))
                    hardpoints.Add(hp);
            }

            return hardpoints.ToArray();
        }

    public static void InstallSelectedStartingModules()
        {
            Hardpoint[] hardpoints = GetSelectedHardpointsIncludingChildren();

            if (hardpoints == null || hardpoints.Length == 0)
            {
                Debug.LogWarning("[BoatBuilder] Select one or more Hardpoints to install starting modules.");
                return;
            }

            int installed = 0;
            int skipped = 0;

            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hp = hardpoints[i];
                if (hp == null)
                    continue;

                Debug.Log(
                    $"[BoatBuilder] Checking hardpoint '{hp.HardpointId}' " +
                    $"hasInstalled={hp.HasInstalledModule} " +
                    $"startingModule={(hp.StartingModuleDefinition != null ? hp.StartingModuleDefinition.DisplayName : "NULL")} " +
                    $"accepts={hp.GetAcceptedTypesText()}",
                    hp);

                if (hp.HasInstalledModule)
                {
                    skipped++;
                    Debug.LogWarning(
                        $"[BoatBuilder] Skipping '{hp.HardpointId}': already has installed module '{hp.InstalledModule.name}'.",
                        hp);
                    continue;
                }

                ModuleDefinition module = hp.StartingModuleDefinition;

                if (module == null)
                {
                    skipped++;
                    Debug.LogWarning(
                        $"[BoatBuilder] Skipping '{hp.HardpointId}': no starting module assigned.",
                        hp);
                    continue;
                }

                if (module.InstalledPrefab == null)
                {
                    skipped++;
                    Debug.LogWarning(
                        $"[BoatBuilder] Skipping '{hp.HardpointId}': starting module '{module.DisplayName}' has no InstalledPrefab assigned.",
                        module);
                    continue;
                }

                if (!hp.ModuleMatchesAcceptedTypes(module))
                {
                    skipped++;
                    Debug.LogWarning(
                        $"[BoatBuilder] Skipping '{hp.HardpointId}': module '{module.DisplayName}' is not compatible. " +
                        $"Hardpoint accepts: {hp.GetAcceptedTypesText()}",
                        hp);
                    continue;
                }

                if (hp.EditorInstallStartingModule())
                {
                    installed++;
                    Debug.Log(
                        $"[BoatBuilder] Installed '{module.DisplayName}' on '{hp.HardpointId}'.",
                        hp);
                }
                else
                {
                    skipped++;
                    Debug.LogWarning(
                        $"[BoatBuilder] Failed to install '{module.DisplayName}' on '{hp.HardpointId}' for an unknown reason. " +
                        "Check Hardpoint.EditorInstallStartingModule logs.",
                        hp);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[BoatBuilder] Installed selected starting modules. Installed={installed}, Skipped={skipped}");
        }

    public static void UninstallSelectedModules()
        {
            Hardpoint[] hardpoints = GetSelectedHardpointsIncludingChildren();

            if (hardpoints == null || hardpoints.Length == 0)
            {
                Debug.LogWarning("[BoatBuilder] Select one or more Hardpoints to uninstall modules.");
                return;
            }

            int removed = 0;
            int skipped = 0;

            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hp = hardpoints[i];
                if (hp == null)
                    continue;

                if (!hp.HasInstalledModule)
                {
                    skipped++;
                    continue;
                }

                if (hp.EditorUninstallInstalledModule())
                    removed++;
                else
                    skipped++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[BoatBuilder] Uninstalled selected modules. Removed={removed}, Skipped={skipped}");
        }

    public static void LinkSelectedTurretWithController()
        {
            Hardpoint hardpoint = null;
            TurretControlStation station = null;

            UnityEngine.Object[] selected = Selection.objects;

            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is GameObject go)
                {
                    if (hardpoint == null)
                        hardpoint = go.GetComponentInParent<Hardpoint>();

                    if (station == null)
                        station = go.GetComponentInParent<TurretControlStation>();
                }
                else if (selected[i] is Component c)
                {
                    if (hardpoint == null)
                        hardpoint = c.GetComponentInParent<Hardpoint>();

                    if (station == null)
                        station = c.GetComponentInParent<TurretControlStation>();
                }
            }

            if (hardpoint == null || station == null)
            {
                Debug.LogWarning("[BoatBuilder] Select both a Hardpoint and a TurretControlStation, then click Link Turret With Controller.");
                return;
            }

            Undo.RecordObject(hardpoint, "Link Turret Controller To Hardpoint");
            Undo.RecordObject(station, "Link Turret Controller To Hardpoint");

            hardpoint.EditorAddController(station);

            SerializedObject stationSO = new SerializedObject(station);
            SerializedProperty hardpointProp = stationSO.FindProperty("hardpoint");

            if (hardpointProp != null)
            {
                hardpointProp.objectReferenceValue = hardpoint;
                stationSO.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[BoatBuilder] TurretControlStation has no serialized 'hardpoint' field.", station);
            }

            EditorUtility.SetDirty(hardpoint);
            EditorUtility.SetDirty(station);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[BoatBuilder] Linked controller '{station.name}' to hardpoint '{hardpoint.HardpointId}'.", hardpoint);
        }

    public static void ApplyStartingModuleToSelectedHardpoints()
        {
            Hardpoint[] hardpoints = GetSelectedHardpointsIncludingChildren();

            if (hardpoints == null || hardpoints.Length == 0)
            {
                Debug.LogWarning("[BoatBuilder] Select one or more Hardpoints to apply the starting module.");
                return;
            }

            ModuleDefinition module = _ctx.HardpointStartingModuleDefinition;

            if (module == null)
            {
                Debug.LogWarning("[BoatBuilder] No Starting Module selected in the Boat Builder window.");
                return;
            }

            int applied = 0;
            int skipped = 0;

            for (int i = 0; i < hardpoints.Length; i++)
            {
                Hardpoint hp = hardpoints[i];
                if (hp == null)
                    continue;

                if (!hp.ModuleMatchesAcceptedTypes(module))
                {
                    skipped++;
                    Debug.LogWarning(
                        $"[BoatBuilder] Cannot apply '{module.DisplayName}' to '{hp.HardpointId}'. " +
                        $"Hardpoint accepts: {hp.GetAcceptedTypesText()}",
                        hp);
                    continue;
                }

                hp.EditorSetStartingModuleDefinition(module);
                applied++;

                Debug.Log(
                    $"[BoatBuilder] Applied starting module '{module.DisplayName}' to hardpoint '{hp.HardpointId}'.",
                    hp);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[BoatBuilder] Applied starting modules. Applied={applied}, Skipped={skipped}");
        }

    private static void DrawSelectedHardpointControllerLinks()
    {
        UnityEngine.Object[] selected = Selection.objects;

        if (selected == null || selected.Length == 0)
            return;

        for (int i = 0; i < selected.Length; i++)
        {
            Hardpoint hp = null;
            TurretControlStation station = null;

            if (selected[i] is GameObject go)
            {
                hp = go.GetComponentInParent<Hardpoint>();
                station = go.GetComponentInParent<TurretControlStation>();
            }
            else if (selected[i] is Component c)
            {
                hp = c.GetComponentInParent<Hardpoint>();
                station = c.GetComponentInParent<TurretControlStation>();
            }

            if (hp != null)
                DrawControllerLinksForHardpoint(hp);

            if (station != null)
                DrawControllerLinkForStation(station);
        }
    }

    private static void DrawControllerLinksForHardpoint(Hardpoint hardpoint)
    {
        if (hardpoint == null || hardpoint.Controllers == null)
            return;

        IReadOnlyList<MonoBehaviour> controllers = hardpoint.Controllers;

        for (int i = 0; i < controllers.Count; i++)
        {
            if (controllers[i] is TurretControlStation station)
                DrawHardpointControllerLine(hardpoint, station);
        }
    }

    private static void DrawControllerLinkForStation(TurretControlStation station)
    {
        if (station == null || station.LinkedHardpoint == null)
            return;

        DrawHardpointControllerLine(station.LinkedHardpoint, station);
    }

    private static void DrawHardpointControllerLine(Hardpoint hardpoint, TurretControlStation station)
    {
        if (hardpoint == null || station == null)
            return;

        Transform hardpointAnchor = hardpoint.ModuleAnchor != null
            ? hardpoint.ModuleAnchor
            : hardpoint.MountPoint != null
                ? hardpoint.MountPoint
                : hardpoint.transform;

        Transform stationAnchor = station.LinkAnchor != null
            ? station.LinkAnchor
            : station.transform;

        Vector3 a = hardpointAnchor.position;
        Vector3 b = stationAnchor.position;

        Color oldColor = Handles.color;

        Handles.color = new Color(0.1f, 0.85f, 1f, 0.95f);
        Handles.DrawAAPolyLine(4f, a, b);

        Handles.color = new Color(0.1f, 0.85f, 1f, 0.35f);
        Handles.DrawSolidDisc(a, Vector3.forward, 0.08f);
        Handles.DrawSolidDisc(b, Vector3.forward, 0.08f);

        Handles.color = oldColor;

        Vector3 labelPos = Vector3.Lerp(a, b, 0.5f);
        Handles.Label(labelPos, "Turret Link");
    }
}
#endif
