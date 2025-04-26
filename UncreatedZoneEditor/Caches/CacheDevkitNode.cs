using System;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.IO.FormattedFiles;

namespace Uncreated.ZoneEditor.Caches;

public class CacheDevkitNode : TempNodeBase, IDevkitSelectionTransformableHandler
{
    private GameObject? _childObject;
    private Collider _collider = null!;

    public ulong Creator { get; set; }

    internal void UpdateEditorVisibility()
    {
        bool isVisible = SpawnpointSystemV2.Get().IsVisible;
        _collider.enabled = isVisible;
        _childObject?.SetActive(isVisible);
    }

    protected override void readHierarchyItem(IFormattedFileReader reader)
    {
        base.readHierarchyItem(reader);
        Creator = reader.readValue<ulong>("Creator");
    }

    protected override void writeHierarchyItem(IFormattedFileWriter writer)
    {
        base.writeHierarchyItem(writer);
        writer.writeValue("Creator", Creator);
    }

    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        CacheDevkitNodeSystem.Get().isDirty = true;
    }

    [UsedImplicitly]
    private void Awake()
    {
        Creator = Provider.client.m_SteamID;
        name = "Cache";
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

            _childObject = Instantiate(loadedCache);
            _childObject.transform.SetParent(transform);
            _childObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _childObject.SetActive(true);
            _collider = _childObject.GetComponent<Collider>();
            if (_collider == null)
            {
                UncreatedZoneEditor.Instance.LogWarning("Unable to find Cache.prefab collider.");
            }
            else
            {
                UncreatedZoneEditor.Instance.LogDebug("Spawned Cache.prefab.");
            }
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
}
