using HarmonyLib;

using PaintBrush.Brush;

namespace PaintBrush.Patches;

/// <summary>
/// Lets the brush tools into the menu's grid: see <see cref="BrushPrefabs.SetToolsAsRepair"/>.
/// </summary>
[HarmonyPatch(typeof(BuildUi), "UpdatePieceButtons")]
internal static class BuildUi_UpdatePieceButtons_Patch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        BrushPrefabs.SetToolsAsRepair(false);
    }

    [HarmonyFinalizer]
    private static void Finalizer()
    {
        BrushPrefabs.SetToolsAsRepair(true);
    }
}
