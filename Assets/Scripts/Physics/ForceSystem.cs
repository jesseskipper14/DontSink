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
        // Preserve existing behavior for now, but make the ambiguity visible.
        body = GetComponent<IForceBody>();

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