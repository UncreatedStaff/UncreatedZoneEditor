#if CLIENT
using DevkitServer;
using DevkitServer.Core.Tools;
using System.Collections.Generic;
using System.Linq;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor.Tools;
public class ZoneEditorTool : DevkitServerSelectionTool
{
    protected override void EarlyInputTick()
    {

    }

    public override void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        int anchorIndex = EditorZones.Instance.SelectedZoneAnchorIndex;

        ZoneInfo? selectedZone = EditorZones.Instance.SelectedZone;

        if (selectedZone != null)
        {
            if (InputUtil.IsHoldingControl())
                anchorIndex -= 1;
            else anchorIndex += 1;

            if (anchorIndex < 0)
                anchorIndex = selectedZone.Anchors.Count;
            else if (anchorIndex > selectedZone.Anchors.Count)
                anchorIndex = selectedZone.Anchors.Count;
        }
        
        if (DevkitServerModule.IsEditing)
        {
            if (selectedZone != null)
            {
                EditorZones.Instance.RequestZoneAnchorInstantiation(position, EditorZones.Instance.SelectedZoneIndex, anchorIndex);
            }
            else
            {
                EditorZones.Instance.RequestZoneInstantiation(string.Empty, ZoneShape.Cylinder, position, position);
            }
        }
        else
        {
            if (selectedZone != null)
            {
                EditorZones.Instance.AddZoneAnchorLocal(new ZoneAnchor(selectedZone, anchorIndex) { Position = position }, EditorZones.Instance.SelectedZoneIndex, anchorIndex);
            }
            else
            {
                EditorZones.Instance.AddZoneLocal(new ZoneInfo
                {
                    Center = position,
                    Spawn = position,
                    Shape = ZoneShape.Cylinder,
                    Creator = Provider.client
                });
            }
        }
    }

    protected override IEnumerable<GameObject> EnumerateAreaSelectableObjects()
    {
        ZoneInfo? selectedZone = EditorZones.Instance.SelectedZone;
        if (selectedZone == null)
            return Enumerable.Empty<GameObject>();

        return selectedZone.Anchors.Where(x => x.Component != null)
                                   .Select(x => x.Component!.gameObject);
    }
}
#endif