using System;
using System.Reflection;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Multiplayer;
#if CLIENT
using System.Collections.Generic;
using System.Reflection.Emit;
using Uncreated.ZoneEditor.UI;
#endif

namespace Uncreated.ZoneEditor;

[PermissionPrefix("uncreated.zones")]
public class UncreatedZoneEditor : Plugin<UncreatedZoneEditorConfig>
{
    public static class Permissions
    {
        public static readonly PermissionLeaf EditZones = new PermissionLeaf("uncreated.zones::level.zones.edit");
    }


#nullable disable

    /// <summary>
    /// The singleton instance of the <see cref="UncreatedZoneEditor"/> plugin.
    /// </summary>
    public static UncreatedZoneEditor Instance { get; private set; }

#nullable restore
    protected override LocalDatDictionary DefaultLocalization => new LocalDatDictionary
    {
        { "LoadText", "Loaded {0} v{1} by {2}." },
        { "UnloadText", "Unloaded {0} v{1} by {2}." },
        { "TooManyZones", "There can not be more than {0} zones in the level." },
        { "TooManyZoneAnchors", "There can not be more than {0} anchors in a single zone." },
        { "ZoneToolButton", "Zone Editor" },
        { "ZoneToolButtonTooltip", "Tool used to edit zones for Uncreated Warfare." }
    };

#if DEBUG
    public override bool DeveloperMode => true;
#else
    public override bool DeveloperMode => false;
#endif

    protected override void Load()
    {
        Instance = this;

#if CLIENT
        UIAccessTools.OnInitializingUIInfo += RegisterUITypes;
#endif

        AssemblyName assemblyName = Assembly.Assembly.GetName();
        this.LogInfo(Translations.Translate("LoadText", assemblyName.Name, assemblyName.Version.ToString(3), "DanielWillett"));

        this.LogInfo(Configuration.HelloProperty ?? "Hello config is null.");

        ZoneNetIdDatabase.Init();
    }

    protected override void Unload()
    {
        ZoneNetIdDatabase.Shutdown();

        Instance = null;

        AssemblyName assemblyName = Assembly.Assembly.GetName();
        this.LogInfo(Translations.Translate("UnloadText", assemblyName.Name, assemblyName.Version.ToString(3), "DanielWillett"));

        ((IDisposable)EditorZones.Instance).Dispose();
    }

#if CLIENT
    private static void RegisterUITypes(Dictionary<Type, UITypeInfo> typeInfo)
    {
        UIAccessTools.OnInitializingUIInfo -= RegisterUITypes;

        typeInfo.Add(typeof(ZoneEditorUI), new UITypeInfo(typeof(ZoneEditorUI), hasActiveMember: true)
        {
            IsStaticUI = false,
            CustomEmitter = (_, il) =>
            {
                MethodInfo loadMethod = typeof(ZoneEditorUI).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetMethod;
                il.Emit(OpCodes.Call, loadMethod);
            }
        });
    }
#endif
}