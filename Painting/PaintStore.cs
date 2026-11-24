namespace PaintBrush.Painting;

/// <summary>
/// Reads and writes a piece's paint in its saved data. Players and servers without the mod don't know the key,
/// so they keep it untouched and see the piece as vanilla.
/// </summary>
internal static class PaintStore
{
    private static readonly int Key = "PaintBrush_Paint".GetStableHashCode();

    public static PaintColor Read(ZNetView view)
    {
        return view != null && view.IsValid() ? PaintColor.Unpack(view.GetZDO().GetInt(Key)) : PaintColor.None;
    }

    /// <summary>
    /// Takes ownership of the piece first, as vanilla does when a sign is edited, so the change is sent to
    /// everyone. Clearing writes 0 rather than removing the key, because only a write marks the data as changed.
    /// </summary>
    public static void Write(ZNetView view, PaintColor paint)
    {
        if (view == null || !view.IsValid())
        {
            return;
        }

        view.ClaimOwnership();
        view.GetZDO().Set(Key, paint.Pack());
    }
}
