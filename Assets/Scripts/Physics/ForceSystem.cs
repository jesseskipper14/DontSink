using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ForceSystem : MonoBehaviour
{
    [Header("Diagnostics")]
    [SerializeField] private bool verboseDiagnostics = true;

    private IForceBody body;

    private readonly List<IForceProvider> allProviders = new();
    private readonly List<IForceProvider> orderedProviders = new();

    private void Awake()
    {
        body = ResolveAuthoritativeBody();

        if (body == null)
        {
            Debug.LogError(
                $"[ForceSystem:{name}] No IForceBody authority found.",
                this);
            enabled = false;
            return;
        }

        allProviders.Clear();
        allProviders.AddRange(
            GetComponents<IForceProvider>());

        orderedProviders.Clear();
        orderedProviders.AddRange(
            allProviders
                .OfType<IOrderedForceProvider>()
                .OrderBy(p => p.Priority)
                .Cast<IForceProvider>());

        if (verboseDiagnostics)
            LogConfiguration();
    }

    /// <summary>
    /// Explicit body-authority rule:
    /// - Boat remains authoritative for Boat physics/mass/geometry.
    /// - Otherwise ForceBody2D is authoritative for generic/player/simple bodies.
    /// - A different IForceBody is only a compatibility fallback when neither exists.
    ///
    /// This removes component-order authority from GetComponent&lt;IForceBody&gt;().
    /// </summary>
    private IForceBody ResolveAuthoritativeBody()
    {
        Boat boat =
            GetComponent<Boat>();

        if (boat != null)
            return boat;

        ForceBody2D forceBody =
            GetComponent<ForceBody2D>();

        if (forceBody != null)
            return forceBody;

        return GetComponents<MonoBehaviour>()
            .OfType<IForceBody>()
            .FirstOrDefault();
    }

    private void FixedUpdate()
    {
        foreach (IForceProvider provider in orderedProviders)
        {
            IOrderedForceProvider ordered =
                (IOrderedForceProvider)provider;

            if (!ordered.Enabled)
                continue;

            provider.ApplyForces(body);
        }

        foreach (IForceProvider provider in allProviders)
        {
            if (provider is IOrderedForceProvider)
                continue;

            provider.ApplyForces(body);
        }
    }

    private void LogConfiguration()
    {
        IForceBody[] candidates =
            GetComponents<MonoBehaviour>()
                .OfType<IForceBody>()
                .ToArray();

        string candidateText =
            candidates.Length > 0
                ? string.Join(
                    ", ",
                    candidates.Select(
                        c => c.GetType().Name))
                : "(none)";

        string providerText =
            allProviders.Count > 0
                ? string.Join(
                    ", ",
                    allProviders.Select(
                        p =>
                        {
                            if (p is IOrderedForceProvider ordered)
                            {
                                return
                                    $"{p.GetType().Name}" +
                                    $"(ordered priority={ordered.Priority}, enabled={ordered.Enabled})";
                            }

                            return
                                $"{p.GetType().Name}(unordered)";
                        }))
                : "(none)";

        Debug.Log(
            $"[ForceSystem] DIAG selectedBody=" +
            $"{(body != null ? body.GetType().Name : "NULL")} | " +
            $"IForceBody candidates=[{candidateText}] | " +
            $"providers=[{providerText}]",
            this);
    }
}
