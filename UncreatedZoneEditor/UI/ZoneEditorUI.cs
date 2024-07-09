#if CLIENT
using DanielWillett.ReflectionTools;
using System;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Tools;

namespace Uncreated.ZoneEditor.UI;
public class ZoneEditorUI : SleekFullscreenBox
{
    private static readonly Action<object>? CloseVolumeUI = Accessor.GenerateInstanceCaller<Action<object>>(
        UIAccessTools.EditorVolumesUIType?.GetMethod("Close")!,
        throwOnError: false,
        allowUnsafeTypeBinding: true
    );

    public static ZoneEditorUI? Instance { get; internal set; }
    public bool IsActive { get; private set; }

    private readonly SleekList<ZoneInfo> _zoneList;
    private readonly SleekButtonState _shapeToggle;
    public ZoneShape SelectedShape => (ZoneShape)_shapeToggle.state;
    internal ZoneEditorUI()
    {
        Instance = this;

        EditorZones.Instance.OnZoneAdded += UpdateZoneList;
        EditorZones.Instance.OnZoneRemoved += UpdateZoneList;
        EditorZones.Instance.OnZoneSelectionChanged += SelectionChanged;
        EditorZones.Instance.OnZoneShapeChanged += ShapeChanged;

        _zoneList = new SleekList<ZoneInfo>
        {
            PositionOffset_X = -230,
            PositionOffset_Y = 230,
            PositionScale_X = 1f,
            SizeOffset_X = 230f,
            SizeOffset_Y = -230f,
            SizeScale_Y = 1f,
            itemHeight = 30,
            itemPadding = 10,
            onCreateElement = CreateZoneElement
        };

        _zoneList.SetData(EditorZones.Instance.ZoneList);

        AddChild(_zoneList);


        _shapeToggle = new SleekButtonState
        (
            [
                new GUIContent(UncreatedZoneEditor.Instance.Translations.Translate("ShapeAABB")),
                new GUIContent(UncreatedZoneEditor.Instance.Translations.Translate("ShapeCylinder")),
                new GUIContent(UncreatedZoneEditor.Instance.Translations.Translate("ShapeSphere")),
                new GUIContent(UncreatedZoneEditor.Instance.Translations.Translate("ShapePolygon"))
            ])
        {
            PositionScale_Y = 1f,
            PositionOffset_X = 0f,
            PositionOffset_Y = -30f,
            SizeOffset_Y = 30f,
            SizeOffset_X = 230f,
            tooltip = UncreatedZoneEditor.Instance.Translations.Translate("ShapeTooltip")
        };
        _shapeToggle.onSwappedState += OnShapeToggled;
        _shapeToggle.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("ShapeField"), ESleekSide.RIGHT);

        AddChild(_shapeToggle);
    }

    private void ShapeChanged(ZoneInfo zone, int zoneIndex, ZoneShape newShape, ZoneShape oldShape)
    {
        if (zoneIndex == EditorZones.Instance.SelectedZoneIndex && IsActive)
        {
            _shapeToggle.state = (int)newShape;
        }
    }

    private void SelectionChanged(ZoneInfo? newSelection, int newSlectionIndex, int newAnchorSelectionIndex, ZoneInfo? oldSelection, int oldSelectionIndex, int oldAnchorSelectionIndex)
    {
        if (newSelection != null && IsActive)
        {
            _shapeToggle.state = (int)newSelection.Shape;
        }
    }

    private void OnShapeToggled(SleekButtonState button, int index)
    {
        ZoneInfo? selectedZone = EditorZones.Instance.SelectedZone;

        if (selectedZone == null)
            return;

        // todo not local
        EditorZones.Instance.SetZoneShapeLocal(EditorZones.Instance.SelectedZoneIndex, SelectedShape);
    }

    public override void OnDestroy()
    {
        EditorZones.Instance.OnZoneAdded -= UpdateZoneList;
        EditorZones.Instance.OnZoneRemoved -= UpdateZoneList;
    }

    private void UpdateZoneList(ZoneInfo zone, int index)
    {
        if (IsActive)
        {
            _zoneList.NotifyDataChanged();
        }
    }

    private ISleekElement CreateZoneElement(ZoneInfo item)
    {
        ISleekButton button = Glazier.Get().CreateButton();
        if (item.ShortName != null && !item.ShortName.Equals(item.Name, StringComparison.Ordinal))
        {
            button.Text = item.Name + " [" + item.ShortName + "]";
        }
        else
        {
            button.Text = item.Name;
        }

        button.OnClicked += OnSelectedZoneInfo;
        return button;
    }

    private void OnSelectedZoneInfo(ISleekElement button)
    {
        // this is awful but theres not really a better way with SleekList.
        int index = Mathf.FloorToInt(button.PositionOffset_Y / 40f);

        if (index >= EditorZones.Instance.ZoneList.Count || index < 0)
        {
            return;
        }

        ZoneInfo zone = EditorZones.Instance.ZoneList[index];
        EditorZones.Instance.SelectZone(zone);
    }

    public void Open()
    {
        if (IsActive)
            return;

        // close other tools
        EditorLevelObjectsUI.close();
        EditorLevelVisibilityUI.close();
        EditorLevelPlayersUI.close();
        if (UserControl.ActiveTool is VolumesEditor && UIAccessTools.EditorVolumesUI is { } volUi)
        {
            CloseVolumeUI?.Invoke(volUi);
        }

        IsActive = true;
        IsVisible = true;
        _zoneList.NotifyDataChanged();
        ZoneInfo? selectedZone = EditorZones.Instance.SelectedZone;

        if (selectedZone != null)
        {
            _shapeToggle.state = (int)selectedZone.Shape;
        }

        UserControl.ActiveTool = new ZoneEditorTool();
        AnimateIntoView();
    }

    public void Close()
    {
        if (!IsActive)
            return;

        // close this tool
        if (UserControl.ActiveTool is ZoneEditorTool)
            UserControl.ActiveTool = null;

        IsActive = false;
        IsVisible = false;
        AnimateOutOfView(1f, 0f);
    }
}
#endif