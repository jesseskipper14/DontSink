using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Services/Surveyor Service")]
public sealed class SurveyorServiceDefinition : AgentServiceDefinition
{
    public override AgentServiceKind Kind => AgentServiceKind.Surveyor;

}
