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
