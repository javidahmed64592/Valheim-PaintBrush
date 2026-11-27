using HarmonyLib;

using PaintBrush.Brush;
using PaintBrush.Core;

namespace PaintBrush.Patches;

/// <summary>
/// Skips the support tint on the piece being painted, so the paint preview shows its real colour.
/// </summary>
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Highlight))]
[Feature(Feature)]
internal static class WearNTear_Highlight_Patch
{
    /// <summary>Without it, the piece being painted shows the game's support tint over the preview.</summary>
    public const string Feature = "True colour preview";

    [HarmonyPrefix]
    private static bool Prefix(WearNTear __instance)
    {
        return !PaintPreview.HidesHighlight(__instance);
    }
}
