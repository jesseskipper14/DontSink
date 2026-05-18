using System.Collections.Generic;

public interface IInteractPromptActionProvider
{
    void GetPromptActions(in InteractContext context, List<PromptAction> actions);
}