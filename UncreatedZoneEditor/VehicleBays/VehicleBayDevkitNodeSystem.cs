using DanielWillett.UITools;
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Uncreated.ZoneEditor.Nodes;

namespace Uncreated.ZoneEditor.VehicleBays;

public class VehicleBayDevkitNodeSystem : TempNodeSystemBase, IDisposable, IDirtyable
{
    private static VehicleBayDevkitNodeSystem? _instance;
    private readonly List<VehicleBayDevkitNode> _allNodes;

    private const float VehicleSpawnOffset = 5f;

    private string? _filePath;

    public bool IsActive => UnturnedUIToolsNexus.UIExtensionManager.GetInstance<EditorEnvironmentNodesUIExtension>()?.IsVehicleBayActive is true;

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
                UncreatedZoneEditor.Instance.LogDebug(nameof(VehicleBayDevkitNodeSystem), "Marked dirty.");
            }
            else
                DirtyManager.markClean(this);
        }
    }

    internal VehicleBayDevkitNodeSystem()
    {
        _instance = this;
        _allNodes = new List<VehicleBayDevkitNode>();
        TimeUtility.updated += OnUpdateGizmos;
    }

    internal void Load()
    {
        _filePath = Path.GetFullPath(Level.info.path + "/Uncreated/vehicle_bays.json");
        _allNodes.Clear();
        if (!File.Exists(_filePath))
            return;

        List<VehicleBay> locations;
        try
        {
            using FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024, FileOptions.SequentialScan);
            locations = (List<VehicleBay>?)JsonSerializer.Deserialize(fs, typeof(List<VehicleBay>), VehicleBaySerializeContext.Default) ?? [];
        }
        catch (Exception ex)
        {
            UncreatedZoneEditor.Instance.LogError(ex, $"Error loading vehicle bays file: {_filePath}.");
            return;
        }

        foreach (VehicleBay loc in locations)
        {
            GameObject node = new GameObject("Vehicle Bay", typeof(VehicleBayDevkitNode));
            node.transform.SetPositionAndRotation(
                new Vector3(loc.PositionX, loc.PositionY, loc.PositionZ),
                Quaternion.Euler(loc.RotationX, loc.RotationY, loc.RotationZ)
            );
            VehicleBayDevkitNode nodeComponent = node.GetComponent<VehicleBayDevkitNode>();
            nodeComponent.Creator = loc.Creator;
            nodeComponent.Id = string.IsNullOrEmpty(loc.Id) ? null : loc.Id;
            nodeComponent.Faction = string.IsNullOrEmpty(loc.Faction) ? null : loc.Faction;
            nodeComponent.Vehicle = loc.Vehicle;
        }
    }

    // using Krafts.Publicizer to make these public:
    public override Type GetComponentType() => typeof(VehicleBayDevkitNode);
    public override IEnumerable<GameObject> EnumerateGameObjects() => _allNodes.Select(x => x.gameObject);

    public IReadOnlyList<VehicleBayDevkitNode> GetAllNodes() => _allNodes;

    public static VehicleBayDevkitNodeSystem Get()
    {
        return _instance ?? throw new InvalidOperationException("Not initialized.");
    }

    internal void AddNode(VehicleBayDevkitNode node)
    {
        _allNodes.Add(node);
        isDirty = true;
    }

    internal void RemoveNode(VehicleBayDevkitNode node)
    {
        _allNodes.RemoveFast(node);
        isDirty = true;
    }

    public void save()
    {
        List<VehicleBay> locations = _allNodes.Select(x =>
        {
            Vector3 pos = x.transform.position;
            Vector3 rot = x.transform.eulerAngles;
            return new VehicleBay
            {
                PositionX = pos.x,
                PositionY = pos.y,
                PositionZ = pos.z,
                RotationX = rot.x,
                RotationY = rot.y,
                RotationZ = rot.z,
                Creator = x.Creator,
                Id = string.IsNullOrEmpty(x.Id) ? null : x.Id,
                Faction = string.IsNullOrEmpty(x.Faction) ? null : x.Faction,
                Vehicle = x.Vehicle
            };
        }).ToList();

        string path = _filePath!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        Thread.BeginCriticalRegion();
        try
        {
            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1024, FileOptions.SequentialScan);
            JsonSerializer.Serialize(fs, locations, typeof(List<VehicleBay>), VehicleBaySerializeContext.Default);
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
        if (IsActive && Input.GetMouseButtonUp(2) && DevkitSelectionManager.selection.Count == 1)
        {
            DevkitSelection bay = DevkitSelectionManager.selection.First();
            if (bay.transform.root.TryGetComponent(out VehicleBayDevkitNode node))
            {
                if (Physics.Raycast(EditorInteractEx.Ray, out RaycastHit hit, 8192f, 1 << 30, QueryTriggerInteraction.Collide)
                    && hit.transform.root.TryGetComponent(out VehicleBaySignDevkitNode sign)
                    && !string.IsNullOrEmpty(node.Id))
                {
                    sign.VehicleBayId = node.Id;
                    EditorMessage.SendEditorMessage(UncreatedZoneEditor.Instance.Translations.Translate("SignLinked", node.Id));
                }
                else
                {
                    EditorMessage.SendEditorMessage(
                        UncreatedZoneEditor.Instance.Translations.Translate(
                            string.IsNullOrEmpty(node.Id)
                                ? "BayLinkNoID"
                                : "BayLinkNoTarget"
                        )
                    );
                }
            }
        }

        RuntimeGizmos gizmos = RuntimeGizmos.Get();

        Vector3 box1Center = new Vector3(0f, -0.8007679f, 0f);
        Vector3 box1Size = new Vector3(2f, 2f, 3f);

        foreach (VehicleBayDevkitNode allNode in _allNodes)
        {
            Matrix4x4 matrix = allNode.transform.localToWorldMatrix;

            Color color = allNode.isSelected ? new Color32(26, 255, 255, 255) : new Color32(204, 255, 255, 255);

            gizmos.Box(matrix, box1Center, box1Size, color);

            if (!allNode.isSelected)
            {
                if (allNode.PreviewVehicle != null)
                {
                    allNode.DestroyPreviewVehicle();
                }

                continue;
            }

            Provider._isServer = true;
            try
            {
                if (allNode.PreviewVehicle == null)
                {
                    SpawnVehiclePreview(allNode);
                }
                else
                {
                    if (!allNode.HasUpdatedPhysics)
                    {
                        allNode.PreviewVehicle.updatePhysics();
                        allNode.HasUpdatedPhysics = true;
                    }

                    if (allNode.PreviewVehicle.hasUnityCalledStart)
                        allNode.PreviewVehicle.OnUpdate(Time.deltaTime);
                }
            }
            finally
            {
                Provider._isServer = false;
            }
        }
    }

    private static void SpawnVehiclePreview(VehicleBayDevkitNode node)
    {
        // spawns a fake vehicle that isn't added to VehicleManager and can be destroyed easily

        CachingAssetRef reference = node.VehicleReference;

        VehicleAsset? asset = reference.Get<VehicleAsset>();
        if (asset == null)
            return;

        GameObject? model = asset.GetOrLoadModel();
        if (model == null)
            return;

        node.transform.GetPositionAndRotation(out Vector3 spawnPosition, out Quaternion spawnRotation);
        spawnPosition += Vector3.up * VehicleSpawnOffset;


        GameObject vehicle = Object.Instantiate(model, spawnPosition, spawnRotation);

        Transform vehicleTransform = vehicle.transform;

        Rigidbody rb = vehicleTransform.GetOrAddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;

        InteractableVehicle tempVehicle = vehicle.AddComponent<InteractableVehicle>();
        tempVehicle.instanceID = uint.MaxValue;
        tempVehicle.id = asset.id;
        tempVehicle.fuel = asset.fuel;
        tempVehicle.batteryCharge = 10000;
        tempVehicle.health = asset.health;
        Color32? paintColor = asset.GetRandomDefaultPaintColor();
        if (paintColor.HasValue)
            tempVehicle.ReceivePaintColor(paintColor.Value);

        tempVehicle.init(asset);
        tempVehicle.tellHeadlights(false);
        tempVehicle.tellTaillights(false);
        tempVehicle.gatherVehicleColliders();
        tempVehicle.tireAliveMask = byte.MaxValue;

        node.PreviewVehicle = tempVehicle;
    }

    internal void ResetVehiclePreviewPosition(VehicleBayDevkitNode node)
    {
        node.transform.GetPositionAndRotation(out Vector3 spawnPosition, out Quaternion spawnRotation);
        spawnPosition += Vector3.up * VehicleSpawnOffset;

        // node.PreviewVehicle!.tellState(spawnPosition, spawnRotation, 0f, 0f, 0f, 0f);
        node.PreviewVehicle!.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
    }
}