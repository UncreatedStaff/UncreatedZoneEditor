using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
#if CLIENT
using Uncreated.ZoneEditor.Objects;
#endif

namespace Uncreated.ZoneEditor.Data;
public class ZoneInfo
{
    private List<ZoneAnchor>? _anchors;
    public string Name { get; internal set; } = string.Empty;
    public string? ShortName { get; internal set; }
    public CSteamID Creator { get; internal set; }
    public ZoneShape Shape { get; internal set; }
    public Vector3 Center { get; internal set; }
    public Vector3 TemporaryCenter { get; internal set; }
    public Vector3 Spawn { get; internal set; }
    public Vector3 TemporarySpawn { get; internal set; }
    public NetId NetId { get; internal set; }
    public float Height { get; internal set; }
#if CLIENT
    public ZoneComponent? Component { get; internal set; }
    public bool IsSelected => Level.isEditor && ReferenceEquals(EditorZones.Instance.SelectedZone, this);
#endif
    public IReadOnlyList<ZoneAnchor> Anchors { get; private set; } = Array.Empty<ZoneAnchor>();
    internal void AddAnchorIntl(ZoneAnchor anchor, int index, bool createObject = true)
    {
        ThreadUtil.assertIsGameThread();

        anchor.TemporaryPosition = anchor.Position;
        if (_anchors == null)
        {
            _anchors = new List<ZoneAnchor>(8);
            Anchors = new ReadOnlyCollection<ZoneAnchor>(_anchors);
        }

        _anchors.Insert(index, anchor);

#if CLIENT

        if (!createObject || !Level.isEditor)
        {
            return;
        }

        if (Component == null)
        {
            GameObject mainGameObject = new GameObject(Name);
            Component = mainGameObject.AddComponent<ZoneComponent>();
            Component.Init(this);
        }

        if (anchor.Component == null)
        {
            GameObject gameObject = new GameObject($"{Name}[{index}]")
            {
                transform = { parent = Component.transform }
            };
            anchor.Component = gameObject.AddComponent<ZoneAnchorComponent>();
        }

        anchor.Component.Init(anchor);
#endif
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
