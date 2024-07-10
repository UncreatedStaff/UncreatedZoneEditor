using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Uncreated.ZoneEditor.Data;
#if CLIENT
using SDG.Framework.Devkit;
using Uncreated.ZoneEditor.Objects;
using Uncreated.ZoneEditor.Tools;
#endif
#if SERVER
using DevkitServer;
using Uncreated.ZoneEditor.Multiplayer;
#endif

namespace Uncreated.ZoneEditor;

/// <summary>
/// API for editing zones in <see cref="LevelZones"/>.
/// </summary>
public static class EditorZones
{
    private static readonly CachedMulticastEvent<ZoneAdded> EventOnZoneAdded = new CachedMulticastEvent<ZoneAdded>(typeof(EditorZones), nameof(OnZoneAdded));
    private static readonly CachedMulticastEvent<ZoneRemoved> EventOnZoneRemoved = new CachedMulticastEvent<ZoneRemoved>(typeof(EditorZones), nameof(OnZoneRemoved));
    private static readonly CachedMulticastEvent<ZoneIndexUpdated> EventOnZoneIndexUpdated = new CachedMulticastEvent<ZoneIndexUpdated>(typeof(EditorZones), nameof(OnZoneIndexUpdated));
    internal static readonly CachedMulticastEvent<ZoneDimensionsUpdated> EventOnZoneDimensionsUpdated = new CachedMulticastEvent<ZoneDimensionsUpdated>(typeof(EditorZones), nameof(OnZoneDimensionsUpdated));
    private static readonly CachedMulticastEvent<SelectedZoneUpdated> EventOnSelectedZoneUpdated = new CachedMulticastEvent<SelectedZoneUpdated>(typeof(EditorZones), nameof(OnSelectedZoneUpdated));
    private static readonly CachedMulticastEvent<ZoneShapeUpdated> EventOnZoneShapeUpdated = new CachedMulticastEvent<ZoneShapeUpdated>(typeof(EditorZones), nameof(OnZoneShapeUpdated));

    internal static readonly List<BaseZoneComponent> ComponentsPendingUndo = new List<BaseZoneComponent>(4);
    private static ZoneModel? _selectedZone;

