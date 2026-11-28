using HarmonyLib;

using PaintBrush.Core;
using PaintBrush.UI;

namespace PaintBrush.Patches;

/// <summary>
/// Runs when the piece menu opens or switches tab, after vanilla (and BetterHoe, which hides the line under the
/// tabs for tools without tabs) has set up the tab row.
/// </summary>
[HarmonyPatch(typeof(BuildUi), "UpdateTagButtons")]
[Feature(Feature)]
internal static class BuildUi_UpdateTagButtons_Patch
{
    /// <summary>Without it, the brush menu has no title and shows the Q / E hint for tabs, as vanilla does for a tool without tabs.</summary>
    public const string Feature = "Menu title";

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    private static void Postfix(BuildUi __instance)
    {
        MenuTitle.AfterUpdateTagButtons(__instance);
    }
}
