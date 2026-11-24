using System.Collections.Generic;

using HarmonyLib;

using PaintBrush.Brush;

namespace PaintBrush.Patches;

/// <summary>
/// Includes the phantom brush's piece table in inventory scans, so UpdateKnownRecipesList finds the brush's tools
/// without a real brush.
/// </summary>
[HarmonyPatch(typeof(Inventory), nameof(Inventory.GetAllPieceTables))]
internal static class Inventory_GetAllPieceTables_Patch
{
    [HarmonyPostfix]
    private static void Postfix(List<PieceTable> tables)
    {
        PieceTable table = PhantomBrush.Item?.m_shared?.m_buildPieces;
        if (table != null && !tables.Contains(table))
        {
            tables.Add(table);
        }
    }
}
