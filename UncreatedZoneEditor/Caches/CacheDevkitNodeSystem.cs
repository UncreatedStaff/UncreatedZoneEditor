using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DanielWillett.UITools;

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
            CacheDevkitNode nodeComponent = node.GetComponent<CacheDevkitNode>();
            nodeComponent.Creator = loc.Creator;
            nodeComponent.IsEnabled = !loc.IsDisabled;
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
                Creator = x.Creator,
                IsDisabled = !x.IsEnabled
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
        if (!SpawnpointSystemV2.Get().IsVisible || !Level.isEditor || Level.isLoading)
            return;

        bool isActive = UnturnedUIToolsNexus.UIExtensionManager.GetInstance<EditorEnvironmentNodesUIExtension>() is { IsActive: true };

        RuntimeGizmos gizmos = RuntimeGizmos.Get();

        Vector3 spawnPoint1 = new Vector3(-0.325f, 0f, -1.6f - 0.325f);
        Vector3 spawnPoint2 = new Vector3(+0.325f, 0f, -1.6f + 0.325f);
        Vector3 spawnPoint3 = new Vector3(spawnPoint1.x, spawnPoint1.y, spawnPoint2.z);
        Vector3 spawnPoint4 = new Vector3(spawnPoint2.x, spawnPoint2.y, spawnPoint1.z);
        Vector3 box1Center = new Vector3(0.01125595f, 0.5144702f, 0f);
        Vector3 box1Size = new Vector3(1.920226f, 1.070658f, 1.641956f);
        Vector3 box2Center = new Vector3(0.01125595f, 1.295658f, 0f);
        Vector3 box2Size = new Vector3(0.125f, 0.45f, 0.125f);
        Vector3 box3Center = new Vector3(0.01125595f, 1.583158f, 0f);
        Vector3 box3Size = new Vector3(0.65f, 0.125f, 0.25f);

        foreach (CacheDevkitNode allNode in _allNodes)
        {
            Matrix4x4 matrix = allNode.transform.localToWorldMatrix;

            Color color;
            if (allNode.IsEnabled)
                color = allNode.isSelected ? Color.cyan : Color.magenta;
            else
                color = allNode.isSelected ? new Color32(255, 153, 51, 255) : new Color32(255, 153, 102, 255);

            gizmos.Box(matrix, box1Center, box1Size, color);
            gizmos.Box(matrix, box2Center, box2Size, color);
            gizmos.Box(matrix, box3Center, box3Size, color);

            if (!isActive)
                continue;

            // spawn X symbol
            Vector3 spawnPoint = matrix.MultiplyPoint3x4((spawnPoint1 + spawnPoint2) / 2f);

            if (!PlayerStance.hasHeightClearanceAtPosition(spawnPoint, PlayerMovement.HEIGHT_STAND + 0.5f))
            {
                color = Color.red;
            }

            gizmos.Line(matrix.MultiplyPoint3x4(spawnPoint1), matrix.MultiplyPoint3x4(spawnPoint2), color);
            gizmos.Line(matrix.MultiplyPoint3x4(spawnPoint3), matrix.MultiplyPoint3x4(spawnPoint4), color);
        }
    }
}