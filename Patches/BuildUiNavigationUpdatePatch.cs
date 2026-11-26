using HarmonyLib;

using PaintBrush.UI;

using UnityEngine;

namespace PaintBrush.Patches;

/// <summary>
/// Right-click is the game's key for closing the build menu. Over a palette slot it empties the slot instead, so the
/// menu stays open.
/// </summary>
[HarmonyPatch(typeof(BuildUi), "NavigationUpdate")]
internal static class BuildUi_NavigationUpdate_Patch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (Swatch.RightClickHovered && Input.GetMouseButtonDown(1))
        {
            ZInput.ResetButtonStatus("BuildMenu");
        }
    }
}
