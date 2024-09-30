using SDG.Framework.Devkit.Interactable;

namespace Uncreated.ZoneEditor.Objects;
internal class PlayerSpawnWidgetComponent : MonoBehaviour,
    IDevkitSelectionTransformableHandler,
    IDevkitInteractableBeginSelectionHandler,
    IDevkitInteractableEndSelectionHandler,
    IDevkitSelectionCopyableHandler
{
#nullable disable
    public BaseZoneComponent Zone { get; private set; }
#nullable restore
    internal void Init(BaseZoneComponent baseZone)
    {
        Zone = baseZone;
    }

    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        Zone.IsPlayerSelected = true;
        ((IDevkitInteractableBeginSelectionHandler)Zone).beginSelection(data);
    }

    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        Zone.IsPlayerSelected = false;
        ((IDevkitInteractableEndSelectionHandler)Zone).endSelection(data);
    }

    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        ((IDevkitSelectionTransformableHandler)Zone).transformSelection();
    }

    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        return ((IDevkitSelectionCopyableHandler)Zone).copySelection();
    }
}
