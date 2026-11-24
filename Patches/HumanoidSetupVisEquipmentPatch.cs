using HarmonyLib;

using PaintBrush.Brush;

namespace PaintBrush.Patches;

/// <summary>
/// SetupVisEquipment throws for items that didn't come through the inventory, so the phantom brush is hidden from
/// it and put in the player's hand directly afterwards (as BetterHammer and BetterHoe do).
/// </summary>
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
internal static class Humanoid_SetupVisEquipment_Patch
{
    [HarmonyPrefix]
    private static void Prefix(Humanoid __instance, out ItemDrop.ItemData __state)
    {
        ItemDrop.ItemData brush = PhantomBrush.Item;
        __state = null;
        if (brush != null && __instance.GetRightItem() == brush)
        {
            __state = brush;
            __instance.m_rightItem = null;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(Humanoid __instance, ItemDrop.ItemData __state)
    {
        if (__state == null)
        {
            return;
        }

        __instance.m_rightItem = __state;
        if (__instance is Player player)
        {
            player.m_visEquipment?.SetRightItem(__state.m_dropPrefab.name.GetStableHashCode(), __state.m_quality);
        }
    }
}
