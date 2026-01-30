using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.IO.FormattedFiles;
using System;

namespace Uncreated.ZoneEditor.VehicleBays;

public class VehicleBayDevkitNode : TempNodeBase, IDevkitSelectionTransformableHandler
{
    private GameObject? _childObject;
    private Collider _collider = null!;

    private static Guid _previousVehicle;

    internal CachingAssetRef VehicleReference;

    internal InteractableVehicle? PreviewVehicle;
    internal bool HasUpdatedPhysics;
    internal void DestroyPreviewVehicle()
    {
        HasUpdatedPhysics = false;
        Destroy(PreviewVehicle!.gameObject);
        PreviewVehicle = null;
    }

    public ulong Creator { get; set; }

    public string? Id { get; set; }
    public string? Faction { get; set; }

    public Guid Vehicle
    {
        get => VehicleReference.Guid;
        set
        {
            if (VehicleReference.Guid == value)
                return;

            VehicleReference = new CachingAssetRef(value);
            VehicleChanged?.Invoke();
            if (PreviewVehicle != null)
                DestroyPreviewVehicle();
            PreviewVehicle = null;
        }
    }

    public event Action? VehicleChanged;

    internal void UpdateEditorVisibility()
    {
        bool isVisible = SpawnpointSystemV2.Get().IsVisible;
        _collider.enabled = isVisible;
        _childObject?.SetActive(isVisible);
    }

    public override ISleekElement CreateMenu() => new Menu(this);

    protected override void readHierarchyItem(IFormattedFileReader reader)
    {
        base.readHierarchyItem(reader);

        Creator = reader.readValue<ulong>("Creator");

        Id = reader.readValue("Id");
        if (string.IsNullOrEmpty(Id))
            Id = null;

        Faction = reader.readValue("Faction");
        if (string.IsNullOrEmpty(Faction))
            Faction = null;

        Vehicle = reader.readValue<Guid>("Vehicle");
    }

