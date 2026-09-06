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

        if (selectedType == HardpointType.Winch)
        {
            TetherWinchLink tetherLink = placed.GetComponent<TetherWinchLink>();
            if (tetherLink == null)
                tetherLink = Undo.AddComponent<TetherWinchLink>(placed);

            if (tetherLink != null)
                tetherLink.EditorSetOwner(hardpoint);
        }

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
            HardpointType.Rudder => "rudder",
            HardpointType.Keel => "keel",
            HardpointType.Anchor => "anchor",
            HardpointType.Winch => "winch",
            HardpointType.TetherPayload => "payload",
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
            if (accepted[i] == HardpointType.Weapon ||
                accepted[i] == HardpointType.Helm)
            {
                return true;
            }
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

    private static void EnsurePilotStationIdForHelmLink(
        PilotChairInteractable chair)
    {
        if (chair == null ||
            !string.IsNullOrWhiteSpace(
                chair.PilotStationId))
        {
            return;
        }

        Boat boat =
            chair.GetComponentInParent<Boat>();

        Transform root =
            boat != null
                ? boat.transform
                : chair.transform.root;

        HashSet<string> used =
            new HashSet<string>(
                StringComparer.Ordinal);

        if (root != null)
        {
            PilotChairInteractable[] chairs =
                root.GetComponentsInChildren<PilotChairInteractable>(
                    true);

            for (int i = 0;
                 i < chairs.Length;
                 i++)
            {
                PilotChairInteractable existing =
                    chairs[i];

                if (existing == null ||
                    string.IsNullOrWhiteSpace(
                        existing.PilotStationId))
                {
                    continue;
                }

                used.Add(
                    existing.PilotStationId.Trim());
            }
        }

        int index =
            1;

        string candidate;

        do
        {
            candidate =
                $"pilot_station_{index:00}";

            index++;
        }
        while (used.Contains(candidate));

        Undo.RecordObject(
            chair,
            "Assign Pilot Station Stable ID");

        chair.EditorSetPilotStationId(
            candidate);

        EditorUtility.SetDirty(
            chair);

        Debug.Log(
            $"[BoatBuilder] Assigned Pilot Station ID '{candidate}' to '{chair.name}'.",
            chair);
    }

    public static void LinkSelectedHelmWithPilotChair()
    {
        Hardpoint hardpoint = null;
        PilotChairInteractable chair = null;

        UnityEngine.Object[] selected = Selection.objects;

        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning(
                "[BoatBuilder] Select both a Helm Hardpoint and a Pilot Chair, then click Link Selected Helm ↔ Chair.");
            return;
        }

        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] is GameObject go)
            {
                if (hardpoint == null)
                    hardpoint = go.GetComponentInParent<Hardpoint>();

                if (chair == null)
                    chair = go.GetComponentInParent<PilotChairInteractable>();
            }
            else if (selected[i] is Component c)
            {
                if (hardpoint == null)
                    hardpoint = c.GetComponentInParent<Hardpoint>();

                if (chair == null)
                    chair = c.GetComponentInParent<PilotChairInteractable>();
            }
        }

        if (hardpoint == null || chair == null)
        {
            Debug.LogWarning(
                "[BoatBuilder] Select both a Helm Hardpoint and a Pilot Chair, then click Link Selected Helm ↔ Chair.");
            return;
        }

        bool acceptsHelm = false;
        HardpointType[] accepted = hardpoint.GetAcceptedTypes();

        for (int i = 0; i < accepted.Length; i++)
        {
            if (accepted[i] == HardpointType.Helm)
            {
                acceptsHelm = true;
                break;
            }
        }

        if (!acceptsHelm)
        {
            Debug.LogWarning(
                $"[BoatBuilder] Hardpoint '{hardpoint.HardpointId}' does not accept Helm modules.",
                hardpoint);
            return;
        }

        Boat hardpointBoat = hardpoint.GetComponentInParent<Boat>();
        Boat chairBoat = chair.GetComponentInParent<Boat>();

        if (hardpointBoat != null &&
            chairBoat != null &&
            hardpointBoat != chairBoat)
        {
            Debug.LogWarning(
                "[BoatBuilder] Cannot link a Pilot Chair to a Helm Hardpoint on a different boat.",
                chair);
            return;
        }

        EnsurePilotStationIdForHelmLink(
            chair);

        Hardpoint previousHelm =
            chair.LinkedHelmHardpoint;

        Undo.RecordObject(
            hardpoint,
            "Link Pilot Chair To Helm Hardpoint");

        Undo.RecordObject(
            chair,
            "Link Pilot Chair To Helm Hardpoint");

        if (previousHelm != null &&
            previousHelm != hardpoint)
        {
            Undo.RecordObject(
                previousHelm,
                "Link Pilot Chair To Helm Hardpoint");
        }

        if (!chair.TryLinkToHelm(hardpoint))
        {
            Debug.LogWarning(
                $"[BoatBuilder] Failed to link pilot chair '{chair.name}' to helm hardpoint " +
                $"'{hardpoint.HardpointId}'.",
                chair);
            return;
        }

        EditorUtility.SetDirty(hardpoint);
        EditorUtility.SetDirty(chair);

        if (previousHelm != null)
            EditorUtility.SetDirty(previousHelm);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());

        string capacityText = "no installed HelmModule";

        if (hardpoint.TryGetInstalledModuleComponent(
                out HelmModule helm))
        {
            capacityText =
                $"capacity {helm.StationConnectionCapacity}";
        }

        Debug.Log(
            $"[BoatBuilder] Linked pilot chair '{chair.name}' to helm hardpoint " +
            $"'{hardpoint.HardpointId}' ({capacityText}).",
            hardpoint);

        SceneView.RepaintAll();
    }

    public static void UnlinkSelectedPilotChairFromHelm()
    {
        PilotChairInteractable chair = null;

        UnityEngine.Object[] selected =
            Selection.objects;

        if (selected != null)
        {
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is GameObject go)
                    chair = go.GetComponentInParent<PilotChairInteractable>();
                else if (selected[i] is Component c)
                    chair = c.GetComponentInParent<PilotChairInteractable>();

                if (chair != null)
                    break;
            }
        }

        if (chair == null)
        {
            Debug.LogWarning(
                "[BoatBuilder] Select a Pilot Chair to unlink it from its Helm.");
            return;
        }

        Hardpoint previousHelm =
            chair.LinkedHelmHardpoint;

        if (previousHelm == null)
        {
            Debug.Log(
                $"[BoatBuilder] Pilot chair '{chair.name}' is already unlinked.",
                chair);
            return;
        }

        Undo.RecordObject(
            chair,
            "Unlink Pilot Chair From Helm");

        Undo.RecordObject(
            previousHelm,
            "Unlink Pilot Chair From Helm");

        string oldId =
            previousHelm.HardpointId;

        if (!chair.UnlinkHelm())
        {
            Debug.LogWarning(
                $"[BoatBuilder] Failed to unlink pilot chair '{chair.name}' from helm hardpoint '{oldId}'.",
                chair);
            return;
        }

        EditorUtility.SetDirty(chair);
        EditorUtility.SetDirty(previousHelm);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());

        Debug.Log(
            $"[BoatBuilder] Unlinked pilot chair '{chair.name}' from helm hardpoint '{oldId}'.",
            chair);

        SceneView.RepaintAll();
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

    public static void LinkSelectedWinchWithPayload()
    {
        Hardpoint[] selected = GetSelectedHardpointsIncludingChildren();

        if (selected == null || selected.Length != 2)
        {
            Debug.LogWarning(
                "[BoatBuilder] Select exactly ONE Winch hardpoint and ONE Tether Payload hardpoint, then click Link Selected Winch ↔ Payload.");
            return;
        }

        Hardpoint winch = null;
        Hardpoint payload = null;

        for (int i = 0; i < selected.Length; i++)
        {
            Hardpoint hp = selected[i];
            if (hp == null)
                continue;

            if (HardpointAcceptsType(hp, HardpointType.Winch))
                winch = hp;

            if (HardpointAcceptsType(hp, HardpointType.TetherPayload) ||
                HardpointAcceptsType(hp, HardpointType.Anchor))
            {
                payload = hp;
            }
        }

        if (winch == null || payload == null || winch == payload)
        {
            Debug.LogWarning(
                "[BoatBuilder] Selection must contain one Winch hardpoint and one Tether Payload/legacy Anchor hardpoint.");
            return;
        }

        Boat winchBoat = winch.GetComponentInParent<Boat>();
        Boat payloadBoat = payload.GetComponentInParent<Boat>();

        if (winchBoat != null && payloadBoat != null && winchBoat != payloadBoat)
        {
            Debug.LogWarning(
                "[BoatBuilder] Cannot link a winch to a payload hardpoint on a different boat.",
                winch);
            return;
        }

        Transform root = winchBoat != null
            ? winchBoat.transform
            : winch.transform.root;

        if (root != null)
        {
            TetherWinchLink[] existingLinks =
                root.GetComponentsInChildren<TetherWinchLink>(true);

            for (int i = 0; i < existingLinks.Length; i++)
            {
                TetherWinchLink existing = existingLinks[i];
                if (existing == null || existing.LinkedPayloadHardpoint != payload)
                    continue;

                Hardpoint existingOwner = existing.OwnerWinchHardpoint;
                if (existingOwner == winch)
                    continue;

                Undo.RecordObject(existing, "Reassign Tether Payload Link");
                existing.Unlink();
                EditorUtility.SetDirty(existing);
            }
        }

        TetherWinchLink link = winch.GetComponent<TetherWinchLink>();
        if (link == null)
            link = Undo.AddComponent<TetherWinchLink>(winch.gameObject);

        if (link == null)
            return;

        Undo.RecordObject(link, "Link Winch To Tether Payload");
        link.EditorSetOwner(winch);

        if (!link.TryLink(payload))
        {
            Debug.LogWarning(
                $"[BoatBuilder] Failed to link winch '{winch.HardpointId}' to payload '{payload.HardpointId}'.",
                winch);
            return;
        }

        EditorUtility.SetDirty(link);
        EditorUtility.SetDirty(winch);
        EditorUtility.SetDirty(payload);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(
            $"[BoatBuilder] Linked winch '{winch.HardpointId}' ↔ payload '{payload.HardpointId}'.",
            winch);

        SceneView.RepaintAll();
    }

    public static void UnlinkSelectedTether()
    {
        TetherWinchLink target = null;
        UnityEngine.Object[] selected = Selection.objects;

        if (selected != null)
        {
            for (int i = 0; i < selected.Length && target == null; i++)
            {
                Hardpoint hp = null;

                if (selected[i] is GameObject go)
                    hp = go.GetComponentInParent<Hardpoint>();
                else if (selected[i] is Component c)
                    hp = c.GetComponentInParent<Hardpoint>();

                if (hp == null)
                    continue;

                target = hp.GetComponent<TetherWinchLink>();
                if (target != null)
                    break;

                Boat boat = hp.GetComponentInParent<Boat>();
                Transform root = boat != null ? boat.transform : hp.transform.root;
                if (root == null)
                    continue;

                TetherWinchLink[] links = root.GetComponentsInChildren<TetherWinchLink>(true);
                for (int j = 0; j < links.Length; j++)
                {
                    if (links[j] != null && links[j].LinkedPayloadHardpoint == hp)
                    {
                        target = links[j];
                        break;
                    }
                }
            }
        }

        if (target == null || !target.HasLinkedPayload)
        {
            Debug.LogWarning(
                "[BoatBuilder] Select a linked Winch hardpoint or its linked Tether Payload hardpoint to unlink it.");
            return;
        }

        string winchId = target.OwnerWinchHardpoint != null
            ? target.OwnerWinchHardpoint.HardpointId
            : "Winch";
        string payloadId = target.LinkedPayloadHardpoint != null
            ? target.LinkedPayloadHardpoint.HardpointId
            : "Payload";

        Undo.RecordObject(target, "Unlink Tether Payload");
        target.Unlink();
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[BoatBuilder] Unlinked winch '{winchId}' from payload '{payloadId}'.");
        SceneView.RepaintAll();
    }

    private static bool HardpointAcceptsType(Hardpoint hardpoint, HardpointType type)
    {
        if (hardpoint == null)
            return false;

        HardpointType[] accepted = hardpoint.GetAcceptedTypes();
        for (int i = 0; i < accepted.Length; i++)
        {
            if (accepted[i] == type)
                return true;
        }

        return false;
    }

    private static void DrawSelectedTetherLinks()
    {
        UnityEngine.Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
            return;

        HashSet<TetherWinchLink> drawn = new HashSet<TetherWinchLink>();

        for (int i = 0; i < selected.Length; i++)
        {
            Hardpoint selectedHardpoint = null;

            if (selected[i] is GameObject go)
                selectedHardpoint = go.GetComponentInParent<Hardpoint>();
            else if (selected[i] is Component c)
                selectedHardpoint = c.GetComponentInParent<Hardpoint>();

            if (selectedHardpoint == null)
                continue;

            TetherWinchLink direct = selectedHardpoint.GetComponent<TetherWinchLink>();
            if (direct != null && direct.HasLinkedPayload && drawn.Add(direct))
                DrawTetherLink(direct);

            Boat boat = selectedHardpoint.GetComponentInParent<Boat>();
            Transform root = boat != null ? boat.transform : selectedHardpoint.transform.root;
            if (root == null)
                continue;

            TetherWinchLink[] links = root.GetComponentsInChildren<TetherWinchLink>(true);
            for (int j = 0; j < links.Length; j++)
            {
                TetherWinchLink link = links[j];
                if (link == null || link.LinkedPayloadHardpoint != selectedHardpoint)
                    continue;

                if (drawn.Add(link))
                    DrawTetherLink(link);
            }
        }
    }

    private static void DrawTetherLink(TetherWinchLink link)
    {
        if (link == null || link.OwnerWinchHardpoint == null || link.LinkedPayloadHardpoint == null)
            return;

        Hardpoint winch = link.OwnerWinchHardpoint;
        Hardpoint payload = link.LinkedPayloadHardpoint;

        Vector3 a = winch.ModuleAnchor != null
            ? winch.ModuleAnchor.position
            : winch.transform.position;
        Vector3 b = payload.ModuleAnchor != null
            ? payload.ModuleAnchor.position
            : payload.transform.position;

        Color oldColor = Handles.color;
        Handles.color = new Color(1f, 0.68f, 0.18f, 0.95f);
        Handles.DrawAAPolyLine(4f, a, b);

        Handles.color = new Color(1f, 0.68f, 0.18f, 0.35f);
        Handles.DrawSolidDisc(a, Vector3.forward, 0.08f);
        Handles.DrawSolidDisc(b, Vector3.forward, 0.08f);
        Handles.color = oldColor;

        Handles.Label(Vector3.Lerp(a, b, 0.5f), "Tether Link");
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
            PilotChairInteractable pilotChair = null;

            if (selected[i] is GameObject go)
            {
                hp = go.GetComponentInParent<Hardpoint>();
                station = go.GetComponentInParent<TurretControlStation>();
                pilotChair = go.GetComponentInParent<PilotChairInteractable>();
            }
            else if (selected[i] is Component c)
            {
                hp = c.GetComponentInParent<Hardpoint>();
                station = c.GetComponentInParent<TurretControlStation>();
                pilotChair = c.GetComponentInParent<PilotChairInteractable>();
            }

            if (hp != null)
                DrawControllerLinksForHardpoint(hp);

            if (station != null)
                DrawControllerLinkForStation(station);

            if (pilotChair != null)
                DrawControllerLinkForPilotChair(pilotChair);
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
            {
                DrawHardpointControllerLine(hardpoint, station);
                continue;
            }

            if (controllers[i] is PilotChairInteractable pilotChair)
                DrawHelmControllerLine(hardpoint, pilotChair);
        }
    }

    private static void DrawControllerLinkForStation(TurretControlStation station)
    {
        if (station == null || station.LinkedHardpoint == null)
            return;

        DrawHardpointControllerLine(station.LinkedHardpoint, station);
    }

    private static void DrawControllerLinkForPilotChair(
        PilotChairInteractable chair)
    {
        if (chair == null ||
            chair.LinkedHelmHardpoint == null)
        {
            return;
        }

        DrawHelmControllerLine(
            chair.LinkedHelmHardpoint,
            chair);
    }

    private static void DrawHelmControllerLine(
        Hardpoint hardpoint,
        PilotChairInteractable chair)
    {
        if (hardpoint == null || chair == null)
            return;

        Transform hardpointAnchor =
            hardpoint.ModuleAnchor != null
                ? hardpoint.ModuleAnchor
                : hardpoint.MountPoint != null
                    ? hardpoint.MountPoint
                    : hardpoint.transform;

        Transform chairAnchor =
            chair.GetPromptAnchor();

        if (chairAnchor == null)
            chairAnchor = chair.transform;

        Vector3 a =
            hardpointAnchor.position;

        Vector3 b =
            chairAnchor.position;

        Color oldColor =
            Handles.color;

        Handles.color =
            new Color(
                0.3f,
                1f,
                0.55f,
                0.95f);

        Handles.DrawAAPolyLine(
            4f,
            a,
            b);

        Handles.color =
            new Color(
                0.3f,
                1f,
                0.55f,
                0.35f);

        Handles.DrawSolidDisc(
            a,
            Vector3.forward,
            0.08f);

        Handles.DrawSolidDisc(
            b,
            Vector3.forward,
            0.08f);

        Handles.color =
            oldColor;

        Vector3 labelPos =
            Vector3.Lerp(
                a,
                b,
                0.5f);

        Handles.Label(
            labelPos,
            "Helm Link");
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
