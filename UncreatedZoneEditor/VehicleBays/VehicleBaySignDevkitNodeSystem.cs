using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using DanielWillett.UITools;
using Uncreated.ZoneEditor.Nodes;

namespace Uncreated.ZoneEditor.VehicleBays;

public class VehicleBaySignDevkitNodeSystem : TempNodeSystemBase, IDisposable, IDirtyable
{
    private static VehicleBaySignDevkitNodeSystem? _instance;
    private readonly List<VehicleBaySignDevkitNode> _allNodes;

    private string? _filePath;

    public bool IsActive => UnturnedUIToolsNexus.UIExtensionManager.GetInstance<EditorEnvironmentNodesUIExtension>()?.IsVehicleBaySignActive is true;

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
                UncreatedZoneEditor.Instance.LogDebug(nameof(VehicleBaySignDevkitNodeSystem), "Marked dirty.");
            }
            else
                DirtyManager.markClean(this);
        }
    }

    internal VehicleBaySignDevkitNodeSystem()
    {
        _instance = this;
        _allNodes = new List<VehicleBaySignDevkitNode>();
        TimeUtility.updated += OnUpdateGizmos;
    }

    internal void Load()
    {
        _filePath = Path.GetFullPath(Level.info.path + "/Uncreated/vehicle_bay_signs.json");
        _allNodes.Clear();
        if (!File.Exists(_filePath))
            return;

        List<VehicleBaySign> locations;
        try
        {
            using FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024, FileOptions.SequentialScan);
            locations = (List<VehicleBaySign>?)JsonSerializer.Deserialize(fs, typeof(List<VehicleBaySign>), VehicleBaySignSerializeContext.Default) ?? [ ];
        }
        catch (Exception ex)
        {
            UncreatedZoneEditor.Instance.LogError(ex, $"Error loading vehicle bay signs file: {_filePath}.");
            return;
        }

        foreach (VehicleBaySign loc in locations)
        {
            GameObject node = new GameObject("Vehicle Bay Sign", typeof(VehicleBaySignDevkitNode));
            node.transform.SetPositionAndRotation(
                new Vector3(loc.PositionX, loc.PositionY, loc.PositionZ),
                Quaternion.Euler(loc.RotationX, loc.RotationY, loc.RotationZ)
            );
            VehicleBaySignDevkitNode nodeComponent = node.GetComponent<VehicleBaySignDevkitNode>();
            nodeComponent.Creator = loc.Creator;
            nodeComponent.VehicleBayId = string.IsNullOrEmpty(loc.VehicleBayId) ? null : loc.VehicleBayId;
        }
    }

    // using Krafts.Publicizer to make these public:
    public override Type GetComponentType() => typeof(VehicleBaySignDevkitNode);
    public override IEnumerable<GameObject> EnumerateGameObjects() => _allNodes.Select(x => x.gameObject);

    public IReadOnlyList<VehicleBaySignDevkitNode> GetAllNodes() => _allNodes;

    public static VehicleBaySignDevkitNodeSystem Get()
    {
        return _instance ?? throw new InvalidOperationException("Not initialized.");
    }

    internal void AddNode(VehicleBaySignDevkitNode node)
    {
        _allNodes.Add(node);
        isDirty = true;
    }

    internal void RemoveNode(VehicleBaySignDevkitNode node)
    {
        _allNodes.RemoveFast(node);
        isDirty = true;
    }

    public void save()
    {
        List<VehicleBaySign> locations = _allNodes.Select(x =>
        {
            Vector3 pos = x.transform.position;
            Vector3 rot = x.transform.eulerAngles;
            return new VehicleBaySign
            {
                PositionX = pos.x,
                PositionY = pos.y,
                PositionZ = pos.z,
                RotationX = rot.x,
                RotationY = rot.y,
                RotationZ = rot.z,
                Creator = x.Creator,
                VehicleBayId = string.IsNullOrEmpty(x.VehicleBayId) ? null : x.VehicleBayId
            };
        }).ToList();

        string path = _filePath!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        Thread.BeginCriticalRegion();
        try
        {
            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1024, FileOptions.SequentialScan);
            JsonSerializer.Serialize(fs, locations, typeof(List<VehicleBaySign>), VehicleBaySignSerializeContext.Default);
        }
        finally
        {
            Thread.EndCriticalRegion();
        }
    }

    public void Dispose()
    {
        TimeUtility.updated -= OnUpdateGizmos;
    }

    private void OnUpdateGizmos()
    {
        if (!SpawnpointSystemV2.Get().IsVisible || !Level.isEditor || Level.isLoading)
            return;

        // middle click to link sign
        if (IsActive && Input.GetMouseButtonUp(2) && DevkitSelectionManager.selection.Count > 0)
        {
            foreach (DevkitSelection sign in DevkitSelectionManager.selection)
            {
                if (!sign.transform.root.TryGetComponent(out VehicleBaySignDevkitNode node))
                    continue;

                VehicleBayDevkitNode? bay = null;
                if (Physics.Raycast(EditorInteractEx.Ray, out RaycastHit hit, 8192f, 1 << 30, QueryTriggerInteraction.Collide)
                    && hit.transform.root.TryGetComponent(out bay)
                    && !string.IsNullOrEmpty(bay!.Id))
                {
                    node.VehicleBayId = bay.Id;
                    isDirty = true;
                    EditorMessage.SendEditorMessage(UncreatedZoneEditor.Instance.Translations.Translate("SignLinked", bay.Id));
                }
                else if (node.VehicleBayId != null)
                {
                    EditorMessage.SendEditorMessage(UncreatedZoneEditor.Instance.Translations.Translate("SignUnlinked", node.VehicleBayId));
                    node.VehicleBayId = null;
                    isDirty = true;
                }
                else if (node != null)
                {
                    EditorMessage.SendEditorMessage(
                        UncreatedZoneEditor.Instance.Translations.Translate(
                            bay != null
                                ? "SignAlreadyUnlinkedHit"
                                : "SignAlreadyUnlinked"
                        )
                    );
                }
            }
        }

        RuntimeGizmos gizmos = RuntimeGizmos.Get();

        Vector3 box1Center = new Vector3(0f, 1.325f, 0.1052027f);
        Vector3 box1Size = new Vector3(2f, 1.5f, 0.2036657f);

        foreach (VehicleBaySignDevkitNode allNode in _allNodes)
        {
            Matrix4x4 matrix = allNode.transform.localToWorldMatrix;

            Color color = allNode.isSelected ? new Color32(51, 204, 204, 255) : new Color32(153, 230, 230, 255);

            gizmos.Box(matrix, box1Center, box1Size, color);
        }
    }
}