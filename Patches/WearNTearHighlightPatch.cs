using HarmonyLib;

using PaintBrush.Brush;

namespace PaintBrush.Patches;

/// <summary>
/// Skips the support tint on the piece being painted, so the paint preview shows its real colour.
/// </summary>
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Highlight))]
internal static class WearNTear_Highlight_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(WearNTear __instance)
    {
        return !PaintPreview.HidesHighlight(__instance);
    }
}
