#if CLIENT
using DevkitServer.Core.Tools;
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
        CanRotate = false;
    }

    protected override void OnMiddleClickPicked(ref RaycastHit hit)
    {
        if (EditorZones.SelectedZone == null && hit.transform.TryGetComponent(out BaseZoneComponent comp) && ZoneEditorUI.Instance != null)
        {
            ZoneEditorUI.Instance.SelectedShape = comp.Model.Shape;
        }
    }

    protected override void EarlyInputTick()
    {
        RuntimeGizmos gizmos = RuntimeGizmos.Get();
        foreach (ZoneModel zone in LevelZones.ZoneList)
        {
            if (zone.Component != null)
            {
                zone.Component.RenderGizmos(gizmos);
            }
        }
    }

    protected override bool TryRaycastSelectableItems(ref Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, 8192f, 8, QueryTriggerInteraction.Collide);
    }

    public override void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (ZoneEditorUI.Instance?.CurrentName is not { Length: > 0 } name)
        {
            EditorMessage.SendEditorMessage(UncreatedZoneEditor.Instance.Translations.Translate("CreateZoneNoName"));
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            EditorMessage.SendEditorMessage(UncreatedZoneEditor.Instance.Translations.Translate("CreateZoneNoName"));
            return;
        }

        string? shortName = ZoneEditorUI.Instance?.CurrentShortName;
        if (string.IsNullOrWhiteSpace(shortName))
            shortName = null;

        ZoneShape shape = ZoneEditorUI.Instance?.SelectedShape ?? ZoneShape.Cylinder;
        EditorZones.AddZoneLocal(position, position, rotation.eulerAngles.y, name, shortName, shape, Provider.client);
    }
    protected override IEnumerable<GameObject> EnumerateAreaSelectableObjects()
    {
        return LevelZones.ZoneList.Where(x => x.Component != null)
                                  .Select(x => x.Component!.gameObject);
    }
}
#endif