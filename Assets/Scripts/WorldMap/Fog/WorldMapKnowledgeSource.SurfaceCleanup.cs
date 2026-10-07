using UnityEngine;

public sealed partial class WorldMapKnowledgeSource
{
    [Header("Surface Chart Gap Cleanup")]
    [Tooltip("After a legitimate surface chart expands coverage, fill unknown connected regions up to this many knowledge-grid cells. Zero disables cleanup. Surface geography only.")]
    [Range(0, 64)] [SerializeField] private int maximumUnknownComponentCells = 8;

    // Trusted host-side source seam; clients request integration of their carried item, never supply grants.
    public bool TryCommitCartographicSource(WorldMapCartographicPayload payload, out string reason)
    {
        if (!GameplayAuthority.IsAuthoritative) { reason = "Shared cartography requires authority."; return false; }
        EnsureInitialized();
        return State.TryApplyPayload(payload, out reason, Mathf.Clamp(maximumUnknownComponentCells, 0, 64));
    }
}
