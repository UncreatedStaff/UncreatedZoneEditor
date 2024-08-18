using Uncreated.ZoneEditor.Tools;

#if CLIENT
namespace Uncreated.ZoneEditor.UI;
public class ZoneMapperUI : SleekFullscreenBox
{
    public static ZoneMapperUI? Instance { get; internal set; }
    public bool IsActive { get; private set; }

    private readonly ISleekFloat32Field _weightField;

    internal ZoneMapperUI()
    {
        Instance = this;

        _weightField = Glazier.Get().CreateFloat32Field();
        _weightField.PositionScale_Y = 1f;
        _weightField.PositionOffset_Y = -30f;
        _weightField.SizeOffset_Y = 30f;
        _weightField.SizeOffset_X = 230f; 
        _weightField.Value = 1f;
        _weightField.IsVisible = false;
        _weightField.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("MapperWeightTooltip");
        _weightField.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("MapperWeightField"), ESleekSide.RIGHT);
        _weightField.OnValueChanged += OnWeightUpdated;

        AddChild(_weightField);
    }

    public override void OnDestroy()
    {
        Close();
    }

    public void Open()
    {
        if (IsActive)
            return;

        // close other tools
        EditorLevelObjectsUI.close();
        EditorLevelVisibilityUI.close();
        EditorLevelPlayersUI.close();
        if (ZoneEditorUI.Instance is { IsActive: true })
        {
            ZoneEditorUI.Instance.Close();
        }
        if (UserControl.ActiveTool is VolumesEditor && UIAccessTools.EditorVolumesUI is { } volUi)
        {
            ZoneEditorUI.CloseVolumeUI?.Invoke(volUi);
        }

        IsActive = true;
        UserControl.ActiveTool = new ZoneMapperTool();
        AnimateIntoView();
    }

    public void Close()
    {
        if (!IsActive)
            return;

        // close this tool
        if (UserControl.ActiveTool is ZoneMapperTool)
            UserControl.ActiveTool = null;

        IsActive = false;
        AnimateOutOfView(1f, 0f);
    }

    private void OnWeightUpdated(ISleekFloat32Field field, float value)
    {
        if (UserControl.ActiveTool is not ZoneMapperTool tool)
            return;

        if (value <= 0f)
        {
            value = 1f;
            field.Value = 1f;
        }

        tool.UpdateSelectedWeight(value);
    }

    internal void UpdateSelectedZone(int selectedZoneIndex, int selectedLineIndex)
    {
        if (selectedLineIndex < 0 || selectedZoneIndex < 0 || selectedZoneIndex > LevelZones.ZoneList.Count)
        {
            _weightField.IsVisible = false;
            _weightField.Value = 1f;
        }
        else
        {
            _weightField.IsVisible = true;
            _weightField.Value = LevelZones.ZoneList[selectedZoneIndex].UpstreamZones[selectedLineIndex].Weight;
        }
    }
}
#endif