    protected override void writeHierarchyItem(IFormattedFileWriter writer)
    {
        base.writeHierarchyItem(writer);

        writer.writeValue("Creator", Creator);

        if (!string.IsNullOrEmpty(Id))
            writer.writeValue("Id", Id);

        if (!string.IsNullOrEmpty(Faction))
            writer.writeValue("Faction", Faction);

        writer.writeValue("Vehicle", Vehicle);
    }

    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        VehicleBayDevkitNodeSystem.Get().isDirty = true;
        if (PreviewVehicle != null)
        {
            VehicleBayDevkitNodeSystem.Get().ResetVehiclePreviewPosition(this);
        }
    }

    [UsedImplicitly]
    private void Awake()
    {
        Creator = Provider.client.m_SteamID;
        Id = null;
        Faction = null;
        Vehicle = _previousVehicle;

        name = "Vehicle Bay";
        gameObject.tag = "Logic";
        gameObject.layer = 30;
        gameObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        if (!Level.isEditor)
            return;

        try
        {
            if (UncreatedZoneEditor.Instance.VehicleBundle == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find VehicleBay bundle.");
                return;
            }

            GameObject? loadedPrefab = UncreatedZoneEditor.Instance.VehicleBundle.load<GameObject>("VehicleBay.prefab");
            if (loadedPrefab == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find VehicleBay.prefab in bundle.");
                return;
            }

            _childObject = Instantiate(loadedPrefab, transform, true);
            _childObject.gameObject.SetLayerRecursively(LayerMasks.BARRICADE);
            Quaternion rotation = Quaternion.Euler(-90f, 0f, 0f);
            _childObject.transform.SetLocalPositionAndRotation(Vector3.zero, rotation);
            BoxCollider collider = _childObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find VehicleBay.prefab collider.");
            }
            else
            {
                BoxCollider newCollider = gameObject.AddComponent<BoxCollider>();
                Vector3 center = collider.center;
                Vector3 size = collider.size;
                newCollider.size = new Vector3(size.x, size.z, size.y);
                newCollider.center = new Vector3(center.x, center.z, -center.y);
                newCollider.isTrigger = collider.isTrigger;
                newCollider.sharedMaterial = collider.sharedMaterial;
                Destroy(collider);
                _collider = newCollider;
            }

            _childObject.SetActive(true);
            _childObject.gameObject.SetTagIfUntaggedRecursively("Barricade");
        }
        catch (Exception ex)
        {
            UncreatedZoneEditor.Instance.LogError(ex, "Failed to make vehicle bay object.");
        }
        finally
        {
            if (_collider == null)
            {
                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(1.5f, 1.5f, 1.5f);
                _collider = collider;
            }

            UpdateEditorVisibility();
        }
    }

    [UsedImplicitly]
    private void OnEnable()
    {
        VehicleBayDevkitNodeSystem.Get().AddNode(this);
    }

    [UsedImplicitly]
    private void OnDisable()
    {
        VehicleBayDevkitNodeSystem.Get().RemoveNode(this);
        if (PreviewVehicle != null)
            DestroyPreviewVehicle();

        PreviewVehicle = null;
    }

    private class Menu : SleekWrapper
    {
        private readonly VehicleBayDevkitNode _node;
        private readonly ISleekLabel _resolvedVehicle;
        private readonly ISleekField _vehicleField;

        public Menu(VehicleBayDevkitNode node)
        {
            _node = node;
            SizeOffset_X = 400f;

            ISleekField id = Glazier.Get().CreateStringField();
            id.PositionOffset_Y = -30f;
            id.SizeOffset_X = 200f;
            id.SizeOffset_Y = 30f;
            id.Text = node.Id ?? string.Empty;
            id.AddLabel("ID", ESleekSide.RIGHT);
            id.OnTextChanged += HandleIdChanged;
            id.OnTextSubmitted += HandleIdSubmitted;
            id.OnTextEscaped += HandleIdCancelled;

            AddChild(id);

            ISleekField faction = Glazier.Get().CreateStringField();
            faction.PositionOffset_Y = -70f;
            faction.SizeOffset_X = 200f;
            faction.SizeOffset_Y = 30f;
            faction.Text = node.Faction ?? string.Empty;
            faction.AddLabel("Faction", ESleekSide.RIGHT);
            faction.OnTextChanged += HandleFactionChanged;

            AddChild(faction);

            _vehicleField = Glazier.Get().CreateStringField();
            _vehicleField.PositionOffset_Y = -110f;
            _vehicleField.SizeOffset_X = 200f;
            _vehicleField.SizeOffset_Y = 30f;
            _vehicleField.Text = node.Vehicle.ToString("N");
            _vehicleField.AddLabel("Vehicle", ESleekSide.RIGHT);
            _vehicleField.OnTextSubmitted += HandleGuidSubmitted;

            AddChild(_vehicleField);

            _resolvedVehicle = Glazier.Get().CreateLabel();
            _resolvedVehicle.PositionOffset_Y = -130f;
            _resolvedVehicle.SizeOffset_X = 200f;
            _resolvedVehicle.SizeOffset_Y = 20f;
            _resolvedVehicle.AllowRichText = true;
            _resolvedVehicle.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
            UpdateAsset();

            AddChild(_resolvedVehicle);

            _node.VehicleChanged += HandleVehicleChanged;
        }

        private void UpdateAsset()
        {
            VehicleAsset? asset = _node.VehicleReference.Get<VehicleAsset>();
            if (asset == null)
            {
                _resolvedVehicle.Text = _node.Vehicle == Guid.Empty
                    ? "Missing Vehicle"
                    : "Unknown Vehicle";

                _resolvedVehicle.TextColor = ESleekTint.BAD;
            }
            else
            {
                _resolvedVehicle.Text = asset.vehicleName ?? asset.name;
                _resolvedVehicle.TextColor = new SleekColor(ItemTool.getRarityColorUI(asset.rarity));
            }
        }

        private string? _lastIdWithMatches;

        private void HandleIdCancelled(ISleekField field)
        {
            if (string.IsNullOrEmpty(_lastIdWithMatches))
                return;

            if (!string.Equals(_node.Id, _lastIdWithMatches, StringComparison.Ordinal))
            {
                _node.Id = _lastIdWithMatches;
                VehicleBayDevkitNodeSystem.Get().isDirty = true;
            }

            field.Text = _lastIdWithMatches;
            _lastIdWithMatches = null;
        }
        private void HandleIdSubmitted(ISleekField field)
        {
            if (string.IsNullOrEmpty(_lastIdWithMatches))
                return;

            bool any = false;
            foreach (VehicleBaySignDevkitNode node in VehicleBaySignDevkitNodeSystem.Get().GetAllNodes())
            {
                if (!string.Equals(node.VehicleBayId, _lastIdWithMatches, StringComparison.Ordinal))
                    continue;

                node.VehicleBayId = _node.Id;
                any = true;
            }

            if (any)
            {
                VehicleBaySignDevkitNodeSystem.Get().isDirty = true;
            }

            _lastIdWithMatches = null;
        }

        private void HandleIdChanged(ISleekField field, string text)
        {
            string? oldNodeId = _node.Id;
            string? newNodeId = string.IsNullOrEmpty(text) ? null : text;
            if (string.Equals(oldNodeId, newNodeId, StringComparison.Ordinal))
                return;

            _node.Id = newNodeId;
            VehicleBayDevkitNodeSystem.Get().isDirty = true;
            if (!string.IsNullOrEmpty(_lastIdWithMatches) || string.IsNullOrEmpty(oldNodeId) || string.IsNullOrEmpty(newNodeId))
                return;

            foreach (VehicleBaySignDevkitNode node in VehicleBaySignDevkitNodeSystem.Get().GetAllNodes())
            {
                if (!string.Equals(node.VehicleBayId, oldNodeId, StringComparison.Ordinal))
                    continue;

                _lastIdWithMatches = oldNodeId;
                break;
            }
        }

        private void HandleFactionChanged(ISleekField field, string text)
        {
            _node.Faction = string.IsNullOrEmpty(text) ? null : text;
            VehicleBayDevkitNodeSystem.Get().isDirty = true;
        }

        private void HandleGuidSubmitted(ISleekField field)
        {
            if (!Guid.TryParse(field.Text, out Guid guid))
            {
                field.Text = _node.Vehicle.ToString("N");
                return;
            }

            _previousVehicle = guid;

            _changingVehicle = true;
            try
            {
                _node.Vehicle = guid;
            }
            finally
            {
                _changingVehicle = false;
            }
            VehicleBayDevkitNodeSystem.Get().isDirty = true;
        }

        private bool _changingVehicle;

        private void HandleVehicleChanged()
        {
            UpdateAsset();
            if (_changingVehicle)
                return;

            _vehicleField.Text = _node.Vehicle.ToString("N");
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _node.VehicleChanged -= HandleVehicleChanged;
        }
    }
}