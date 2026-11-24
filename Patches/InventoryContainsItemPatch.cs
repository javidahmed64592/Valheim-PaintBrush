using HarmonyLib;

using PaintBrush.Brush;

namespace PaintBrush.Patches;

/// <summary>
/// Lets EquipItem accept the phantom brush although it isn't in the inventory.
/// </summary>
[HarmonyPatch(typeof(Inventory), nameof(Inventory.ContainsItem))]
internal static class Inventory_ContainsItem_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(ItemDrop.ItemData item, ref bool __result)
    {
        if (PhantomBrush.Item != null && item == PhantomBrush.Item)
        {
            __result = true;
            return false;
        }

        return true;
    }
}
