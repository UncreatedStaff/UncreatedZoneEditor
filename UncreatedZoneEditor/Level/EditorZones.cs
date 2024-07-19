using System;
using System.Globalization;
using Uncreated.ZoneEditor.Data;
#if CLIENT
using System.Collections.Generic;
using System.Linq;
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
    private static readonly CachedMulticastEvent<ZoneShapeUpdated> EventOnZoneShapeUpdated = new CachedMulticastEvent<ZoneShapeUpdated>(typeof(EditorZones), nameof(OnZoneShapeUpdated));
    private static readonly CachedMulticastEvent<ZoneNameUpdated> EventOnZoneNameUpdated = new CachedMulticastEvent<ZoneNameUpdated>(typeof(EditorZones), nameof(OnZoneNameUpdated));
    private static readonly CachedMulticastEvent<ZoneShortNameUpdated> EventOnZoneShortNameUpdated = new CachedMulticastEvent<ZoneShortNameUpdated>(typeof(EditorZones), nameof(OnZoneShortNameUpdated));
    private static readonly CachedMulticastEvent<ZonePrimaryUpdated> EventOnZonePrimaryUpdated = new CachedMulticastEvent<ZonePrimaryUpdated>(typeof(EditorZones), nameof(OnZonePrimaryUpdated));
#if CLIENT
    private static readonly CachedMulticastEvent<ZoneSelectionUpdated> EventOnZoneSelectionUpdated = new CachedMulticastEvent<ZoneSelectionUpdated>(typeof(EditorZones), nameof(OnZoneSelectionUpdated));
    internal static readonly List<BaseZoneComponent> ComponentsPendingUndo = new List<BaseZoneComponent>(4);
#endif

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
    
    /// <summary>
    /// Invoked when a zone's name is changed locally.
    /// </summary>
    public static event ZoneNameUpdated OnZoneNameUpdated
    {
        add => EventOnZoneNameUpdated.Add(value);
        remove => EventOnZoneNameUpdated.Remove(value);
    }
    
    /// <summary>
    /// Invoked when a zone's short name is changed locally.
    /// </summary>
    public static event ZoneShortNameUpdated OnZoneShortNameUpdated
    {
        add => EventOnZoneShortNameUpdated.Add(value);
        remove => EventOnZoneShortNameUpdated.Remove(value);
    }
    
    /// <summary>
    /// Invoked when a zone's primary/secondary setting is updated.
    /// </summary>
    public static event ZonePrimaryUpdated OnZonePrimaryUpdated
    {
        add => EventOnZonePrimaryUpdated.Add(value);
        remove => EventOnZonePrimaryUpdated.Remove(value);
    }