    /// <summary>
    /// Invoked when a zone is added locally.
    /// </summary>
    public static event ZoneAdded OnZoneAdded
    {
        add => EventOnZoneAdded.Add(value);
        remove => EventOnZoneAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone is removed locally.
    /// </summary>
    public static event ZoneRemoved OnZoneRemoved
    {
        add => EventOnZoneRemoved.Add(value);
        remove => EventOnZoneRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone's index is updated locally.
    /// </summary>
    public static event ZoneIndexUpdated OnZoneIndexUpdated
    {
        add => EventOnZoneIndexUpdated.Add(value);
        remove => EventOnZoneIndexUpdated.Remove(value);
    }
    
    /// <summary>
    /// Invoked when a zone's dimensions are updated locally.
    /// </summary>
    public static event ZoneDimensionsUpdated OnZoneDimensionsUpdated
    {
        add => EventOnZoneDimensionsUpdated.Add(value);
        remove => EventOnZoneDimensionsUpdated.Remove(value);
    }
    
    /// <summary>
    /// Invoked when a zone's shape type is changed locally.
    /// </summary>
    public static event ZoneShapeUpdated OnZoneShapeUpdated
    {
        add => EventOnZoneShapeUpdated.Add(value);
        remove => EventOnZoneShapeUpdated.Remove(value);
    }

#if CLIENT

    /// <summary>
    /// Invoked when the local user selects or deselects a new zone.
    /// </summary>
    public static event SelectedZoneUpdated OnSelectedZoneUpdated
    {
        add => EventOnSelectedZoneUpdated.Add(value);
        remove => EventOnSelectedZoneUpdated.Remove(value);
    }

    /// <summary>
    /// The currently selected zone, if any.
    /// </summary>
    public static ZoneModel? SelectedZone
    {
        get => _selectedZone;
        set
        {
            ThreadUtil.assertIsGameThread();

            if (ReferenceEquals(_selectedZone, value))
                return;

            ZoneModel? oldSelection = _selectedZone;

            if (oldSelection != null)
                oldSelection.Index = GetIndexQuick(oldSelection);

            if (value != null)
                value.Index = GetIndexQuick(value);

            _selectedZone = value;
            EventOnSelectedZoneUpdated.TryInvoke(value, oldSelection);

            if (value != null
                && UserControl.ActiveTool is ZoneEditorTool
                && value.Component != null
                && DevkitSelectionManager.selection.All(x => x.collider != value.Component)
                && DevkitSelectionManager.data.collider != value.Component.Collider)
            {
                DevkitSelectionManager.add(new DevkitSelection(value.Component.gameObject, value.Component.Collider));
            }
        }
    }

    internal static void Unload()
    {
        foreach (BaseZoneComponent comp in ComponentsPendingUndo)
        {
            if (comp != null)
            {
                Object.Destroy(comp.gameObject);
            }
        }

        ComponentsPendingUndo.Clear();
    }

#endif

    /// <summary>
    /// Add a zone without replicating.
    /// </summary>
    public static ZoneModel AddZoneLocal(Vector3 position, Vector3 spawn, float spawnYaw, string name, string? shortName, ZoneShape shape, CSteamID creator)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();

        int index = LevelZones.ZoneList.Count;
        name ??= index.ToString(CultureInfo.InvariantCulture);

        ZoneModel model = new ZoneModel
        {
            Center = position,
            Spawn = spawn,
            Name = name,
            ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName,
            Shape = shape,
            Creator = creator,
            SpawnYaw = spawnYaw,
            Index = index
        };

#if SERVER
        if (DevkitServerModule.IsEditing)
        {
            model.NetId = ZoneNetIdDatabase.AddZone(index);
        }
#endif

#if CLIENT
        AddComponentIntl(model);
#endif
        LevelZones.ZoneList.Add(model);
        UncreatedZoneEditor.Instance.isDirty = true;

        EventOnZoneAdded.TryInvoke(model);

        return model;
    }
#if CLIENT
    // for copy/pasting and enabling/disabling
    internal static GameObject AddFromModelLocal(ZoneModel model)
    {
        GameObject obj;
        int targetIndex = LevelZones.ZoneList.Count;
        if (model.Component != null)
        {
            ComponentsPendingUndo.Remove(model.Component);

            obj = model.Component.gameObject;
            if (model.Component.GetType() != GetComponentType(model.Shape))
            {
                model.Component.IsRemoved = true;
                model.Component.IsRemovedForShapeChange = false;

                // need to use immediate since we're re-adding another component of the same type immediately
                Object.DestroyImmediate(model.Component);

                obj.SetActive(true);
                AddComponentIntl(obj, model);
            }
            else
            {
                if (model.Component.AddBackAtIndex >= 0)
                {
                    targetIndex = model.Component.AddBackAtIndex;
                }
            }
        }
        else
        {
            AddComponentIntl(model);
            obj = model.Component!.gameObject;
        }
        
        int index = LevelZones.ZoneList.IndexOf(model);
        model.Index = index < 0 ? targetIndex : index;

        if (index >= 0)
            return obj;

        LevelZones.ZoneList.Insert(targetIndex, model);

        foreach (BaseZoneComponent comp in ComponentsPendingUndo)
        {
            if (comp.AddBackAtIndex >= index)
                ++comp.AddBackAtIndex;
        }

        UncreatedZoneEditor.Instance.isDirty = true;

        for (int i = LevelZones.ZoneList.Count - 1; i > index; --i)
        {
            EventOnZoneIndexUpdated.TryInvoke(LevelZones.ZoneList[i], i - 1);
        }

        EventOnZoneAdded.TryInvoke(model);
        return obj;
    }
#endif
    /// <summary>
    /// Remove a zone without replicating.
    /// </summary>
    public static bool RemoveZoneLocal(ZoneModel model)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();

        int index = GetIndexQuick(model);
        if (index < 0)
            return false;

        RemoveZoneLocal(LevelZones.ZoneList[index], index);
        return true;
    }

    /// <summary>
    /// Remove a zone without replicating.
    /// </summary>
    public static void RemoveZoneLocal(int index)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();

        if (index < 0 || index >= LevelZones.ZoneList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Given index does not correlate to a valid entry in LevelZones.LoadedZones.");
        }

        RemoveZoneLocal(LevelZones.ZoneList[index], index);
    }

    private static void RemoveZoneLocal(ZoneModel model, int index)
    {

#if CLIENT
        if (_selectedZone == model)
        {
            SelectedZone = null;
        }
#endif

        model.Index = index;
        LevelZones.ZoneList.RemoveAt(index);
        UncreatedZoneEditor.Instance.isDirty = true;

        foreach (BaseZoneComponent comp in ComponentsPendingUndo)
        {
            if (comp.AddBackAtIndex > index)
                --comp.AddBackAtIndex;
        }

#if CLIENT
        if (model.Component != null)
        {
            model.Component.IsRemoved = true;
            model.Component.IsRemovedForShapeChange = false;
            Object.Destroy(model.Component.gameObject);
        }

        model.Component = null;
#endif

        EventOnZoneRemoved.TryInvoke(model);
        
        for (int i = index; i < LevelZones.ZoneList.Count; ++i)
        {
            ZoneModel movedModel = LevelZones.ZoneList[i];
            movedModel.Index = i;
            EventOnZoneIndexUpdated.TryInvoke(movedModel, i + 1);
        }
    }

