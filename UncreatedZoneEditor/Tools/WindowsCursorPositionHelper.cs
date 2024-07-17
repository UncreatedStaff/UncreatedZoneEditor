using System;
using System.Runtime.InteropServices;

namespace Uncreated.ZoneEditor.Tools;

internal class WindowsCursorPositionHelper
{
    [DllImport("User32.Dll")]
    private static extern long SetCursorPos(int x, int y);


    public static void SetCursorPosition(in Vector2 position)
    {
        int w = Screen.width, h = Screen.height;
        
        SetCursorPos(
            Mathf.RoundToInt(Math.Clamp(position.x, 0f, w)),
            Mathf.RoundToInt(h - Math.Clamp(position.y, 0f, h))
        );
    }
}