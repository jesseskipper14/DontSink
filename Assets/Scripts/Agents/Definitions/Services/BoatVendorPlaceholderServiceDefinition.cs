using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Services/Boat Vendor Placeholder Service")]
public sealed class BoatVendorPlaceholderServiceDefinition : AgentServiceDefinition
{
    [TextArea(2, 6)]
    [SerializeField]
    private string placeholderMessage =
        "Boat sales are coming later. For now, please admire these imaginary vessels.";

    [SerializeField]
    private string[] fakeBoatNames =
    {
        "Dinghy - unavailable",
        "Skiff - unavailable",
        "Ugly Little Coffin With Oars - unavailable"
    };

    public override AgentServiceKind Kind => AgentServiceKind.BoatVendorPlaceholder;

    public string PlaceholderMessage => placeholderMessage;
    public string[] FakeBoatNames => fakeBoatNames;

    public override AgentServiceResult Run(AgentServiceContext context)
    {
        if (AgentServiceDispatcher.TryDispatch(context, out AgentServiceResult result))
            return result;

        Debug.Log($"{context.Agent.DisplayName}: {placeholderMessage}\n- {string.Join("\n- ", fakeBoatNames)}", context.Agent);
        return AgentServiceResult.Handled(placeholderMessage);
    }
}