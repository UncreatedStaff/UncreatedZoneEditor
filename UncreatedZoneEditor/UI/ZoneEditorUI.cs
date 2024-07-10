#if CLIENT
using DanielWillett.ReflectionTools;
using SDG.Framework.Landscapes;
using System;
using System.Globalization;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Objects;
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

    private readonly ISleekField _nameField;
    private readonly ISleekField _shortNameField;
    private readonly ISleekFloat32Field _minHeightField;
    private readonly ISleekSlider _minHeightSlider;
    private readonly ISleekToggle _minHeightInfinityToggle;
    private readonly ISleekFloat32Field _maxHeightField;
    private readonly ISleekSlider _maxHeightSlider;
    private readonly ISleekToggle _maxHeightInfinityToggle;
    private readonly SleekList<ZoneModel> _zoneList;
    private readonly SleekButtonState _shapeToggle;
    public ZoneShape SelectedShape
    {
        get => (ZoneShape)_shapeToggle.state;
        set
        {
            _shapeToggle.state = (int)value;
            OnShapeToggled(_shapeToggle, (int)value);
        }
    }

    public string CurrentName
    {
        get => _nameField.Text ?? string.Empty;
        set
        {
            _nameField.Text = value ?? string.Empty;
            NameFieldUpdated(_nameField);
        }
    }

    public string CurrentShortName
    {
        get => _shortNameField.Text ?? string.Empty;
        set
        {
            _shortNameField.Text = value ?? string.Empty;
            NameFieldUpdated(_shortNameField);
        }
    }

    public float CurrentMinimumHeight
    {
        get => _minHeightField.Value;
        set
        {
            TryUpdateMinHeight(ref value);
            UpdateMinHeightUI(value);
        }
    }

    public float CurrentMaximumHeight
    {
        get => _maxHeightField.Value;
        set
        {
            TryUpdateMaxHeight(ref value);
            UpdateMaxHeightUI(value);
        }
    }

    internal ZoneEditorUI()
    {
        Instance = this;

        _zoneList = new SleekList<ZoneModel>
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

        _zoneList.SetData(LevelZones.ZoneList);

        AddChild(_zoneList);

        float h = 0;

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
            PositionOffset_Y = h - 30f,
            SizeOffset_Y = 30f,
            SizeOffset_X = 230f,
            tooltip = UncreatedZoneEditor.Instance.Translations.Translate("ShapeTooltip")
        };

        _shapeToggle.onSwappedState += OnShapeToggled;
        _shapeToggle.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("ShapeField"), ESleekSide.RIGHT);

        AddChild(_shapeToggle);

        h -= 35f;

        _shortNameField = Glazier.Get().CreateStringField();
        _shortNameField.PositionScale_Y = 1f;
        _shortNameField.PositionOffset_X = 0f;
        _shortNameField.PositionOffset_Y = h - 30f;
        _shortNameField.SizeOffset_Y = 30f;
        _shortNameField.SizeOffset_X = 230f;
        _shortNameField.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("ShortNameTooltip");
        _shortNameField.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("ShortNameField"), ESleekSide.RIGHT);
        _shortNameField.OnTextSubmitted += ShortNameFieldUpdated;

        AddChild(_shortNameField);

        h -= 35f;

        _nameField = Glazier.Get().CreateStringField();
        _nameField.PositionScale_Y = 1f;
        _nameField.PositionOffset_X = 0f;
        _nameField.PositionOffset_Y = h - 30f;
        _nameField.SizeOffset_Y = 30f;
        _nameField.SizeOffset_X = 230f;
        _nameField.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("NameTooltip");
        _nameField.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("NameField"), ESleekSide.RIGHT);
        _nameField.OnTextSubmitted += NameFieldUpdated;

        AddChild(_nameField);

        h -= 35f;

        _minHeightSlider = Glazier.Get().CreateSlider();
        _minHeightSlider.PositionScale_Y = 1f;
        _minHeightSlider.PositionOffset_X = 0f;
        _minHeightSlider.PositionOffset_Y = h - 25f;
        _minHeightSlider.SizeOffset_Y = 20f;
        _minHeightSlider.Orientation = ESleekOrientation.HORIZONTAL;
        _minHeightSlider.Value = 0;
        _minHeightSlider.IsInteractable = false;
        _minHeightSlider.SizeOffset_X = 230f;
        _minHeightSlider.OnValueChanged += MinHeightSliderUpdated;

        AddChild(_minHeightSlider);

        _minHeightInfinityToggle = Glazier.Get().CreateToggle();
        _minHeightInfinityToggle.PositionScale_Y = 1f;
        _minHeightInfinityToggle.PositionOffset_X = 235f;
        _minHeightInfinityToggle.PositionOffset_Y = h - 35f;
        _minHeightInfinityToggle.SizeOffset_Y = 40f;
        _minHeightInfinityToggle.SizeOffset_X = 40f;
        _minHeightInfinityToggle.Value = true;
        _minHeightInfinityToggle.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("MinHeightInfinityTooltip");
        _minHeightInfinityToggle.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("MinHeightInfinityToggle"), ESleekSide.RIGHT);
        _minHeightInfinityToggle.OnValueChanged += MinHeightInfinityToggleUpdated;

        AddChild(_minHeightInfinityToggle);

        h -= 35f;

        _minHeightField = Glazier.Get().CreateFloat32Field();
        _minHeightField.PositionScale_Y = 1f;
        _minHeightField.PositionOffset_X = 0f;
        _minHeightField.PositionOffset_Y = h - 30f;
        _minHeightField.SizeOffset_Y = 30f;
        _minHeightField.SizeOffset_X = 230f;
        _minHeightField.Value = float.NegativeInfinity;
        _minHeightField.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("MinHeightTooltip");
        _minHeightField.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("MinHeightField"), ESleekSide.RIGHT);
        _minHeightField.OnValueChanged += MinHeightFieldUpdated;

        AddChild(_minHeightField);

        h -= 35f;

        _maxHeightSlider = Glazier.Get().CreateSlider();
        _maxHeightSlider.PositionScale_Y = 1f;
        _maxHeightSlider.PositionOffset_X = 0f;
        _maxHeightSlider.PositionOffset_Y = h - 25f;
        _maxHeightSlider.SizeOffset_Y = 20f;
        _maxHeightSlider.Orientation = ESleekOrientation.HORIZONTAL;
        _maxHeightSlider.IsInteractable = false;
        _maxHeightSlider.SizeOffset_X = 230f;
        _maxHeightSlider.OnValueChanged += MaxHeightSliderUpdated;

        AddChild(_maxHeightSlider);

        _maxHeightInfinityToggle = Glazier.Get().CreateToggle();
        _maxHeightInfinityToggle.PositionScale_Y = 1f;
        _maxHeightInfinityToggle.PositionOffset_X = 235f;
        _maxHeightInfinityToggle.PositionOffset_Y = h - 35f;
        _maxHeightInfinityToggle.SizeOffset_Y = 40f;
        _maxHeightInfinityToggle.SizeOffset_X = 40f;
        _maxHeightInfinityToggle.Value = true;
        _maxHeightInfinityToggle.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("MaxHeightInfinityTooltip");
        _maxHeightInfinityToggle.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("MaxHeightInfinityToggle"), ESleekSide.RIGHT);
        _maxHeightInfinityToggle.OnValueChanged += MaxHeightInfinityToggleUpdated;

        AddChild(_maxHeightInfinityToggle);

        h -= 35f;

        _maxHeightField = Glazier.Get().CreateFloat32Field();
        _maxHeightField.PositionScale_Y = 1f;
        _maxHeightField.PositionOffset_X = 0f;
        _maxHeightField.PositionOffset_Y = h - 30f;
        _maxHeightField.SizeOffset_Y = 30f;
        _maxHeightField.SizeOffset_X = 230f;
        _maxHeightField.TooltipText = UncreatedZoneEditor.Instance.Translations.Translate("MaxHeightTooltip");
        _maxHeightField.AddLabel(UncreatedZoneEditor.Instance.Translations.Translate("MaxHeightField"), ESleekSide.RIGHT);
        _maxHeightField.OnValueChanged += MaxHeightFieldUpdated;

        AddChild(_maxHeightField);

        //h -= 35f;

        EditorZones.OnZoneAdded += UpdateZoneList;
        EditorZones.OnZoneRemoved += UpdateZoneList;
        EditorZones.OnSelectedZoneUpdated += SelectionChanged;
        EditorZones.OnZoneShapeUpdated += ShapeChanged;
    }


    public override void OnDestroy()
    {
        EditorZones.OnZoneAdded -= UpdateZoneList;
        EditorZones.OnZoneRemoved -= UpdateZoneList;
        EditorZones.OnSelectedZoneUpdated -= SelectionChanged;
        EditorZones.OnZoneShapeUpdated -= ShapeChanged;
    }

    private static float HeightToSlider(float height) => Mathf.Clamp01(MathF.Sqrt(Math.Max(0, height + Landscape.TILE_HEIGHT / 2f)) / 45.254834f);
    private static float SliderToHeight(float state) => MathF.Pow(state * 45.254834f, 2f) - Landscape.TILE_HEIGHT / 2f;

    private void NameFieldUpdated(ISleekField field)
    {
        ZoneModel? selectedZone = EditorZones.SelectedZone;

        if (selectedZone == null)
        {
            field.Text = string.Empty;
            return;
        }

        string name = field.Text;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = selectedZone.Index.ToString(CultureInfo.InvariantCulture);
        }

        // todo EditorZones.Instance.SetZoneShapeLocal(EditorZones.Instance.SelectedZoneIndex, SelectedShape);
        selectedZone.Name = name;
        field.Text = name;
    }

    private void ShortNameFieldUpdated(ISleekField field)
    {
        ZoneModel? selectedZone = EditorZones.SelectedZone;

        if (selectedZone == null)
        {
            field.Text = string.Empty;
            return;
        }

        string? shortName = field.Text;
        if (string.IsNullOrWhiteSpace(shortName))
        {
            shortName = null;
        }

        // todo EditorZones.Instance.SetZoneShapeLocal(EditorZones.Instance.SelectedZoneIndex, SelectedShape);
        selectedZone.ShortName = shortName;
        field.Text = shortName ?? string.Empty;
    }

    private void MinHeightFieldUpdated(ISleekFloat32Field field, float value)
    {
        CurrentMinimumHeight = float.IsFinite(CurrentMaximumHeight) ? Math.Min(CurrentMaximumHeight, value) : value;
    }

    private void MinHeightSliderUpdated(ISleekSlider slider, float state)
    {
        CurrentMinimumHeight = float.IsFinite(state) ? SliderToHeight(state) : float.NegativeInfinity;
    }

    private void MaxHeightFieldUpdated(ISleekFloat32Field field, float value)
    {
        CurrentMaximumHeight = float.IsFinite(CurrentMinimumHeight) ? Math.Max(CurrentMinimumHeight, value) : value;
    }

    private void MaxHeightSliderUpdated(ISleekSlider slider, float state)
    {
        CurrentMaximumHeight = float.IsFinite(state) ? SliderToHeight(state) : float.PositiveInfinity;
    }

    private void MinHeightInfinityToggleUpdated(ISleekToggle toggle, bool state)
    {
        CurrentMinimumHeight = state ? float.NegativeInfinity : Landscape.TILE_HEIGHT / -2f;
    }

    private void MaxHeightInfinityToggleUpdated(ISleekToggle toggle, bool state)
    {
        CurrentMaximumHeight = state ? float.PositiveInfinity : 1024f;
    }

    private void UpdateMinHeightUI(float value)
    {
        if (!float.IsFinite(value))
        {
            _minHeightInfinityToggle.Value = true;
            _minHeightSlider.Value = 0f;
            _minHeightSlider.IsInteractable = false;
            _minHeightField.Value = float.NegativeInfinity;
            return;
        }

        _minHeightField.Value = value;
        _minHeightSlider.IsInteractable = true;
        _minHeightSlider.Value = HeightToSlider(value);
        _minHeightInfinityToggle.Value = false;
    }

    private void UpdateMaxHeightUI(float value)
    {
        if (!float.IsFinite(value))
        {
            _maxHeightInfinityToggle.Value = true;
            _maxHeightSlider.Value = 1f;
            _maxHeightSlider.IsInteractable = false;
            _maxHeightField.Value = float.PositiveInfinity;
            return;
        }

        _maxHeightField.Value = value;
        _maxHeightSlider.IsInteractable = true;
        _maxHeightSlider.Value = HeightToSlider(value);
        _maxHeightInfinityToggle.Value = false;
    }

    private void TryUpdateMinHeight(ref float value)
    {
        ZoneModel? selectedZone = EditorZones.SelectedZone;

        if (selectedZone == null)
            return;

        // todo request

        switch (selectedZone.Component)
        {
            case CircleZoneComponent circle:
                circle.MinimumHeight = value;
                break;

            case PolygonZoneComponent polygon:
                polygon.MinimumHeight = value;
                break;

            case null when selectedZone.Shape == ZoneShape.Cylinder:
                (selectedZone.CircleInfo ??= new ZoneCircleInfo()).MinimumHeight = value;
                break;

            case null when selectedZone.Shape == ZoneShape.Polygon:
                (selectedZone.PolygonInfo ??= new ZonePolygonInfo()).MinimumHeight = value;
                break;
        }
    }

    private bool TryUpdateMaxHeight(ref float value)
    {
        ZoneModel? selectedZone = EditorZones.SelectedZone;

        if (selectedZone == null)
            return true;

        // todo request

        switch (selectedZone.Component)
        {
            case CircleZoneComponent circle:
                circle.MaximumHeight = value;
                break;

            case PolygonZoneComponent polygon:
                polygon.MaximumHeight = value;
                break;

            case null when selectedZone.Shape == ZoneShape.Cylinder:
                (selectedZone.CircleInfo ??= new ZoneCircleInfo()).MaximumHeight = value;
                break;

            case null when selectedZone.Shape == ZoneShape.Polygon:
                (selectedZone.PolygonInfo ??= new ZonePolygonInfo()).MaximumHeight = value;
                break;
        }

        return true;
    }

    private void ShapeChanged(ZoneModel zone, ZoneShape oldShape)
    {
        if (zone == EditorZones.SelectedZone && IsActive)
        {
            _shapeToggle.state = (int)zone.Shape;
        }
    }

    private void SelectionChanged(ZoneModel? newSelection, ZoneModel? oldSelection)
    {
        bool heightVisibility;
        if (newSelection != null && IsActive)
        {
            heightVisibility = newSelection.Shape is ZoneShape.Polygon or ZoneShape.Cylinder;
            _shapeToggle.state = (int)newSelection.Shape;
            if (newSelection.CircleInfo != null)
            {
                UpdateMinHeightUI(newSelection.CircleInfo.MinimumHeight);
                UpdateMaxHeightUI(newSelection.CircleInfo.MaximumHeight);
            }
            else if (newSelection.PolygonInfo != null)
            {
                UpdateMinHeightUI(newSelection.PolygonInfo.MinimumHeight);
                UpdateMaxHeightUI(newSelection.PolygonInfo.MaximumHeight);
            }

            _nameField.Text = newSelection.Name;
            _shortNameField.Text = newSelection.ShortName ?? string.Empty;
        }
        else
        {
            heightVisibility = false;
            _nameField.Text = LevelZones.ZoneList.Count.ToString(CultureInfo.InvariantCulture);
            _shortNameField.Text = string.Empty;
        }

        _minHeightSlider.IsVisible = heightVisibility;
        _minHeightField.IsVisible = heightVisibility;
        _minHeightInfinityToggle.IsVisible = heightVisibility;
        _maxHeightSlider.IsVisible = heightVisibility;
        _maxHeightField.IsVisible = heightVisibility;
        _maxHeightInfinityToggle.IsVisible = heightVisibility;
    }

    private void OnShapeToggled(SleekButtonState button, int index)
    {
        ZoneModel? selectedZone = EditorZones.SelectedZone;

        if (selectedZone != null)
            EditorZones.ChangeShapeLocal(selectedZone, SelectedShape);
    }

    private void UpdateZoneList(ZoneModel model)
    {
        if (IsActive)
        {
            _zoneList.NotifyDataChanged();
        }
    }

    private ISleekElement CreateZoneElement(ZoneModel item)
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

        button.OnClicked += OnSelectedZoneModel;
        return button;
    }

    private void OnSelectedZoneModel(ISleekElement button)
    {
        // this is awful but theres not really a better way with SleekList.
        int index = Mathf.FloorToInt(button.PositionOffset_Y / 40f);

        if (index >= LevelZones.ZoneList.Count || index < 0)
        {
            return;
        }

        ZoneModel zone = LevelZones.ZoneList[index];
        EditorZones.SelectedZone = zone;
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
        _zoneList.NotifyDataChanged();
        ZoneModel? selectedZone = EditorZones.SelectedZone;

        if (selectedZone != null)
        {
            SelectionChanged(selectedZone, null);
        }

        UserControl.ActiveTool = new ZoneEditorTool();
        AnimateIntoView();
    }

    public void Close()
    {
        if (!IsActive)
            return;

        EditorZones.SelectedZone = null;

        // close this tool
        if (UserControl.ActiveTool is ZoneEditorTool)
            UserControl.ActiveTool = null;

        IsActive = false;
        AnimateOutOfView(1f, 0f);
    }
}
#endif