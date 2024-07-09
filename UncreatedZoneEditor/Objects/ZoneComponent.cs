#if CLIENT
using SDG.Framework.Devkit.Interactable;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor.Objects;
public class ZoneComponent : MonoBehaviour, IDevkitInteractableBeginSelectionHandler, IDevkitSelectionTransformableHandler, IDevkitInteractableEndSelectionHandler
{
#nullable disable

    public ZoneInfo Zone { get; private set; }

    public Collider Collider { get; private set; }
    public Collider SpawnCollider { get; private set; }

    public bool IsSpawnSelected { get; private set; }

#nullable restore

    internal void Init(ZoneInfo zone)
    {
        Zone = zone;

        transform.SetPositionAndRotation(zone.Center, Quaternion.identity);
        transform.localScale = Vector3.one;
        gameObject.layer = 3;
        gameObject.tag = "Logic";

        BoxCollider collider = gameObject.GetOrAddComponent<BoxCollider>();
        collider.size = new Vector3(2f, 10f, 2f);
        collider.center = new Vector3(0f, 10f, 0f);
        collider.isTrigger = true;
        Collider = collider;

        if (SpawnCollider == null)
        {
            GameObject spawnObject = new GameObject("SpawnCollider")
            {
                layer = 3,
                tag = "Logic"
            };

            spawnObject.transform.SetParent(transform, false);
            BoxCollider spawnCollider = spawnObject.AddComponent<BoxCollider>();
            spawnCollider.size = new Vector3(2f, 10f, 2f);
            spawnCollider.center = new Vector3(0f, 10f, 0f);
            spawnCollider.isTrigger = true;
            SpawnCollider = spawnCollider;
        }

        SpawnCollider.transform.SetPositionAndRotation(zone.Center, Quaternion.identity);
        SpawnCollider.transform.localScale = Vector3.one;
    }

    //private void Update()
    //{
    //    if (ZoneEditorUI.Instance is not { IsActive: true })
    //        return;
    //
    //    Bounds bounds = Collider.bounds;
    //    RuntimeGizmos.Get().Box(bounds.center, bounds.size, Color.cyan);
    //}
    public void RebuildVisuals()
    {

    }

    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        if (data.collider != Collider && data.collider != SpawnCollider)
            return;

        UncreatedZoneEditor.Instance.LogConditional("Selected zone arrow.");
        if (EditorZones.Instance.RequestSelectZone(Zone, 0))
        {
            IsSpawnSelected = data.collider == SpawnCollider;
            EditorZones.Instance.IsSpawnPositionSelected = IsSpawnSelected;
        }
    }
    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        if (data.collider != Collider && data.collider != SpawnCollider)
            return;

        if (EditorZones.Instance.RequestDeselectZone())
        {
            IsSpawnSelected = false;
        }
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        int index = EditorZones.Instance.ZoneList.IndexOf(Zone);

        if (IsSpawnSelected)
        {
            Zone.Spawn = transform.position;
            Zone.TemporarySpawn = Zone.Spawn;
            transform.position = Zone.Center;
            SpawnCollider.transform.position = Zone.Spawn;
            // todo
            // EditorZones.Instance.MoveAnchor(new ZoneAnchorIdentifier(index, Anchor.Index), transform.position);
            return;
        }

        if (index >= 0)
        {
            Zone.Center = transform.position;
            Zone.TemporaryCenter = Zone.Center;
            SpawnCollider.transform.position = Zone.Spawn;
            foreach (ZoneAnchor anchor in Zone.Anchors)
            {
                if (anchor.Component == null)
                    continue;

                anchor.Position = anchor.Component.transform.position;
                anchor.TemporaryPosition = anchor.Position;
            }
            // todo
            // EditorZones.Instance.MoveAnchor(new ZoneAnchorIdentifier(index, Anchor.Index), transform.position);
        }
    }
}
#endif