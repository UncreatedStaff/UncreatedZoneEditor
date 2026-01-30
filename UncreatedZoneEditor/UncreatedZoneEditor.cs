using System;
using System.IO;
using System.Reflection;
using DanielWillett.UITools.API;
using SDG.Framework.Devkit;
using Uncreated.ZoneEditor.Caches;
using Uncreated.ZoneEditor.Multiplayer;
using Uncreated.ZoneEditor.VehicleBays;
#if CLIENT
using System.Collections.Generic;
using DanielWillett.UITools.Util;
#endif

namespace Uncreated.ZoneEditor;

[PermissionPrefix("uncreated.zones")]
public class UncreatedZoneEditor : Plugin<UncreatedZoneEditorConfig>, IDirtyable
{
    public static class Permissions
    {
        public static readonly PermissionLeaf EditZones = new PermissionLeaf("uncreated.zones::level.zones.edit");
    }


#nullable disable

    private CacheDevkitNodeSystem _cacheSystem;
    private VehicleBayDevkitNodeSystem _vehicleBaySystem;
    private VehicleBaySignDevkitNodeSystem _vehicleBaySignSystem;

    /// <summary>
    /// The singleton instance of the <see cref="UncreatedZoneEditor"/> plugin.
    /// </summary>
    public static UncreatedZoneEditor Instance { get; private set; }

#if CLIENT
    /// <summary>
    /// Bundle at Caches/CacheModelBundle.unity3d.
    /// </summary>
    public Bundle CacheBundle { get; private set; }

    /// <summary>
    /// Bundle at VehicleBays/VehicleAssetsBundle.unity3d.
    /// </summary>
    public Bundle VehicleBundle { get; private set; }
#endif

#nullable restore
    protected override LocalDatDictionary DefaultLocalization => new LocalDatDictionary
    {
        { "LoadText", "Loaded {0} v{1} by {2}." },
        { "UnloadText", "Unloaded {0} v{1} by {2}." },
        { "TooManyZones", "There can not be more than {0} zones in the level." },
        { "TooManyZoneAnchors", "There can not be more than {0} anchors in a single zone." },
        { "ZoneToolButton", "Zone Editor" },
        { "ZoneToolButtonTooltip", "Tool used to edit zones for Uncreated Warfare." },
        { "ZoneMapperButton", "Zone Mapper" },
        { "ZoneMapperButtonTooltip", "Tool used to view and edit zones on the map and their adjacencies." },
        { "CreateZoneNoName", "Zones must have a non-whitespace name." },
        
        { "ShapeAABB", "Rectangle" },
        { "ShapeCylinder", "Circle" },
        { "ShapeSphere", "Sphere" },
        { "ShapePolygon", "Polygon" },

        { "TypeFlag", "Capture Flag" },
        { "TypeMainBase", "Main Base" },
        { "TypeAntiMainCampArea", "Anti-Maincamp Zone" },
        { "TypeLobby", "Lobby" },
        { "TypeOther", "Misc." },
        { "TypeWarRoom", "War Room" },

        { "ShapeField", "Shape" },
        { "ShapeTooltip", "Shape of the border of the zone." },

        { "TypeField", "Type" },
        { "TypeTooltip", "Type or use case of the zone." },

        { "FactionField", "Faction ID" },
        { "FactionTooltip", "The ID of the faction this zone is related to." },

        { "NameField", "Display Name" },
        { "NameTooltip", "Display name of the zone." },

        { "ShortNameField", "Short Name" },
        { "ShortNameTooltip", "Shorter version of Name." },

        { "SetAsPrimaryButton", "Set as Primary" },
        { "SetAsPrimaryTooltip", "Marks this part of a zone cluster as the primary zone." },

        { "EditSpawnButton", "Edit Spawnpoint" },
        { "EditSpawnButtonTooltip", "Edit the point at which players teleporting to this zone spawn at." },

        { "MinHeightField", "Minimum Height" },
        { "MinHeightTooltip", "The minimum Y value of the zone's effect." },
        { "MinHeightInfinityTooltip", "Adds no limit to the minimum height." },
        { "MinHeightInfinityToggle", "Infinite" },

        { "MaxHeightField", "Maximum Height" },
        { "MaxHeightTooltip", "The maximum Y value of the zone's effect." },
        { "MaxHeightInfinityTooltip", "Adds no limit to the maximum height." },
        { "MaxHeightInfinityToggle", "Infinite" },

        { "HideOthersToggle", "Hide Deselected Zones" },
        { "HideOthersToggleTooltip", "Hides all zones other than the one you're editing." },

        { "EditPolygonButton", "Edit Vertices" },
        { "EditPolygonTooltip", "Edit vertex locations from a top-down view." },
        { "StopEditPolygonButton", "Exit" },
        { "EditPolygonNotSelected", "Must have exactly 1 polygon selected." },

        { "PolygonIntersectsItself", "Modifying the polygon will\ncause it to intersect itself." },
        { "ZoneToolHint", "[SHIFT] Snap to lines | [CTRL] Snap to grid\n[E] Add | Hold [L-Click] to select." },
        { "NearClipHint", "View clip height: {0}." },
        { "SelectedUpstreamWeightHint", "Weight: {0}." },
        { "NotOneSelectionHint", "There must be exactly one zone selected to add grid objects." },
        { "NonPowerGridObjectHint", "Only powered objects can be selected." },

        { "MapperWeightField", "Weight" },
        { "MapperWeightTooltip", "The relative chance this relation will be chosen." },

        { "ButtonCacheNode", "Insurgency Cache" },
        { "ButtonVehicleBayNode", "Vehicle Bay" },
        { "ButtonVehicleBaySignNode", "Vehicle Bay Sign" },

        { "SignLinked", "Sign linked to bay {0}." },
        { "SignUnlinked", "Sign unlinked from bay {0}." },
        { "SignAlreadyUnlinked", "Sign is already unlinked." },
        { "SignAlreadyUnlinkedHit", "Sign is already unlinked,\nadd an ID to the bay." },
        { "SignLinkNoTarget", "Middle click on a vehicle bay\nto link it to this sign." },
        { "BayLinkNoTarget", "Middle click on a vehicle bay sign\nto link it to this bay." },
        { "BayLinkNoID", "ID required to link." }
    };

#if DEBUG
    public override bool DeveloperMode => true;
#else
    public override bool DeveloperMode => false;
#endif


