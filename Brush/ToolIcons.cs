using System;
using System.IO;
using System.Reflection;

using UnityEngine;

namespace PaintBrush.Brush;

/// <summary>
/// The brush tools' icons, drawn to match the mod icon: a flat brush part-way through painting a plank panel for
/// Paint, stripping one back to wood for Clear, and an eyedropper drawing paint from one for Pick. They're SVGs in Icons/, rendered to the embedded PNGs by
/// Icons/render_icons.py.
/// </summary>
internal static class ToolIcons
{
    private const string ResourcePrefix = "PaintBrush.Icons.";

    /// <summary>
    /// ImageConversion.LoadImage, found at runtime: its module targets a newer .NET Standard than this mod can
    /// reference.
    /// </summary>
    private static readonly MethodInfo LoadImage = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
        ?.GetMethod("LoadImage", [typeof(Texture2D), typeof(byte[])]);

    public static Sprite Paint()
    {
        return Load("Paint");
    }

    public static Sprite Clear()
    {
        return Load("Clear");
    }

    public static Sprite Pick()
    {
        return Load("Pick");
    }

    private static Sprite Load(string name)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name + ".png");
        if (stream == null)
        {
            Jotunn.Logger.LogWarning("Missing tool icon " + name);
            return null;
        }

        using MemoryStream memory = new();
        stream.CopyTo(memory);
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, mipChain: false) { name = "PaintBrush_" + name };
        if (LoadImage == null || !(bool)LoadImage.Invoke(null, [texture, memory.ToArray()]))
        {
            Jotunn.Logger.LogWarning("Couldn't load tool icon " + name);
            return null;
        }

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        sprite.name = texture.name;
        return sprite;
    }
}