#if CLIENT
    internal static void TemporarilyRemoveZoneLocal(ZoneModel model, int index)
    {
        if (_selectedZone == model)
        {
            SelectedZone = null;
        }

        model.Index = index;
        LevelZones.ZoneList.RemoveAt(index);
        UncreatedZoneEditor.Instance.isDirty = true;

        foreach (BaseZoneComponent comp in ComponentsPendingUndo)
        {
            if (comp.AddBackAtIndex > index)
                --comp.AddBackAtIndex;
        }

        if (model.Component != null)
        {
            model.Component.AddBackAtIndex = index;
            ComponentsPendingUndo.Add(model.Component);
        }

        EventOnZoneRemoved.TryInvoke(model);
        
        for (int i = index; i < LevelZones.ZoneList.Count; ++i)
        {
            ZoneModel movedModel = LevelZones.ZoneList[i];
            movedModel.Index = i;
            EventOnZoneIndexUpdated.TryInvoke(movedModel, i + 1);
        }
    }
#endif

    /// <summary>
    /// Change a zone's shape without replicating.
    /// </summary>
    public static bool ChangeShapeLocal(ZoneModel model, ZoneShape shape)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();

        int index = GetIndexQuick(model);
        if (index < 0)
            return false;

        ChangeShapeLocal(LevelZones.ZoneList[index], index, shape);
        return true;
    }

    /// <summary>
    /// Change a zone's shape without replicating.
    /// </summary>
    public static void ChangeShapeLocal(int index, ZoneShape shape)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();

        if (index < 0 || index >= LevelZones.ZoneList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Given index does not correlate to a valid entry in LevelZones.LoadedZones.");
        }

        ChangeShapeLocal(LevelZones.ZoneList[index], index, shape);
    }

    private static void ChangeShapeLocal(ZoneModel model, int index, ZoneShape shape)
    {
        ZoneShape oldShape = model.Shape;
        if (shape == oldShape)
            return;

        model.Index = index;
        model.Shape = shape;
        UncreatedZoneEditor.Instance.isDirty = true;

#if CLIENT
        bool isSelected = SelectedZone == model;
        if (model.Component != null)
        {
            if (isSelected && DevkitSelectionManager.selection.FirstOrDefault(x => x.collider == model.Component.Collider) is { } selected)
            {
                DevkitSelectionManager.remove(selected);
            }

            model.Component.IsRemoved = true;
            model.Component.IsRemovedForShapeChange = true;
            GameObject obj = model.Component.gameObject;

            // need to use immediate since we're re-adding another component of the same type immediately
            Object.DestroyImmediate(model.Component);

            AddComponentIntl(obj, model);
        }
        else
        {
            AddComponentIntl(model);
        }

        if (isSelected && UserControl.ActiveTool is ZoneEditorTool)
        {
            DevkitSelectionManager.add(new DevkitSelection(model.Component!.gameObject, model.Component.Collider));
        }
#endif

        EventOnZoneShapeUpdated.TryInvoke(model, oldShape);
    }

    internal static int GetIndexQuick(ZoneModel model)
    {
        int index = model.Index >= 0 && model.Index < LevelZones.ZoneList.Count && LevelZones.ZoneList[model.Index] == model
            ? model.Index
            : LevelZones.ZoneList.IndexOf(model);

        return index;
    }

    private static void AssertEditor()
    {
        if (!Level.isEditor)
        {
            throw new InvalidOperationException("Not editor.");
        }
    }

#if CLIENT
    internal static Type GetComponentType(ZoneShape shape)
    {
        return shape switch
        {
            ZoneShape.AABB => typeof(RectangleZoneComponent),
            ZoneShape.Cylinder => typeof(CircleZoneComponent),
            ZoneShape.Sphere => typeof(SphereZoneComponent),
            _ => typeof(PolygonZoneComponent)
        };
    }
    internal static void AddComponentIntl(ZoneModel model)
    {
        GameObject obj = new GameObject(model.Name);
        AddComponentIntl(obj, model);
    }
    internal static void AddComponentIntl(GameObject obj, ZoneModel model)
    {
        BaseZoneComponent component = (BaseZoneComponent)obj.AddComponent(GetComponentType(model.Shape));

        component.Init(model);
        model.Component = component;
    }
#endif

}

public delegate void ZoneAdded(ZoneModel model);
public delegate void ZoneRemoved(ZoneModel model);
public delegate void ZoneIndexUpdated(ZoneModel model, int oldIndex);
public delegate void ZoneShapeUpdated(ZoneModel model, ZoneShape oldShape);
public delegate void ZoneDimensionsUpdated(ZoneModel model);
public delegate void SelectedZoneUpdated(ZoneModel? newSelection, ZoneModel? oldSelection);