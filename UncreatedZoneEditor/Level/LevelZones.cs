using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    internal static void Unload()
    {
        Level.onLevelLoaded -= OnLevelLoaded;
        EditorZones.Unload();
    }

    private static void OnLevelLoaded(int level)
    {
        if (level == Level.BUILD_INDEX_GAME)
        {
            ReadZones();
        }
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
            _zoneList = new ZoneJsonConfig(path) { ReadOnlyReloading = false };
        }

        _zoneList.ReloadConfig();

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
    }
}
