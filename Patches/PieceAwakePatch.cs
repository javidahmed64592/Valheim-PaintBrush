using HarmonyLib;

using PaintBrush.Painting;

namespace PaintBrush.Patches;

/// <summary>
/// Shows a piece's saved paint as soon as it loads.
/// </summary>
[HarmonyPatch(typeof(Piece), "Awake")]
internal static class Piece_Awake_Patch
{
    [HarmonyPostfix]
    private static void Postfix(Piece __instance)
    {
        PaintedPieces.OnPieceAwake(__instance);
    }
}
