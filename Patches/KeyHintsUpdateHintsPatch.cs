using HarmonyLib;

using PaintBrush.UI;

namespace PaintBrush.Patches;

[HarmonyPatch(typeof(KeyHints), "UpdateHints")]
internal static class KeyHints_UpdateHints_Patch
{
    [HarmonyPostfix]
    private static void Postfix(KeyHints __instance)
    {
        BrushKeyHints.AfterUpdateHints(__instance);
        MenuKeyHints.AfterUpdateHints(__instance);
    }
}
