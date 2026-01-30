using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.IO.FormattedFiles;
using System;

namespace Uncreated.ZoneEditor.Caches;

public class CacheDevkitNode : TempNodeBase, IDevkitSelectionTransformableHandler
{
    private GameObject? _childObject;
    private Collider _collider = null!;

    public ulong Creator { get; set; }

    public bool IsEnabled { get; set; }

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
        IsEnabled = !reader.readValue<bool>("IsDisabled");
    }

    protected override void writeHierarchyItem(IFormattedFileWriter writer)
    {
        base.writeHierarchyItem(writer);
        writer.writeValue("Creator", Creator);
        writer.writeValue<bool>("IsDisabled", !IsEnabled);
    }

    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        CacheDevkitNodeSystem.Get().isDirty = true;
    }

    [UsedImplicitly]
    private void Awake()
    {
        Creator = Provider.client.m_SteamID;
        IsEnabled = true;
        name = "Cache";
        gameObject.tag = "Logic";
        gameObject.layer = 30;
        if (!Level.isEditor)
            return;

        try
        {
            if (UncreatedZoneEditor.Instance.CacheBundle == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find Cache bundle.");
                return;
            }

            GameObject? loadedCache = UncreatedZoneEditor.Instance.CacheBundle.load<GameObject>("Cache.prefab");
            if (loadedCache == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find Cache.prefab in bundle.");
                return;
            }

            _childObject = Instantiate(loadedCache, transform, true);
            _childObject.gameObject.SetLayerRecursively(LayerMasks.BARRICADE);
            _childObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
            BoxCollider collider = _childObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find Cache.prefab collider.");
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
            UncreatedZoneEditor.Instance.LogError(ex, "Failed to make cache object.");
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
        CacheDevkitNodeSystem.Get().AddNode(this);
    }

    [UsedImplicitly]
    private void OnDisable()
    {
        CacheDevkitNodeSystem.Get().RemoveNode(this);
    }

    private class Menu : SleekWrapper
    {
        private readonly CacheDevkitNode _node;

        public Menu(CacheDevkitNode node)
        {
            _node = node;
            SizeOffset_X = 400f;

            ISleekToggle toggle = Glazier.Get().CreateToggle();
            toggle.PositionOffset_Y = -30f;
            toggle.SizeOffset_X = 40f;
            toggle.SizeOffset_Y = 40f;
            toggle.Value = node.IsEnabled;
            toggle.AddLabel("Enabled", ESleekSide.RIGHT);
            toggle.OnValueChanged += OnToggled;

            AddChild(toggle);
        }

        private void OnToggled(ISleekToggle toggle, bool state)
        {
            _node.IsEnabled = state;
            CacheDevkitNodeSystem.Get().isDirty = true;
        }
    }
}