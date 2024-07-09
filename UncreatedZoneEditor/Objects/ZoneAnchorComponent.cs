#if CLIENT
using SDG.Framework.Devkit.Interactable;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor.Objects;
public class ZoneAnchorComponent : MonoBehaviour, IDevkitInteractableBeginSelectionHandler, IDevkitSelectionTransformableHandler, IDevkitInteractableEndSelectionHandler
{
#nullable disable
    
    public ZoneAnchor Anchor { get; private set; }
    
    public Collider Collider { get; private set; }

#nullable restore

    internal void Init(ZoneAnchor anchor)
    {
        Anchor = anchor;

        transform.SetLocalPositionAndRotation(anchor.Position - anchor.Zone.Center, Quaternion.identity);
        transform.localScale = Vector3.one;
        gameObject.layer = 3;
        gameObject.tag = "Logic";

        BoxCollider collider = gameObject.GetOrAddComponent<BoxCollider>();
        collider.size = new Vector3(0.385f, 0.385f, 0.385f);
        collider.center = Vector3.zero;
        collider.isTrigger = true;
        collider.enabled = true;
        Collider = collider;
    }
    //private void Update()
    //{
    //    if (ZoneEditorUI.Instance is not { IsActive: true })
    //        return;
    //
    //    Bounds bounds = Collider.bounds;
    //    RuntimeGizmos.Get().Box(bounds.center, bounds.size, Color.cyan);
    //}

    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        if (data.collider != Collider)
            return;

        UncreatedZoneEditor.Instance.LogConditional($"Selected anchor {Anchor.Index}.");
        EditorZones.Instance.RequestSelectZone(Anchor.Zone, Anchor.Index);
    }
    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        if (data.collider != Collider)
            return;

        EditorZones.Instance.DeselectZone();
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        int index = EditorZones.Instance.ZoneList.IndexOf(Anchor.Zone);

        if (index >= 0)
            EditorZones.Instance.MoveAnchor(new ZoneAnchorIdentifier(index, Anchor.Index), transform.position - Anchor.Zone.Center);
    }
}
#endif