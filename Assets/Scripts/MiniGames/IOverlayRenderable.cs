using UnityEngine;

namespace MiniGames
{
    /// <summary>
    /// Optional UI hook for cartridges that want to render interactive overlay UI.
    /// This does not grant authority; it is purely presentation + input handling inside the overlay.
    /// World mutations still happen via emitted effects.
    /// </summary>
    public interface IOverlayRenderable
    {
        void DrawOverlayGUI(Rect panel);
    }
}
