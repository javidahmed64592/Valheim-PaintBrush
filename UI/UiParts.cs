using System;

using Jotunn.Managers;

using UnityEngine;
using UnityEngine.UI;

namespace PaintBrush.UI;

/// <summary>
/// Building blocks for the brush menu: boxes placed from the parent's top left, images, labels and small sprites
/// drawn in code (the game's fonts have no arrows or dots).
/// </summary>
internal static class UiParts
{
    private static readonly float[] DotCentres = [6f, 15.5f, 25f];

    public static Sprite White => field ??= Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

    /// <summary>
    /// A rectangle placed from the parent's top left corner, with y growing downwards.
    /// </summary>
    internal static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    internal static RawImage Raw(Transform parent, string name, float x, float y, float width, float height, Texture texture)
    {
        RawImage image = Box(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        return image;
    }

    internal static Image Image(Transform parent, string name, float x, float y, float width, float height, Color color, Sprite sprite)
    {
        Image image = Box(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    internal static Text Label(Transform parent, string token, float x, float y, float width, float height, Color color, int size)
    {
        Text text = Box(parent, "Label", x, y, width, height).gameObject.AddComponent<Text>();
        GUIManager.Instance.ApplyTextStyle(text, GUIManager.Instance.AveriaSerifBold, color, size, createOutline: true);
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = Localization.instance.Localize(token);
        return text;
    }

    internal static Sprite MakeRing()
    {
        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, mipChain: false);
        Vector2 centre = new((size - 1) / 2f, (size - 1) / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), centre);
                float white = Mathf.Clamp01(3f - Mathf.Abs(distance - 11f));
                float black = Mathf.Clamp01(5f - Mathf.Abs(distance - 11f));
                Color color = Color.Lerp(Color.black, Color.white, white);
                color.a = Mathf.Max(white, black * 0.8f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// A triangle pointing left or right.
    /// </summary>
    internal static Sprite MakeArrow(bool right)
    {
        return MakeSprite(32, (x, y) =>
        {
            float px = right ? x : 31f - x;
            // Inside when left of the two slanted edges and right of the back edge, softened over a pixel.
            float back = px - 9f;
            float top = ((24f - px) * 0.75f) - Mathf.Abs(y - 15.5f) + 0.5f;
            return Mathf.Clamp01(Mathf.Min(back, top));
        });
    }

    /// <summary>
    /// A small triangle pointing down, for a dropdown.
    /// </summary>
    internal static Sprite MakeArrowDown()
    {
        return MakeSprite(32, (x, y) =>
        {
            float top = y - 10f;
            float sides = ((y - 10f) * 0.8f) - Mathf.Abs(x - 15.5f) + 0.5f;
            return Mathf.Clamp01(Mathf.Min(22f - y, sides) + Mathf.Min(0f, top));
        });
    }

    /// <summary>
    /// Three dots in a row, for a "more" button.
    /// </summary>
    internal static Sprite MakeDots()
    {
        return MakeSprite(32, (x, y) =>
        {
            float best = 0f;
            foreach (float cx in DotCentres)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, 15.5f));
                best = Mathf.Max(best, Mathf.Clamp01(3.5f - distance));
            }

            return best;
        });
    }

    /// <summary>
    /// A padlock, closed or with its shackle raised and open on the right.
    /// </summary>
    internal static Sprite MakePadlock(bool open)
    {
        const float radius = 5.5f;
        const float half = 1.7f;
        // Body from 3.5 to 16.5 and the closed shackle's top at about 28, centred in the 32 pixels.
        const float bodyBottom = 3.5f;
        const float bodyTop = 16.5f;
        float centre = open ? 24f : 20.5f;
        return MakeSprite(32, (x, y) =>
        {
            // Rows count up from the bottom. The body, with a keyhole cut out.
            float body = Mathf.Clamp01(Mathf.Min(Mathf.Min(x - 7.5f, 24.5f - x), Mathf.Min(y + 0.5f - bodyBottom, bodyTop + 0.5f - y)) + 0.5f);
            if (Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 10f)) < 2f)
            {
                body = 0f;
            }

            // The shackle: a half ring over two legs. Open, the right leg stops short of the body.
            float shackle;
            if (y >= centre)
            {
                shackle = half + 0.5f - Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, centre)) - radius);
            }
            else
            {
                float left = y > bodyTop ? half + 0.5f - Mathf.Abs(x - (15.5f - radius)) : 0f;
                float right = y > (open ? centre - 2f : bodyTop) ? half + 0.5f - Mathf.Abs(x - (15.5f + radius)) : 0f;
                shackle = Mathf.Max(left, right);
            }

            return Mathf.Max(body, Mathf.Clamp01(shackle));
        });
    }

    private static Sprite MakeSprite(int size, Func<float, float, float> alpha)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
