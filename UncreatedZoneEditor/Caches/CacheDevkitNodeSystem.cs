using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Uncreated.ZoneEditor.Caches;

public class CacheDevkitNodeSystem : TempNodeSystemBase, IDisposable, IDirtyable
{
    private static CacheDevkitNodeSystem _instance;
    private readonly List<CacheDevkitNode> _allNodes;

    private string? _filePath;

    /// <inheritdoc />
    public bool isDirty
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            if (value)
            {
                DirtyManager.markDirty(this);
                UncreatedZoneEditor.Instance.LogDebug(nameof(CacheDevkitNodeSystem), "Marked dirty.");
            }
            else
                DirtyManager.markClean(this);
        }
    }

    internal CacheDevkitNodeSystem()
    {
        _instance = this;
        _allNodes = new List<CacheDevkitNode>();
        TimeUtility.updated += OnUpdateGizmos;
    }

    internal void Load()
    {
        _filePath = Path.GetFullPath(Level.info.path + "/Uncreated/caches.json");
        _allNodes.Clear();
        if (!File.Exists(_filePath))
            return;

        List<CacheLocation> locations;
        try
        {
            using FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024, FileOptions.SequentialScan);
            locations = (List<CacheLocation>)JsonSerializer.Deserialize(fs, typeof(List<CacheLocation>), CacheLocationSerializeContext.Default);
        }
        catch (Exception ex)
        {
            UncreatedZoneEditor.Instance.LogError(ex, $"Error loading cache file: {_filePath}.");
            return;
        }

        foreach (CacheLocation loc in locations)
        {
            GameObject node = new GameObject("Cache", typeof(CacheDevkitNode));
            node.transform.SetPositionAndRotation(
                new Vector3(loc.PositionX, loc.PositionY, loc.PositionZ),
                Quaternion.Euler(loc.RotationX, loc.RotationY, loc.RotationZ)
            );
            node.GetComponent<CacheDevkitNode>().Creator = loc.Creator;
        }
    }

    // using Krafts.Publicizer to make these public:
    public override Type GetComponentType() => typeof(CacheDevkitNode);
    public override IEnumerable<GameObject> EnumerateGameObjects() => _allNodes.Select(x => x.gameObject);

    public IReadOnlyList<CacheDevkitNode> GetAllNodes() => _allNodes;

    public static CacheDevkitNodeSystem Get()
    {
        return _instance;
    }

    internal void AddNode(CacheDevkitNode node)
    {
        _allNodes.Add(node);
        isDirty = true;
    }

    internal void RemoveNode(CacheDevkitNode node)
    {
        _allNodes.RemoveFast(node);
        isDirty = true;
    }

    public void save()
    {
        List<CacheLocation> locations = _allNodes.Select(x =>
        {
            Vector3 pos = x.transform.position;
            Vector3 rot = x.transform.eulerAngles;
            return new CacheLocation
            {
                PositionX = pos.x,
                PositionY = pos.y,
                PositionZ = pos.z,
                RotationX = rot.x,
                RotationY = rot.y,
                RotationZ = rot.z,
                Creator = x.Creator
            };
        }).ToList();

        string path = _filePath!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1024, FileOptions.SequentialScan);
        JsonSerializer.Serialize(fs, locations, typeof(List<CacheLocation>), CacheLocationSerializeContext.Default);
    }

    public void Dispose()
    {
        TimeUtility.updated -= OnUpdateGizmos;
    }

    private void OnUpdateGizmos()
    {
        if (!SpawnpointSystemV2.Get().IsVisible || !Level.isEditor)
            return;

        foreach (CacheDevkitNode allNode in _allNodes)
        {
            Color color = allNode.isSelected ? Color.cyan : Color.magenta;
            Vector3 pos = allNode.transform.position;
            Quaternion rot = allNode.transform.rotation;
            RuntimeGizmos.Get().Box(pos + new Vector3(0.01125595f, 0.5144702f, 0f), rot, new Vector3(1.920226f, 1.070658f, 1.641956f), color);
            RuntimeGizmos.Get().Box(pos + new Vector3(0.01125595f, 1.295658f, 0f), rot, new Vector3(0.125f, 0.45f, 0.125f), color);
            RuntimeGizmos.Get().Box(pos + new Vector3(0.01125595f, 1.583158f, 0f), rot, new Vector3(0.65f, 0.125f, 0.25f), color);
        }
    }
}