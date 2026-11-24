using System;
using System.Collections.Generic;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// <para>
/// Shows each piece's paint. A piece's renderers get the painted copy of their texture through a per-material
/// property block, so the game's own shader still draws them, with its lighting, snow, rain and wear.
/// </para>
/// <para>
/// Pieces are checked when they load, and then a few hundred a frame in turn, which picks up paint other players
/// add or clear. The brush's preview is an override on top of the saved paint.
/// </para>
/// <para>
/// The game's MaterialMan sets one property block on every renderer of a piece whenever it changes the hover
/// highlight, snow or Ashlands damage, so the paint is put back each time it does (see
/// <see cref="AfterMaterialManUpdate"/>).
/// </para>
/// </summary>
internal static class PaintedPieces
{
    private const int ChecksPerFrame = 300;

    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    /// <summary>
    /// Shaders whose materials are never painted: snow on top of pieces, glass, fire, water and so on.
    /// </summary>
    private static readonly string[] SkippedShaders =
    [
        "Snow", "Particle", "Unlit", "Distortion", "Water", "Decal", "TextMeshPro", "Flow", "Lighting", "Invis",
        "Sprites", "Blob", "Tar", "UI", "Cloud", "Heightmap", "Grass", "Skybox",
    ];

    private sealed class Slot
    {
        public Renderer Renderer;
        public int Index;
        public Texture Source;
        public Texture2D Texture;

        /// <summary>
        /// A counted texture from <see cref="PaintTextures.Acquire"/> (released when the slot is cleared), rather
        /// than a preview.
        /// </summary>
        public bool Counted;
    }

    private sealed class State
    {
        public Piece Piece;
        public PaintColor Stored;
        public PaintColor? Preview;
        public PaintColor Shown;
        public bool ShownIsPreview;
        public readonly List<Slot> Slots = [];
    }

    private readonly struct MaterialInfo(bool paintable, Texture texture, bool cutout)
    {
        public readonly bool Paintable = paintable;
        public readonly Texture Texture = texture;
        public readonly bool Cutout = cutout;
    }

    private static readonly Dictionary<Piece, State> States = [];
    private static readonly Dictionary<Renderer, List<Slot>> ByRenderer = [];
    private static readonly Dictionary<int, MaterialInfo> Materials = [];
    private static readonly List<Renderer> RendererBuffer = [];
    private static readonly MaterialPropertyBlock Block = new();
    private static int _cursor;
    private static bool _nullClearFails;

    /// <summary>
    /// The paint a piece has saved, or None.
    /// </summary>
    public static PaintColor StoredPaint(Piece piece)
    {
        return piece != null ? PaintStore.Read(piece.m_nview) : PaintColor.None;
    }

    /// <summary>
    /// Whether the brush can paint a piece: built by a player, saved in the world, and with something to paint.
    /// </summary>
    public static bool CanPaint(Piece piece)
    {
        return piece != null
            && piece.IsPlacedByPlayer()
            && piece.m_nview != null
            && piece.m_nview.IsValid()
            && SampleTexture(piece, out _, out _);
    }