#if CLIENT

    /// <summary>
    /// Invoked when a zone is selected or deselected.
    /// </summary>
    public static event ZoneSelectionUpdated OnZoneSelectionUpdated
    {
        add => EventOnZoneSelectionUpdated.Add(value);
        remove => EventOnZoneSelectionUpdated.Remove(value);
    }

    /// <summary>
    /// Enumerates through all selected zones.
    /// </summary>
    public static IEnumerable<ZoneModel> EnumerateSelectedZones()
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();

        return DevkitSelectionManager.selection
            .Select(sel => sel.gameObject.TryGetComponent(out BaseZoneComponent comp) ? comp : null)
            .Where(comp => comp != null)
            .Select(comp => comp!.Model);
    }

    /// <summary>
    /// Check if a zone is selected.
    /// </summary>
    public static bool IsZoneSelected(ZoneModel model)
    {
        return model.Component != null
               && UserControl.ActiveTool is ZoneEditorTool
               && DevkitSelectionManager.selection.Any(x => x.collider == model.Component.Collider);
    }

    /// <summary>
    /// Get the selection object for a zone if it's selected.
    /// </summary>
    public static DevkitSelection? GetZoneSelection(ZoneModel model)
    {
        if (model.Component == null || UserControl.ActiveTool is not ZoneEditorTool)
        {
            return null;
        }

        return DevkitSelectionManager.selection.FirstOrDefault(x => x.collider == model.Component.Collider);

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

    internal static void BroadcastZoneSelected(ZoneModel zone, bool wasSelected)
    {
        EventOnZoneSelectionUpdated.TryInvoke(zone, wasSelected);
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
            Index = index,
            IsPrimary = true
        };

        for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
        {
            if (!LevelZones.ZoneList[i].Name.Equals(name, StringComparison.Ordinal))
                continue;

            model.IsPrimary = false;
            break;
        }

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

        string name = model.Name;
        model.IsPrimary = true;
        for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
        {
            if (!LevelZones.ZoneList[i].Name.Equals(name, StringComparison.Ordinal))
                continue;

            model.IsPrimary = false;
            break;
        }

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
        if (GetZoneSelection(model) is { } selected)
        {
            DevkitSelectionManager.remove(selected);
        }
#endif
        if (model.IsPrimary)
        {
            // re-assign primary | todo this needs to be controlled by the server
            string name = model.Name;
            for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
            {
                if (i == index)
                    continue;

                ZoneModel zone2 = LevelZones.ZoneList[i];
                if (!zone2.Name.Equals(name, StringComparison.Ordinal))
                    continue;

                zone2.IsPrimary = true;
                model.IsPrimary = false;
                EventOnZonePrimaryUpdated.TryInvoke(model);
                EventOnZonePrimaryUpdated.TryInvoke(zone2);
                break;
            }
        }

        model.Index = index;
        LevelZones.ZoneList.RemoveAt(index);

        UncreatedZoneEditor.Instance.isDirty = true;

#if CLIENT
        foreach (BaseZoneComponent comp in ComponentsPendingUndo)
        {
            if (comp.AddBackAtIndex > index)
                --comp.AddBackAtIndex;
        }

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

    #region Setters
    /// <summary>
    /// Change a zone's name locally.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or whitespace.</exception>
    public static void SetZoneNameLocal(int index, string name)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();
        AssertValidIndex(index);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be whitespace or null.");

        ZoneModel zone = LevelZones.ZoneList[index];

        string oldName = zone.Name;
        if (string.Equals(oldName, name, StringComparison.Ordinal))
        {
            return;
        }

        zone.Name = name;

        bool oldPrimary = zone.IsPrimary;

        // check for existing primaries
        bool foundAny = false;
        for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
        {
            ZoneModel zone2 = LevelZones.ZoneList[i];
            if (i == index || !zone2.Name.Equals(name, StringComparison.Ordinal))
                continue;

            foundAny = true;
            break;
        }

        if (oldPrimary)
        {
            zone.IsPrimary = false;
            EventOnZonePrimaryUpdated.TryInvoke(zone);
        }

        if (oldPrimary)
        {
            // assign a new primary
            for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
            {
                if (i == index)
                    continue;

                ZoneModel zone2 = LevelZones.ZoneList[i];
                if (!zone2.Name.Equals(oldName, StringComparison.Ordinal))
                    continue;

                zone2.IsPrimary = true;
                EventOnZonePrimaryUpdated.TryInvoke(zone2);
                break;
            }
        }

        if (!foundAny)
        {
            zone.IsPrimary = true;
            EventOnZonePrimaryUpdated.TryInvoke(zone);
        }

        EventOnZoneNameUpdated.TryInvoke(zone, oldName);
        UncreatedZoneEditor.Instance.isDirty = true;
    }

    /// <summary>
    /// Change a zone's short name locally, along with all other zones in its name cluster.
    /// </summary>
    /// <remarks>Any empty or whitespace-only short name will be saved as <see langword="null"/>.</remarks>
    public static void SetZoneShortNameLocal(int index, string? shortName)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();
        AssertValidIndex(index);

        if (string.IsNullOrWhiteSpace(shortName))
            shortName = null;

        ZoneModel zone = LevelZones.ZoneList[index];

        string? oldName = zone.ShortName;
        if (string.Equals(shortName, oldName, StringComparison.Ordinal))
        {
            return;
        }

        zone.ShortName = shortName;
        EventOnZoneShortNameUpdated.TryInvoke(zone, oldName);

        for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
        {
            ZoneModel zone2 = LevelZones.ZoneList[i];
            if (i == index || !zone2.Name.Equals(zone.Name, StringComparison.Ordinal))
                continue;

            zone2.ShortName = shortName;
            EventOnZoneShortNameUpdated.TryInvoke(zone2, oldName);
        }

        UncreatedZoneEditor.Instance.isDirty = true;
    }

    /// <summary>
    /// Mark a zone as the primary zone out of it's name cluster.
    /// </summary>
    public static void MarkZoneAsPrimary(int index)
    {
        ThreadUtil.assertIsGameThread();
        AssertEditor();
        AssertValidIndex(index);
        ZoneModel zone = LevelZones.ZoneList[index];
        string name = zone.Name;

        for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
        {
            if (i == index)
                continue;

            ZoneModel zone2 = LevelZones.ZoneList[i];
            if (!zone2.Name.Equals(name, StringComparison.Ordinal) || !zone2.IsPrimary)
                continue;

            zone2.IsPrimary = false;
            EventOnZonePrimaryUpdated.TryInvoke(zone2);
        }

        zone.IsPrimary = true;
        EventOnZonePrimaryUpdated.TryInvoke(zone);
    }
    #endregion

    private static void AssertValidIndex(int index)
    {
        if (index < 0 || index >= LevelZones.ZoneList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Given index does not correlate to a valid entry in LevelZones.LoadedZones.");
        }
    }

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
        AssertValidIndex(index);
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
        DevkitSelection? selection = GetZoneSelection(model);
        if (selection != null)
        {
            DevkitSelectionManager.remove(selection);
        }

        if (model.Component != null)
        {
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

        if (selection != null && UserControl.ActiveTool is ZoneEditorTool)
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
public delegate void ZoneNameUpdated(ZoneModel model, string oldName);
public delegate void ZoneShortNameUpdated(ZoneModel model, string? oldShortName);
public delegate void ZonePrimaryUpdated(ZoneModel model);
public delegate void ZoneDimensionsUpdated(ZoneModel model);
#if CLIENT
public delegate void ZoneSelectionUpdated(ZoneModel selectedOrDeselected, bool wasSelected);
#endif