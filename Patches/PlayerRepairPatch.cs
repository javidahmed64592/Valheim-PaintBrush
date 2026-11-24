using HarmonyLib;

using PaintBrush.Brush;

namespace PaintBrush.Patches;

/// <summary>
/// The brush tools are copies of the hammer's Repair tool, so clicking with one ends up in Repair. This paints or
/// clears instead; Repair runs as usual for every other tool.
/// </summary>
[HarmonyPatch(typeof(Player), "Repair")]
internal static class Player_Repair_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(Player __instance, ItemDrop.ItemData toolItem, Piece repairPiece)
    {
        return !BrushTools.TryUse(__instance, toolItem, repairPiece);
    }
}
