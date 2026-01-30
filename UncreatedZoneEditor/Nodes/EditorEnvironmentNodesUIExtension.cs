using DanielWillett.UITools.API.Extensions;
using DanielWillett.UITools.API.Extensions.Members;
using DanielWillett.UITools.Util;
using System.Linq;
using Uncreated.ZoneEditor.Caches;
using Uncreated.ZoneEditor.VehicleBays;

namespace Uncreated.ZoneEditor.Nodes;

[UIExtension(typeof(EditorEnvironmentNodesUI))]
internal class EditorEnvironmentNodesUIExtension : UIExtension<EditorEnvironmentNodesUI>
{
#nullable disable

    [ExistingMember("tool", FailureBehavior = ExistingMemberFailureBehavior.FailToLoad)]
    private readonly NodesEditor _tool;

#nullable restore

    public bool IsCacheActive => _tool.activeNodeSystem == CacheDevkitNodeSystem.Get();
    public bool IsVehicleBayActive => _tool.activeNodeSystem == VehicleBayDevkitNodeSystem.Get();
    public bool IsVehicleBaySignActive => _tool.activeNodeSystem == VehicleBaySignDevkitNodeSystem.Get();

    public EditorEnvironmentNodesUIExtension()
    {
        ISleekElement frame = Instance!.AsEnumerable().LastOrDefault()!;
        if (frame == null)
        {
            UncreatedZoneEditor.Instance.LogWarning("Frame not found.");
            return;
        }

        ISleekButton? button = frame.AsEnumerable().LastOrDefault() as ISleekButton;
        if (button == null)
        {
            UncreatedZoneEditor.Instance.LogWarning("Last button not found.");
            return;
        }

        ISleekButton cachesButton = Glazier.Get().ConfigureButton()
            .WithText(UncreatedZoneEditor.Instance.Translations.Translate("ButtonCacheNode"))
            .WithOrigin(button)
            .AddRawPositionPixels(0f, button.SizeOffset_Y)
            .WhenLeftClicked(_ => _tool.activeNodeSystem = CacheDevkitNodeSystem.Get())
            .BuildAndParent(frame);

        ISleekButton vbButton = Glazier.Get().ConfigureButton()
            .WithText(UncreatedZoneEditor.Instance.Translations.Translate("ButtonVehicleBayNode"))
            .WithOrigin(cachesButton)
            .AddRawPositionPixels(0f, cachesButton.SizeOffset_Y)
            .WhenLeftClicked(_ => _tool.activeNodeSystem = VehicleBayDevkitNodeSystem.Get())
            .BuildAndParent(frame);

        Glazier.Get().ConfigureButton()
            .WithText(UncreatedZoneEditor.Instance.Translations.Translate("ButtonVehicleBaySignNode"))
            .WithOrigin(vbButton)
            .AddRawPositionPixels(0f, vbButton.SizeOffset_Y)
            .WhenLeftClicked(_ => _tool.activeNodeSystem = VehicleBaySignDevkitNodeSystem.Get())
            .BuildAndParent(frame);
    }
}