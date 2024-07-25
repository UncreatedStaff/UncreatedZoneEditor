#if CLIENT
namespace Uncreated.ZoneEditor.UI;

[UIExtension(typeof(EditorLevelUI))]
public class EditorLevelUIExtension : UIExtension
{
#nullable disable

    [ExistingMember("objectsButton")]
    private readonly SleekButtonIcon _objectsButton;

    [ExistingMember("visibilityButton")]
    private readonly SleekButtonIcon _visibilityButton;

    [ExistingMember("container")]
    private readonly SleekFullscreenBox _container;

#nullable restore

    [ExistingMember("playersButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon? _playersButton;

    [ExistingMember("volumesButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon? _volumesButton;

    private readonly ZoneEditorUI _zoneEditorUI;
    private readonly ZoneMapperUI _zoneMapperUI;

    public EditorLevelUIExtension()
    {
        _objectsButton.onClickedButton += CloseTool;
        _visibilityButton.onClickedButton += CloseTool;

        if (_playersButton != null)
            _playersButton.onClickedButton += CloseTool;

        if (_volumesButton != null)
            _volumesButton.onClickedButton += CloseTool;


        ISleekButton zoneEditorButton = Glazier.Get().CreateButton();

        zoneEditorButton.CopyTransformFrom(_objectsButton);
        zoneEditorButton.PositionOffset_Y += 40;
        zoneEditorButton.Text = UncreatedZoneEditor.Instance.Translations.Translate("ZoneToolButton");
        zoneEditorButton.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("ZoneToolButtonTooltip");
        zoneEditorButton.OnClicked += OnZoneEditorOpened;

        _container.AddChild(zoneEditorButton);
        
        ISleekButton zoneMapperButton = Glazier.Get().CreateButton();

        zoneMapperButton.CopyTransformFrom(_visibilityButton);
        zoneMapperButton.PositionOffset_Y += 40;
        zoneMapperButton.Text = UncreatedZoneEditor.Instance.Translations.Translate("ZoneMapperButton");
        zoneMapperButton.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("ZoneMapperButtonTooltip");
        zoneMapperButton.OnClicked += OnZoneMapperOpened;

        _container.AddChild(zoneMapperButton);

        _zoneEditorUI = new ZoneEditorUI
        {
            PositionScale_X = 1f,
            SizeScale_X = 1f,
            SizeScale_Y = 1f
        };

        _container.AddChild(_zoneEditorUI);

        _zoneMapperUI = new ZoneMapperUI
        {
            PositionScale_X = 1f,
            SizeScale_X = 1f,
            SizeScale_Y = 1f
        };

        _container.AddChild(_zoneMapperUI);
    }

    private void OnZoneEditorOpened(ISleekElement button)
    {
        _zoneEditorUI.Open();
    }

    private void OnZoneMapperOpened(ISleekElement button)
    {
        _zoneMapperUI.Open();
    }

    private void CloseTool(ISleekElement button)
    {
        _zoneEditorUI.Close();
        _zoneMapperUI.Close();
    }

    protected override void Closed()
    {
        _zoneEditorUI.Close();
        _zoneMapperUI.Close();
    }
}
#endif