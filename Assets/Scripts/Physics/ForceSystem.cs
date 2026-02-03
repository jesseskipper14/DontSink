using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ForceSystem : MonoBehaviour
{
    private IForceBody body;

    private List<IForceProvider> allProviders = new();
    private List<IForceProvider> orderedProviders = new();

    void Awake()
    {
        body = GetComponent<IForceBody>();

        allProviders.AddRange(GetComponents<IForceProvider>());

        Debug.Log($"[ForceSystem] {name} providers: " +
          string.Join(", ", allProviders.Select(p => p.GetType().Name)));

        // Cache ordered providers once
        orderedProviders = allProviders
            .OfType<IOrderedForceProvider>()
            .OrderBy(p => p.Priority)
            .Cast<IForceProvider>()
            .ToList();
    }

    void FixedUpdate()
    {
        // 1️⃣ Ordered forces first
        foreach (var provider in orderedProviders)
        {
            var ordered = (IOrderedForceProvider)provider;
            if (!ordered.Enabled) continue;

            provider.ApplyForces(body);
        }

        // 2️⃣ Unordered forces (legacy / simple)
        foreach (var provider in allProviders)
        {
            if (provider is IOrderedForceProvider) continue;
            provider.ApplyForces(body);
        }
    }
}