    /// <summary>
    /// The texture of the first paintable material on a piece, for previews in the menu.
    /// </summary>
    public static bool SampleTexture(Piece piece, out Texture texture, out bool cutout)
    {
        texture = null;
        cutout = false;
        if (piece == null)
        {
            return false;
        }

        GatherRenderers(piece);
        foreach (Renderer renderer in RendererBuffer)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                MaterialInfo info = Info(material);
                if (info.Paintable)
                {
                    texture = info.Texture;
                    cutout = info.Cutout;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Called when a piece wakes up: shows its saved paint straight away.
    /// </summary>
    public static void OnPieceAwake(Piece piece)
    {
        PaintColor stored = StoredPaint(piece);
        if (!stored.IsNone)
        {
            State state = GetState(piece);
            state.Stored = stored;
            Refresh(state);
        }
    }

    public static void OnPieceDestroy(Piece piece)
    {
        if (States.TryGetValue(piece, out State state))
        {
            ClearSlots(state);
            _ = States.Remove(piece);
        }
    }

    /// <summary>
    /// After this client wrote a piece's paint, so it shows without waiting for the next check.
    /// </summary>
    public static void OnPaintWritten(Piece piece, PaintColor paint)
    {
        State state = GetState(piece);
        state.Stored = paint;
        Refresh(state);
    }

    /// <summary>
    /// Shows a piece with this paint instead of its saved paint (None shows it vanilla), or with null goes back to
    /// the saved paint.
    /// </summary>
    public static void SetPreview(Piece piece, PaintColor? preview)
    {
        if (piece == null)
        {
            return;
        }

        if (!States.TryGetValue(piece, out State state))
        {
            if (!preview.HasValue)
            {
                return;
            }

            state = GetState(piece);
            state.Stored = StoredPaint(piece);
        }

        state.Preview = preview;
        Refresh(state);
    }

    /// <summary>
    /// Checks a few hundred pieces a frame for paint changed by other players.
    /// </summary>
    public static void Update()
    {
        List<Piece> pieces = Piece.s_allPieces;
        if (pieces == null || pieces.Count == 0)
        {
            return;
        }

        int checks = Mathf.Min(ChecksPerFrame, pieces.Count);
        for (int i = 0; i < checks; i++)
        {
            if (_cursor >= pieces.Count)
            {
                _cursor = 0;
            }

            Piece piece = pieces[_cursor++];
            if (piece == null)
            {
                continue;
            }

            PaintColor stored = StoredPaint(piece);
            bool known = States.TryGetValue(piece, out State state);
            if (known ? state.Stored == stored : stored.IsNone)
            {
                continue;
            }

            state ??= GetState(piece);
            state.Stored = stored;
            Refresh(state);
        }
    }

    /// <summary>
    /// Called after MaterialMan set its property block on a piece's renderers: puts the paint back on top, in a
    /// per-material block holding MaterialMan's values as well, so the highlight and snow still show.
    /// </summary>
    public static void AfterMaterialManUpdate(List<Renderer> renderers, MaterialPropertyBlock block)
    {
        if (ByRenderer.Count == 0 || renderers == null || block == null)
        {
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !ByRenderer.TryGetValue(renderer, out List<Slot> slots))
            {
                continue;
            }

            foreach (Slot slot in slots)
            {
                block.SetTexture(MainTex, slot.Texture);
                renderer.SetPropertyBlock(block, slot.Index);
            }
        }
    }

    private static State GetState(Piece piece)
    {
        if (!States.TryGetValue(piece, out State state))
        {
            state = new State { Piece = piece };
            States[piece] = state;
        }

        return state;
    }

    private static void Refresh(State state)
    {
        PaintColor wanted = state.Preview ?? state.Stored;
        bool preview = state.Preview.HasValue;
        if (wanted == state.Shown && preview == state.ShownIsPreview && (wanted.IsNone || state.Slots.Count > 0))
        {
            Forget(state);
            return;
        }

        ClearSlots(state);
        state.Shown = wanted;
        state.ShownIsPreview = preview;
        if (!wanted.IsNone)
        {
            PaintSlots(state, wanted, preview);
        }

        Forget(state);
    }

    /// <summary>
    /// A piece with nothing saved and nothing previewed needs no state.
    /// </summary>
    private static void Forget(State state)
    {
        if (state.Stored.IsNone && !state.Preview.HasValue && state.Slots.Count == 0)
        {
            _ = States.Remove(state.Piece);
        }
    }

    private static void PaintSlots(State state, PaintColor paint, bool preview)
    {
        GatherRenderers(state.Piece);
        foreach (Renderer renderer in RendererBuffer)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                MaterialInfo info = Info(materials[index]);
                if (!info.Paintable)
                {
                    continue;
                }

                Texture2D texture = preview
                    ? PaintTextures.GetPreview(info.Texture, info.Cutout, paint)
                    : PaintTextures.Acquire(info.Texture, info.Cutout, paint);
                Slot slot = new()
                {
                    Renderer = renderer,
                    Index = index,
                    Source = info.Texture,
                    Texture = texture,
                    Counted = !preview,
                };
                state.Slots.Add(slot);

                if (!ByRenderer.TryGetValue(renderer, out List<Slot> slots))
                {
                    slots = [];
                    ByRenderer[renderer] = slots;
                }

                slots.Add(slot);

                // Starts from the block MaterialMan last set, so its values stay.
                renderer.GetPropertyBlock(Block);
                Block.SetTexture(MainTex, texture);
                renderer.SetPropertyBlock(Block, index);
            }
        }
    }

    private static void ClearSlots(State state)
    {
        foreach (Slot slot in state.Slots)
        {
            // Unity objects hash by instance id, so a destroyed renderer's entry still comes out.
            if (ByRenderer.TryGetValue(slot.Renderer, out List<Slot> slots))
            {
                _ = slots.Remove(slot);
                if (slots.Count == 0)
                {
                    _ = ByRenderer.Remove(slot.Renderer);
                }
            }

            if (slot.Renderer != null)
            {
                ClearBlock(slot.Renderer, slot.Index);
            }

            if (slot.Counted)
            {
                PaintTextures.Release(slot.Source, state.Shown);
            }
        }

        state.Slots.Clear();
    }

    /// <summary>
    /// Removes the per-material block, so the material shows its own texture again.
    /// </summary>
    private static void ClearBlock(Renderer renderer, int index)
    {
        if (!_nullClearFails)
        {
            try
            {
                renderer.SetPropertyBlock(null, index);
                return;
            }
            catch (Exception e)
            {
                _nullClearFails = true;
                Jotunn.Logger.LogWarning("Clearing a per-material block failed, using the renderer's block instead: " + e.Message);
            }
        }

        renderer.GetPropertyBlock(Block);
        renderer.SetPropertyBlock(Block, index);
    }

    private static void GatherRenderers(Piece piece)
    {
        RendererBuffer.Clear();
        piece.GetComponentsInChildren(includeInactive: true, RendererBuffer);
        for (int i = RendererBuffer.Count - 1; i >= 0; i--)
        {
            if (RendererBuffer[i] is not MeshRenderer and not SkinnedMeshRenderer)
            {
                RendererBuffer.RemoveAt(i);
            }
        }
    }

    private static MaterialInfo Info(Material material)
    {
        if (material == null)
        {
            return default;
        }

        int id = material.GetInstanceID();
        if (!Materials.TryGetValue(id, out MaterialInfo info))
        {
            info = Examine(material);
            Materials[id] = info;
        }

        return info;
    }

    private static MaterialInfo Examine(Material material)
    {
        string shader = material.shader != null ? material.shader.name : "";
        foreach (string skipped in SkippedShaders)
        {
            if (shader.IndexOf(skipped, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return default;
            }
        }

        // Transparent materials (glass, water) draw from the transparent queue onwards.
        if (!material.HasProperty(MainTex) || material.renderQueue >= 2950)
        {
            return default;
        }

        Texture texture = material.GetTexture(MainTex);
        if (texture == null)
        {
            return default;
        }

        bool cutout = material.IsKeywordEnabled("_ALPHATEST_ON")
            || material.renderQueue >= 2450
            || (material.HasProperty("_Mode") && Mathf.Approximately(material.GetFloat("_Mode"), 1f));
        return new MaterialInfo(paintable: true, texture, cutout);
    }
}
