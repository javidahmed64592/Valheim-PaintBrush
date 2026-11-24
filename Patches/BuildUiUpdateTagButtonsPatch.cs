using HarmonyLib;

using PaintBrush.UI;

namespace PaintBrush.Patches;

/// <summary>
/// Runs when the piece menu opens or switches tab, after vanilla (and BetterHoe, which hides the line under the
/// tabs for tools without tabs) has set up the tab row.
/// </summary>
[HarmonyPatch(typeof(BuildUi), "UpdateTagButtons")]
internal static class BuildUi_UpdateTagButtons_Patch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    private static void Postfix(BuildUi __instance)
    {
        MenuTitle.AfterUpdateTagButtons(__instance);
    }
}
