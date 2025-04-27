using DanielWillett.UITools.API.Extensions;
using DanielWillett.UITools.API.Extensions.Members;
using DanielWillett.UITools.Util;
using System.Linq;

namespace Uncreated.ZoneEditor.Caches;

[UIExtension(typeof(EditorEnvironmentNodesUI))]
internal class EditorEnvironmentNodesUIExtension : UIExtension<EditorEnvironmentNodesUI>
{
#nullable disable

    [ExistingMember("tool", FailureBehavior = ExistingMemberFailureBehavior.FailToLoad)]
    private readonly NodesEditor _tool;

#nullable restore

    public bool IsActive => _tool.activeNodeSystem == CacheDevkitNodeSystem.Get();

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

        Glazier.Get().ConfigureButton()
            .WithText(UncreatedZoneEditor.Instance.Translations.Translate("ButtonCacheNode"))
            .WithOrigin(button)
            .AddRawPositionPixels(0f, button.SizeOffset_Y)
            .WhenLeftClicked(_ => _tool.activeNodeSystem = CacheDevkitNodeSystem.Get())
            .BuildAndParent(frame);
    }
}