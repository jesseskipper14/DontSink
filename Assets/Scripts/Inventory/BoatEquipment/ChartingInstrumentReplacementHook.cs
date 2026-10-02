using UnityEngine;

/// <summary>Small town/debug adapter. Final NPC dialogue can invoke these same authority calls.</summary>
[DisallowMultipleComponent]
public sealed class ChartingInstrumentReplacementHook : MonoBehaviour
{
    [SerializeField] private Boat boat;
    [SerializeField] private ItemDefinition instrumentDefinition;
    [Tooltip("Place this point above a boat deck. Replacement starts loose, not deployed.")]
    [SerializeField] private Transform issuePoint;

    private Boat Boat => boat != null ? boat : GetComponentInParent<Boat>();
    public bool HasValidInstrument => ChartingInstrumentReplacement.HasValidPersistedInstrument(Boat);
    public bool TryIssueReplacement(out WorldItem replacement, out string error) =>
        ChartingInstrumentReplacement.TryIssueReplacement(Boat, instrumentDefinition,
            issuePoint != null ? (Vector2)issuePoint.position : (Vector2)transform.position, out replacement, out error);
    public bool ForceReplace(out WorldItem replacement, out string error) =>
        ChartingInstrumentReplacement.ForceReplaceChartingInstrument(Boat, instrumentDefinition,
            issuePoint != null ? (Vector2)issuePoint.position : (Vector2)transform.position, out replacement, out error);

    [ContextMenu("Charting Instrument / Issue Missing Instrument (Play Mode)")]
    private void DebugIssue()
    {
        if (!Application.isPlaying) return;
        if (TryIssueReplacement(out _, out string error)) GameMessageService.PostInfo("Charting instrument issued.");
        else GameMessageService.PostWarning(error);
    }
    [ContextMenu("Charting Instrument / FORCE Replace And Discard Pending Observation (Play Mode)")]
    private void DebugForceReplace()
    {
        if (!Application.isPlaying) return;
        if (ForceReplace(out _, out string error)) GameMessageService.PostInfo("Charting instrument replaced. Previous observation discarded.");
        else GameMessageService.PostWarning(error);
    }
}
