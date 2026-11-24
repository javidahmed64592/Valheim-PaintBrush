using BepInEx.Configuration;

using Jotunn.Configs;
using Jotunn.Managers;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaintBrush.Brush;

/// <summary>
/// A brush that isn't in the inventory: the Equip Paint Brush key puts it in the player's hand (and takes it away
/// again), like BetterHoe's Equip Hoe key. It's patched in where vanilla expects equipped items and piece tables to
/// come from the inventory (see the Inventory and Humanoid patches).
/// </summary>
internal static class PhantomBrush
{
    private const string ButtonName = "PaintBrush_Equip";

    private static ConfigEntry<KeyCode> _key;
    private static bool _knownRecipesRefreshed;

    internal static ItemDrop.ItemData Item { get; private set; }

    private static string ButtonQueryName => ButtonName + "!" + PaintBrushPlugin.PluginGUID;

    public static void BindConfig(ConfigFile config)
    {
        _key = config.Bind("Keybinds", "Equip Paint Brush", KeyCode.O,
            "Puts the paint brush in your hand, or puts it away. Rebindable from the in-game controls menu.");

        InputManager.Instance.AddButton(PaintBrushPlugin.PluginGUID, new ButtonConfig
        {
            Name = ButtonName,
            Config = _key,
        });
    }

    public static void Update(Player player)
    {
        if (player == null)
        {
            Item = null;
            _knownRecipesRefreshed = false;
            return;
        }

        Ensure();
        if (Item == null)
        {
            return;
        }

        // Once per session, so the brush's tools count as known without a brush in the inventory.
        if (!_knownRecipesRefreshed)
        {
            _knownRecipesRefreshed = true;
            player.UpdateKnownRecipesList();
        }

        if (!InputBlocked() && ZInput.GetButtonDown(ButtonQueryName))
        {
            Toggle(player);
        }
    }

    private static bool InputBlocked()
    {
        return Menu.IsVisible()
            || InventoryGui.IsVisible()
            || (Chat.instance != null && Chat.instance.HasFocus())
            || Console.IsVisible()
            || TextInput.IsVisible()
            || (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large)
            || TypingInField();
    }

    private static bool TypingInField()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
        {
            return false;
        }

        InputField field = selected.GetComponent<InputField>();
        if (field != null && field.isFocused)
        {
            return true;
        }

        TMPro.TMP_InputField tmpField = selected.GetComponent<TMPro.TMP_InputField>();
        return tmpField != null && tmpField.isFocused;
    }

    private static void Toggle(Player player)
    {
        // Checking the hand rather than keeping a flag stays right across death, respawn, or something else
        // unequipping the brush.
        if (player.GetRightItem() == Item)
        {
            player.UnequipItem(Item);
        }
        else
        {
            // The same path as picking a tool from the hotbar: shows it in hand and opens build mode.
            _ = player.EquipItem(Item);
        }
    }

    private static void Ensure()
    {
        if (Item != null || ObjectDB.instance == null)
        {
            return;
        }

        GameObject prefab = ObjectDB.instance.GetItemPrefab(BrushPrefabs.ItemName);
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
        {
            return;
        }

        // A fresh ItemData rather than a clone of the prefab's: the prefab's has fields that are never set up
        // (m_customData is null), which SetupVisEquipment trips over.
        Item = new ItemDrop.ItemData
        {
            m_shared = drop.m_itemData.m_shared,
            m_dropPrefab = prefab,
            m_stack = 1,
            m_quality = 1,
            m_variant = 0,
            m_durability = drop.m_itemData.m_shared.m_maxDurability,
            m_customData = [],
        };
    }
}
