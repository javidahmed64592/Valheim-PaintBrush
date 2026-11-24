using BepInEx;
using BepInEx.Configuration;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaintBrush.Core;

/// <summary>
/// Keys read straight from the keyboard, so they work while moving, and whether typing should keep them quiet.
/// </summary>
internal static class Keys
{
    public static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

    public static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    /// <summary>
    /// KeyboardShortcut.IsDown is false while any other key is held, so it would never fire while moving. This
    /// only needs the main key pressed and the modifiers held.
    /// </summary>
    public static bool IsDown(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None || !UnityInput.Current.GetKeyDown(shortcut.MainKey))
        {
            return false;
        }

        foreach (KeyCode modifier in shortcut.Modifiers)
        {
            if (!UnityInput.Current.GetKey(modifier))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a menu, chat, console or text field has the keyboard.
    /// </summary>
    public static bool Blocked()
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
}
