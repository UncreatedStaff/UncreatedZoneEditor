﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor;

/// <summary>
/// Read-only API for a level's zones.
/// </summary>
public static class LevelZones
{
    internal static readonly List<ZoneModel> ZoneList = [];
    private static ZoneJsonConfig? _zoneList;

    /// <summary>
    /// Path to the file that stores zones.
    /// </summary>
    public static string FilePath => Path.GetFullPath(Level.info.path + "/Uncreated/zones.json");

    /// <summary>
    /// List of all loaded zones.
    /// </summary>
    public static IReadOnlyList<ZoneModel> LoadedZones { get; private set; }


    static LevelZones()
    {
        LoadedZones = new ReadOnlyCollection<ZoneModel>(ZoneList);

        Level.onLevelLoaded += OnLevelLoaded;
    }

    [return: NotNullIfNotNull(nameof(model))]
    [System.Diagnostics.Contracts.Pure]
    public static ZoneModel? GetPrimary(ZoneModel? model)
    {
        if (model == null)
            return null;

        if (model.IsPrimary)
            return model;

        int index = GetIndexQuick(model);
        if (index < 0)
            return model;

        string name = model.Name;
        for (int i = index - 1; i >= 0; --i)
        {
            ZoneModel zone = ZoneList[i];
            if (zone.IsPrimary && zone.Name.Equals(name, StringComparison.Ordinal))
                return zone;
        }

        for (int i = index + 1; i < ZoneList.Count; ++i)
        {
            ZoneModel zone = ZoneList[i];
            if (zone.IsPrimary && zone.Name.Equals(name, StringComparison.Ordinal))
                return zone;
        }

        return model;
    }

    internal static int GetIndexQuick(ZoneModel model)
    {
        int index = model.Index >= 0 && model.Index < LevelZones.ZoneList.Count && LevelZones.ZoneList[model.Index] == model
            ? model.Index
            : LevelZones.ZoneList.IndexOf(model);

        return index;
    }

    internal static void Unload()
    {
        Level.onLevelLoaded -= OnLevelLoaded;
#if CLIENT
        EditorZones.Unload();
#endif
    }

    private static void OnLevelLoaded(int level)
    {
        if (level != Level.BUILD_INDEX_GAME)
            return;

        ReadZones();
    }

    public static void SaveZones()
    {
        ThreadUtil.assertIsGameThread();

        if (!Level.isEditor)
            throw new InvalidOperationException("Only available when editing.");

        string path = FilePath;
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        if (_zoneList == null || !_zoneList.File.Equals(path))
        {
            _zoneList = new ZoneJsonConfig(path) { ReadOnlyReloading = false };
        }

        ZoneJsonList newConfig = _zoneList.Configuration ?? new ZoneJsonList();

        newConfig.Zones ??= [];
        newConfig.Zones.Clear();
        newConfig.Zones.AddRange(ZoneList);

        _zoneList.Configuration = newConfig;
        _zoneList.SaveConfig();

        UncreatedZoneEditor.Instance.LogInfo($"Saved {newConfig.Zones.Count.Format()} zone(s).");
    }

    public static void ReadZones()
    {
        ThreadUtil.assertIsGameThread();

        string path = FilePath;
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        if (_zoneList == null || !_zoneList.File.Equals(path))
        {
            _zoneList = new ZoneJsonConfig(path) { ReadOnlyReloading = !Level.isEditor };
        }

        _zoneList.ReloadConfig();
        _zoneList.Configuration.Zones ??= [ ];

#if CLIENT
        foreach (ZoneModel model in ZoneList)
        {
            if (model.Component == null)
                continue;
            
            Object.Destroy(model.Component.gameObject);
            model.Component = null;
        }
#endif

        ZoneList.Clear();

        foreach (ZoneModel model in _zoneList.Configuration.Zones)
        {
            model.Index = ZoneList.Count;

#if CLIENT
            if (Level.isEditor)
            {
                EditorZones.AddComponentIntl(model);
            }
#endif

            ZoneList.Add(model);
        }

        bool anyChanges = false;

        for (int i = 0; i < ZoneList.Count; ++i)
        {
            ZoneModel zone = ZoneList[i];

            // fixup upstream zones
            for (int j = zone.UpstreamZones.Count - 1; j >= 0; --j)
            {
                UpstreamZone upstreamZone = zone.UpstreamZones[j];
                string zoneName = upstreamZone.ZoneName;
                if (string.IsNullOrWhiteSpace(zoneName) || zoneName.Equals(zone.Name, StringComparison.Ordinal))
                {
                    UncreatedZoneEditor.Instance.LogWarning($"Removed empty or self upstream zone from zone {zone.Name.Format(false)}.");
                    zone.UpstreamZones.RemoveAt(j);
                    anyChanges = true;
                    continue;
                }

                bool foundAny = false;
                for (int k = 0; k < ZoneList.Count; ++k)
                {
                    if (k == i || !zoneName.Equals(ZoneList[k].Name, StringComparison.Ordinal))
                        continue;

                    foundAny = true;
                    break;
                }

                if (!foundAny)
                {
                    zone.UpstreamZones.RemoveAt(j);
                    UncreatedZoneEditor.Instance.LogWarning($"Removed unknown upstream zone {zoneName.Format()} from zone {zone.Name.Format(false)}.");
                    anyChanges = true;
                }

                if (upstreamZone.Weight > 0f)
                    continue;

                UncreatedZoneEditor.Instance.LogWarning($"Removed 0 weight upstream zone {zoneName.Format(false)} from zone {zone.Name.Format(false)}.");
                zone.UpstreamZones.RemoveAt(j);
                anyChanges = true;
            }

            // fixup name clusters
            if (string.IsNullOrWhiteSpace(zone.Name))
            {
                zone.Name = Guid.NewGuid().ToString("N");
                UncreatedZoneEditor.Instance.LogWarning($"Added default name to zone with missing name: {zone.Name.Format()}.");
                anyChanges = true;
            }

            string name = zone.Name;
            bool anyPrimary = zone.IsPrimary;
            for (int j = 0; j < ZoneList.Count; ++j)
            {
                ZoneModel zone2 = ZoneList[j];
                if (i == j || !zone2.Name.Equals(name, StringComparison.Ordinal) || !zone2.IsPrimary)
                    continue;
                
                if (anyPrimary)
                {
                    UncreatedZoneEditor.Instance.LogWarning($"Removed duplicate primary zone in cluster {name.Format(false)}.");
                    zone2.IsPrimary = false;
                    anyChanges = true;
                }
                else
                {
                    anyPrimary = true;
                }
            }

            if (anyPrimary)
                continue;

            UncreatedZoneEditor.Instance.LogWarning($"Set first zone in cluster {name.Format(false)} as the primary zone.");
            zone.IsPrimary = true;
            anyChanges = true;
        }

        if (Level.isEditor)
        {
            anyChanges |= EditorZones.FixInvalidGridObjects();
        }

        if (!anyChanges)
            return;

        if (!Level.isEditor)
        {
            UncreatedZoneEditor.Instance.LogWarning("Open the map in the editor and save to apply the above changes.");
        }
        else
        {
            UncreatedZoneEditor.Instance.LogInfo("Save the map to apply the above changes.");
            UncreatedZoneEditor.Instance.isDirty = true;
        }
    }
}