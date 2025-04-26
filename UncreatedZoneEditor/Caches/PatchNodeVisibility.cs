using HarmonyLib;
using SDG.Framework.Devkit;

namespace Uncreated.ZoneEditor.Caches;

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
    }
}