    private bool _isDirty;

    /// <summary>
    /// If saving needs to happen before quitting.
    /// </summary>
    /// <remarks>Part of the <see cref="IDirtyable"/> interface for <see cref="DirtyManager"/>.</remarks>
    public bool isDirty
    {
        get => _isDirty;
        set
        {
            if (isDirty == value) return;
            _isDirty = value;
            if (value) DirtyManager.markDirty(this);
            else DirtyManager.markClean(this);
        }
    }

    protected override void Load()
    {
        Instance = this;

#if CLIENT
        UIAccessor.OnInitializingUIInfo += RegisterUITypes;
#endif

        AssemblyName assemblyName = Assembly.Assembly.GetName();
        this.LogInfo(Translations.Translate("LoadText", assemblyName.Name, assemblyName.Version.ToString(3), "DanielWillett"));

        ZoneNetIdDatabase.Init();

#if CLIENT

        CacheBundle = LoadBundle("Uncreated.ZoneEditor.Caches.CacheModelBundle.unity3d", "Uncreated.ZoneEditor.Caches");
        VehicleBundle = LoadBundle("Uncreated.ZoneEditor.VehicleBays.VehicleAssetsBundle.unity3d", "Uncreated.ZoneEditor.VehicleBays");

#endif

        _cacheSystem = new CacheDevkitNodeSystem();
        _vehicleBaySystem = new VehicleBayDevkitNodeSystem();
        _vehicleBaySignSystem = new VehicleBaySignDevkitNodeSystem();
        Level.onPrePreLevelLoaded += OnLevelLoading;
    }

    private void OnLevelLoading(int level)
    {
        _cacheSystem.Load();
        _vehicleBaySystem.Load();
        _vehicleBaySignSystem.Load();
    }

    protected override void Unload()
    {
        ZoneNetIdDatabase.Shutdown();

        Instance = null;

        AssemblyName assemblyName = Assembly.Assembly.GetName();
        this.LogInfo(Translations.Translate("UnloadText", assemblyName.Name, assemblyName.Version.ToString(3), "DanielWillett"));

        LevelZones.Unload();

#if CLIENT
        CacheBundle?.unload();
        CacheBundle = null;

        VehicleBundle?.unload();
        VehicleBundle = null;
#endif

        Level.onPrePreLevelLoaded -= OnLevelLoading;
        _cacheSystem.Dispose();
        _vehicleBaySystem.Dispose();
        _vehicleBaySignSystem.Dispose();
    }

#if CLIENT

    private Bundle? LoadBundle(string manifestResource, string name)
    {
        string outFileName = Path.Combine(Path.GetTempPath(), name + ".unity3d");
        Stream? stream = Assembly.Assembly.GetManifestResourceStream(manifestResource);
        if (stream == null)
        {
            this.LogError($"Bundle {name.Format(false)} not found at {manifestResource.Format()}.");
            return null;
        }

        using (stream)
        using (FileStream fs = new FileStream(outFileName, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.CopyTo(fs);
        }

        return new Bundle(outFileName, false, nameOverride: name);
    }


    private static void RegisterUITypes(Dictionary<Type, UITypeInfo> typeInfo)
    {
        UIAccessor.OnInitializingUIInfo -= RegisterUITypes;

        // todo
        // typeInfo.Add(typeof(ZoneEditorUI), new UITypeInfo(typeof(ZoneEditorUI), hasActiveMember: true)
        // {
        //     IsStaticUI = false,
        //     CustomEmitter = (_, il) =>
        //     {
        //         MethodInfo loadMethod = typeof(ZoneEditorUI).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetMethod;
        //         il.Emit(OpCodes.Call, loadMethod);
        //     }
        // });
    }
#endif

    public void save()
    {
        LevelZones.SaveZones();
    }
}