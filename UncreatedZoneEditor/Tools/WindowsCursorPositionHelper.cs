using System.Runtime.InteropServices;

namespace Uncreated.ZoneEditor.Tools;

internal class WindowsCursorPositionHelper
{
    [DllImport("User32.Dll")]
    public static extern long SetCursorPos(int x, int y);
}