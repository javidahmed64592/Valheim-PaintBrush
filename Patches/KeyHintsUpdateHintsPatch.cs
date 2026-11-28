using HarmonyLib;

using PaintBrush.Core;
using PaintBrush.UI;

namespace PaintBrush.Patches;

[HarmonyPatch(typeof(KeyHints), "UpdateHints")]
[Feature(Feature)]
internal static class KeyHints_UpdateHints_Patch
{
    /// <summary>Without it, the game's own build hints are shown while the brush is out.</summary>
    public const string Feature = "Key hints";

    [HarmonyPostfix]
    private static void Postfix(KeyHints __instance)
    {
        BrushKeyHints.AfterUpdateHints(__instance);
        MenuKeyHints.AfterUpdateHints(__instance);
    }
}
