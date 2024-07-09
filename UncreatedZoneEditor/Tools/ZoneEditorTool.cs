#if CLIENT
using DevkitServer;
using DevkitServer.Core.Tools;
using SDG.Framework.Devkit;
using System.Collections.Generic;
using System.Linq;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Objects;
using Uncreated.ZoneEditor.UI;

namespace Uncreated.ZoneEditor.Tools;
public class ZoneEditorTool : DevkitServerSelectionTool
{
    public ZoneEditorTool()
    {
        CanMiddleClickPick = false;
        CanRotate = false;
        CanScale = false;
        RootSelections = false;
    }
    protected override void EarlyInputTick()
    {
        Vector3 size = new Vector3(0.385f, 0.385f, 0.385f);
        RuntimeGizmos gizmos = RuntimeGizmos.Get();
        foreach (ZoneInfo zone in EditorZones.Instance.ZoneList)
        {
            bool isSelected = zone.IsSelected;
            gizmos.Arrow(zone.TemporaryCenter + Vector3.up * 15, Vector3.down, 15f, isSelected && !EditorZones.Instance.IsSpawnPositionSelected ? Color.yellow : Color.magenta);

            if (!zone.TemporaryCenter.IsNearlyEqual(zone.TemporarySpawn))
            {
                gizmos.Arrow(zone.TemporarySpawn + Vector3.up * 15, Vector3.down, 15f, isSelected && EditorZones.Instance.IsSpawnPositionSelected ? new Color32(255, 153, 0, 255) : Color.green);
            }

            for (int i = 0; i < zone.Anchors.Count; i++)
            {
                ZoneAnchor anchor = zone.Anchors[i];
                gizmos.Box(anchor.TemporaryPosition, size, isSelected && i == EditorZones.Instance.SelectedZoneAnchorIndex ? Color.yellow : Color.blue);
            }
        }
    }

    protected override bool TryRaycastSelectableItems(ref Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, 8192f, 8, QueryTriggerInteraction.Collide);
    }

    public override void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        int anchorIndex = EditorZones.Instance.SelectedZoneAnchorIndex;

        ZoneInfo? selectedZone = EditorZones.Instance.SelectedZone;

        if (selectedZone != null)
        {
            // -1 if holding control, otherwise +1
            anchorIndex += (InputUtil.IsHoldingControl() ? 1 : 0) * -2 + 1;

            if (anchorIndex < 0 || anchorIndex > selectedZone.Anchors.Count)
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
                EditorZones.Instance.RequestZoneInstantiation(string.Empty, ZoneEditorUI.Instance?.SelectedShape ?? ZoneShape.Cylinder, position, position, 128f);
            }
        }
        else
        {
            if (selectedZone != null)
            {
                EditorZones.Instance.AddZoneAnchorLocal(new ZoneAnchor(selectedZone, anchorIndex) { Position = position, TemporaryPosition = position }, EditorZones.Instance.SelectedZoneIndex, anchorIndex);
            }
            else
            {
                EditorZones.Instance.AddZoneLocal(new ZoneInfo
                {
                    Center = position,
                    TemporaryCenter = position,
                    Spawn = position,
                    TemporarySpawn = position,
                    Shape = ZoneEditorUI.Instance?.SelectedShape ?? ZoneShape.Cylinder,
                    Creator = Provider.client,
                    Height = 128f
                });
            }
        }
    }

    protected override void OnTempMoved()
    {
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject.TryGetComponent(out ZoneAnchorComponent anchorComp))
            {
                anchorComp.Anchor.TemporaryPosition = anchorComp.transform.position;
            }
            else if (selection.gameObject.TryGetComponent(out ZoneComponent zoneComp))
            {
                if (zoneComp.IsSpawnSelected)
                {
                    zoneComp.Zone.TemporarySpawn = zoneComp.transform.position;
                }
                else
                {
                    zoneComp.Zone.TemporaryCenter = zoneComp.transform.position;

                    foreach (ZoneAnchor anchor in zoneComp.Zone.Anchors)
                    {
                        if (anchor.Component == null)
                            continue;

                        anchor.TemporaryPosition = anchor.Component.transform.position;
                    }
                }
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