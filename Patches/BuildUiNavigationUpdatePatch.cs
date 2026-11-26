using HarmonyLib;

using PaintBrush.UI;

using UnityEngine;

namespace PaintBrush.Patches;

/// <summary>
/// Right-click is the game's key for closing the build menu. Over a palette slot or the palette menu it does
/// something else, so the menu stays open. While the game's text box is open over the brush menu (naming a
/// palette), the menu's keys are left alone, so typing doesn't switch tabs and Escape only closes the text box.
/// </summary>
[HarmonyPatch(typeof(BuildUi), "NavigationUpdate")]
internal static class BuildUi_NavigationUpdate_Patch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (!PaintPanel.Visible)
        {
            return true;
        }

        if (TextInput.IsVisible())
        {
            return false;
        }

        if (PaintPanel.UsesRightClick && Input.GetMouseButtonDown(1))
        {
            ZInput.ResetButtonStatus("BuildMenu");
        }

        return true;
    }
}
