using Uncreated.ZoneEditor.Objects;

namespace Uncreated.ZoneEditor.Data;
public class ZoneAnchor
{
    public ZoneInfo Zone { get; }
    public int Index { get; internal set; }
    public Vector3 Position { get; internal set; }
    public NetId NetId { get; set; }
    public ZoneAnchorComponent? Component { get; internal set; }
    public ZoneAnchor(ZoneInfo zone, int index)
    {
        Zone = zone;
        Index = index;
    }
}
