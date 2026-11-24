using HarmonyLib;

using PaintBrush.Painting;

namespace PaintBrush.Patches;

/// <summary>
/// Frees a piece's painted textures when it's removed or unloaded.
/// </summary>
[HarmonyPatch(typeof(Piece), "OnDestroy")]
internal static class Piece_OnDestroy_Patch
{
    [HarmonyPrefix]
    private static void Prefix(Piece __instance)
    {
        PaintedPieces.OnPieceDestroy(__instance);
    }
}
