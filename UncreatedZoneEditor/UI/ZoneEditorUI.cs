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

    internal ZoneEditorUI()
    {
        Instance = this;

        EditorZones.Instance.OnZoneAdded += UpdateZoneList;
        EditorZones.Instance.OnZoneRemoved += UpdateZoneList;

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
        _zoneList.NotifyDataChanged();
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
        AnimateOutOfView(1f, 0f);
    }
}
#endif