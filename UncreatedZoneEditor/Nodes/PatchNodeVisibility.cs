using HarmonyLib;
using SDG.Framework.Devkit;
using Uncreated.ZoneEditor.Caches;
using Uncreated.ZoneEditor.VehicleBays;

namespace Uncreated.ZoneEditor.Nodes;

[HarmonyPatch]
internal static class PatchNodeVisibility
{
    [HarmonyPostfix, HarmonyPatch(typeof(SpawnpointSystemV2), nameof(SpawnpointSystemV2.IsVisible), MethodType.Setter)]
    [UsedImplicitly]
    private static void IsVisiblePostfix(bool value)
    {
        foreach (CacheDevkitNode node in CacheDevkitNodeSystem.Get().GetAllNodes())
        {
            node.UpdateEditorVisibility();
        }
        foreach (VehicleBayDevkitNode node in VehicleBayDevkitNodeSystem.Get().GetAllNodes())
        {
            node.UpdateEditorVisibility();
        }
        foreach (VehicleBaySignDevkitNode node in VehicleBaySignDevkitNodeSystem.Get().GetAllNodes())
        {
            node.UpdateEditorVisibility();
        }
    }
}
