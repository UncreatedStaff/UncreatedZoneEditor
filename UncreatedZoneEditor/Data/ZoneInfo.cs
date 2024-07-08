using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Uncreated.ZoneEditor.Data;
public class ZoneInfo
{
    private List<ZoneAnchor>? _anchors;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public CSteamID Creator { get; set; }
    public ZoneShape Shape { get; set; }
    public Vector3 Center { get; set; }
    public Vector3 Spawn { get; set; }
    public NetId NetId { get; set; }
    public IReadOnlyList<ZoneAnchor> Anchors { get; private set; } = Array.Empty<ZoneAnchor>();
    internal void AddAnchorIntl(ZoneAnchor anchor, int index)
    {
        if (_anchors == null)
        {
            _anchors = new List<ZoneAnchor>(8);
            Anchors = new ReadOnlyCollection<ZoneAnchor>(_anchors);
        }

        _anchors.Insert(index, anchor);
    }
    internal void RemoveAnchorIntl(int index)
    {
        if (_anchors != null)
            _anchors.RemoveAt(index);
    }
#if CLIENT
    internal void SortAnchorsByOrderIntl(uint[] order)
    {
        if (_anchors == null || order.Length == 0)
            return;

        ZoneAnchor[] oldAnchors = _anchors.ToArray();
        _anchors.Clear();
        
        if (_anchors.Capacity < order.Length)
            _anchors.Capacity = order.Length;

        for (int i = 0; i < order.Length; ++i)
        {
            uint netId = order[i];

            if (netId == 0u)
                continue;

            int ind = Array.FindIndex(oldAnchors, x => x.NetId.id == netId);
            if (ind >= 0)
            {
                _anchors.Add(oldAnchors[ind]);
            }
        }
    }
#endif
}
