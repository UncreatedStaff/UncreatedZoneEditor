#if CLIENT
using Uncreated.ZoneEditor.Objects;
#endif

namespace Uncreated.ZoneEditor.Data;
public class ZoneAnchor
{
    public ZoneInfo Zone { get; }
    public int Index { get; internal set; }
    public Vector3 Position { get; internal set; }
    public Vector3 TemporaryPosition { get; internal set; }
    public NetId NetId { get; set; }
#if CLIENT
    public ZoneAnchorComponent? Component { get; internal set; }
#endif
    public ZoneAnchor(ZoneInfo zone, int index)
    {
        Zone = zone;
        Index = index;
    }
}
