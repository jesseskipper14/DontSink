#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CompartmentTopologyGenerator
{
    public static void GenerateFromBoatRoot(Transform boatRoot)
    {
        if (boatRoot == null)
        {
            Debug.LogWarning("[CompartmentTopologyGenerator] BoatRoot is null.");
            return;
        }

        Boat boat = boatRoot.GetComponent<Boat>();
        if (boat == null)
            boat = boatRoot.GetComponentInParent<Boat>();

        if (boat == null)
        {
            Debug.LogWarning($"[CompartmentTopologyGenerator] No Boat found for '{boatRoot.name}'.", boatRoot);
            return;
        }

        Generate(boat);
    }

    public static void Generate(Boat boat)
    {
        if (boat == null)
        {
            Debug.LogWarning("[CompartmentTopologyGenerator] Boat is null.");
            return;
        }

        Undo.RecordObject(boat, "Generate Compartment Topology");

        if (boat.Connections == null)
            boat.Connections = new List<CompartmentConnection>();
        else
            boat.Connections.Clear();

        if (boat.Compartments == null)
            boat.Compartments = new List<Compartment>();

        // Clear per-compartment backrefs and generated external sources only.
        foreach (Compartment c in boat.Compartments)
        {
            if (c == null)
                continue;

            Undo.RecordObject(c, "Clear generated topology");
            c.connections.Clear();

            if (c.externalWaterSources == null)
                c.externalWaterSources = new List<ExternalWaterSource>();

            c.externalWaterSources.RemoveAll(src =>
                src != null &&
                !string.IsNullOrWhiteSpace(src.name) &&
                src.name.StartsWith(CompartmentLinkAuthoring.GeneratedExternalPrefix));
        }

        CompartmentLinkAuthoring[] links = boat.GetComponentsInChildren<CompartmentLinkAuthoring>(true);

        int internalCount = 0;
        int externalCount = 0;
        int invalidCount = 0;

        foreach (CompartmentLinkAuthoring link in links)
        {
            if (link == null)
                continue;

            var resolution = link.Resolve();

            switch (resolution.resolutionType)
            {
                case CompartmentLinkResolutionType.Internal:
                    {
                        CompartmentConnection conn = new CompartmentConnection
                        {
                            A = resolution.A,
                            B = resolution.B,
                            transform = link.transform
                        };

                        link.ApplyGeometryToConnection(conn);

                        boat.Connections.Add(conn);

                        if (conn.A != null && !conn.A.connections.Contains(conn))
                            conn.A.connections.Add(conn);

                        if (conn.B != null && !conn.B.connections.Contains(conn))
                            conn.B.connections.Add(conn);

                        internalCount++;
                        break;
                    }

                case CompartmentLinkResolutionType.ExternalExposure:
                    {
                        Compartment exposed = resolution.ExposedCompartment;
                        if (exposed != null)
                        {
                            Undo.RecordObject(exposed, "Generate external exposure");

                            if (exposed.externalWaterSources == null)
                                exposed.externalWaterSources = new List<ExternalWaterSource>();

                            ExternalWaterSource src = link.BuildExternalSource();
                            exposed.externalWaterSources.Add(src);
                            externalCount++;
                        }
                        else
                        {
                            invalidCount++;
                            Debug.LogWarning(
                                $"[CompartmentTopologyGenerator] Link '{link.name}' resolved as external but no exposed compartment was found.",
                                link);
                        }

                        break;
                    }

                default:
                    {
                        invalidCount++;
                        Debug.LogWarning(
                            $"[CompartmentTopologyGenerator] Link '{link.name}' unresolved: {resolution.reason}",
                            link);
                        break;
                    }
            }
        }

        EditorUtility.SetDirty(boat);
        EditorSceneManager.MarkSceneDirty(boat.gameObject.scene);

        Debug.Log(
            $"[CompartmentTopologyGenerator] Generated topology for '{boat.name}'. " +
            $"Internal={internalCount}, External={externalCount}, Invalid={invalidCount}",
            boat);
    }
}
#endif