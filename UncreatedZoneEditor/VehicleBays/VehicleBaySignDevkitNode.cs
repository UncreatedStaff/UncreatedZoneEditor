using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.IO.FormattedFiles;
using System;
using TMPro;

namespace Uncreated.ZoneEditor.VehicleBays;

public class VehicleBaySignDevkitNode : TempNodeBase, IDevkitSelectionTransformableHandler
{
    private const string MissingIdText = "<#ff9f80>Missing ID</color>";

    private GameObject? _childObject;
    private Collider _collider = null!;

    private TextMeshPro? _tmp;

    private static readonly Collider[] NearbyVehicleBays = new Collider[16];

    public ulong Creator { get; set; }

    public event Action? OnLinkedIdChanged;

    public string? VehicleBayId
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
                return;

            field = value;
            if (_tmp != null)
            {
                _tmp.text = string.IsNullOrEmpty(value)
                    ? MissingIdText
                    : $"<#80ff80>{value}</color>";
            }

            OnLinkedIdChanged?.Invoke();
        }
    }

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
        VehicleBayId = reader.readValue("VehicleBayId");
        if (string.IsNullOrEmpty(VehicleBayId))
            VehicleBayId = null;
    }

    protected override void writeHierarchyItem(IFormattedFileWriter writer)
    {
        base.writeHierarchyItem(writer);
        writer.writeValue("Creator", Creator);

        if (!string.IsNullOrEmpty(VehicleBayId))
            writer.writeValue("VehicleBayId", VehicleBayId);
    }

    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        VehicleBaySignDevkitNodeSystem.Get().isDirty = true;
    }

    [UsedImplicitly]
    private void Awake()
    {
        Creator = Provider.client.m_SteamID;
        VehicleBayId = null;

        name = "Vehicle Bay Sign";
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

            GameObject? loadedPrefab = UncreatedZoneEditor.Instance.VehicleBundle.load<GameObject>("VehicleBaySign.prefab");
            if (loadedPrefab == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find VehicleBaySign.prefab in bundle.");
                return;
            }

            _childObject = Instantiate(loadedPrefab, transform, true);
            _childObject.gameObject.SetLayerRecursively(LayerMasks.BARRICADE);
            _childObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
            BoxCollider collider = _childObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find VehicleBaySign.prefab collider.");
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
            _tmp = _childObject.GetComponentInChildren<TextMeshPro>();
            if (_tmp != null)
                _tmp.text = MissingIdText;
        }
        catch (Exception ex)
        {
            UncreatedZoneEditor.Instance.LogError(ex, "Failed to make vehicle bay sign object.");
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
        // set vehicle bay ID to nearest bay if not set
        if (string.IsNullOrEmpty(VehicleBayId))
        {
            Vector3 position = transform.position;

            const float range = 15f;

            int ct = Physics.OverlapSphereNonAlloc(position, range, NearbyVehicleBays, 1 << 30, QueryTriggerInteraction.Ignore);
            VehicleBayDevkitNode? nearest = null;
            float distance = range + 1f;
            for (int i = 0; i < ct; ++i)
            {
                Transform transform = NearbyVehicleBays[i].transform;
                if (!transform.TryGetComponent(out VehicleBayDevkitNode node) || node.Id == null)
                    continue;

                float d = (position - transform.position).sqrMagnitude;
                if (d >= distance)
                    continue;

                nearest = node;
                distance = d;
            }

            Array.Clear(NearbyVehicleBays, 0, ct);

            if (nearest != null)
            {
                VehicleBayId = nearest.Id;
                VehicleBayDevkitNodeSystem.Get().isDirty = true;
            }
        }

        VehicleBaySignDevkitNodeSystem.Get().AddNode(this);
    }

    [UsedImplicitly]
    private void OnDisable()
    {
        VehicleBaySignDevkitNodeSystem.Get().RemoveNode(this);
    }

    private class Menu : SleekWrapper
    {
        private readonly VehicleBaySignDevkitNode _node;
        private readonly ISleekField _id;

        public Menu(VehicleBaySignDevkitNode node)
        {
            _node = node;
            SizeOffset_X = 400f;

            _id = Glazier.Get().CreateStringField();
            _id.PositionOffset_Y = -30f;
            _id.SizeOffset_X = 200f;
            _id.SizeOffset_Y = 30f;
            _id.AddLabel("Linked ID (mid-click)", ESleekSide.RIGHT);
            _id.OnTextChanged += HandleIdChanged;
            UpdateId();

            AddChild(_id);

            node.OnLinkedIdChanged += UpdateId;
        }

        private void HandleIdChanged(ISleekField field, string text)
        {
            _node.VehicleBayId = string.IsNullOrEmpty(text) ? null : text;
            VehicleBayDevkitNodeSystem.Get().isDirty = true;
        }

        private void UpdateId()
        {
            _id.Text = _node.VehicleBayId ?? string.Empty;
        }

        public override void OnDestroy()
        {
            _node.OnLinkedIdChanged -= UpdateId;
            base.OnDestroy();
        }
    }
}