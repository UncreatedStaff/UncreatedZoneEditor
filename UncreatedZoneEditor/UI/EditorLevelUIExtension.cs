#if CLIENT
namespace Uncreated.ZoneEditor.UI;

[UIExtension(typeof(EditorLevelUI))]
public class EditorLevelUIExtension : UIExtension
{
#nullable disable
    [ExistingMember("objectsButton")]
    private readonly SleekButtonIcon _objectsButton;
    
    [ExistingMember("playersButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon _playersButton;
    
    [ExistingMember("volumesButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon _volumesButton;
    
    [ExistingMember("visibilityButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon _visibilityButton;

    [ExistingMember("container")]
    private readonly SleekFullscreenBox _container;

    private readonly ZoneEditorUI _zoneEditorUI;

#nullable restore
        
    public EditorLevelUIExtension()
    {
        ISleekButton zoneEditorButton = Glazier.Get().CreateButton();

        _objectsButton.onClickedButton    += CloseTool;
        _playersButton.onClickedButton    += CloseTool;
        _volumesButton.onClickedButton    += CloseTool;
        _visibilityButton.onClickedButton += CloseTool;

        zoneEditorButton.CopyTransformFrom(_objectsButton);
        zoneEditorButton.PositionOffset_Y += 40;
        zoneEditorButton.Text = UncreatedZoneEditor.Instance.Translations.Translate("ZoneToolButton");
        zoneEditorButton.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("ZoneToolButtonTooltip");
        zoneEditorButton.OnClicked += OnZoneEditorOpened;

        _container.AddChild(zoneEditorButton);

        _zoneEditorUI = new ZoneEditorUI
        {
            PositionOffset_X = 10f,
            PositionOffset_Y = 10f,
            PositionScale_X = 1f,
            SizeOffset_X = -20f,
            SizeOffset_Y = -20f,
            SizeScale_X = 1f,
            SizeScale_Y = 1f
        };

        _zoneEditorUI.IsVisible = false;
        _container.AddChild(_zoneEditorUI);

        ZoneEditorUI.Instance = _zoneEditorUI;
    }

    private void OnZoneEditorOpened(ISleekElement button)
    {
        _zoneEditorUI.Open();
    }

    private void CloseTool(ISleekElement button)
    {
        _zoneEditorUI.Close();
    }

    protected override void Closed()
    {
        _zoneEditorUI.Close();
    }
}
#endif