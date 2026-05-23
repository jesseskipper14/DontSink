using System;
using UnityEngine;

public static class RuntimeDebugOverlayGUI
{
    public static Rect DrawWindow(
        int windowId,
        Rect rect,
        string title,
        Action drawContents,
        bool draggable = true)
    {
        return GUI.Window(
            windowId,
            rect,
            id =>
            {
                drawContents?.Invoke();

                if (draggable)
                    GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
            },
            title);
    }

    public static bool IsScreenMouseOverGUIRect(Rect guiRect)
    {
        Vector2 guiMouse = new Vector2(
            Input.mousePosition.x,
            Screen.height - Input.mousePosition.y);

        return guiRect.Contains(guiMouse);
    }
}