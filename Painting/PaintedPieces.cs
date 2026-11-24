using System;
using System.Collections.Generic;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// <para>
/// Shows each piece's paint by giving its renderers painted copies of their materials (see
/// <see cref="PaintMaterials"/>). The game's own shader still draws them, with its lighting, snow, rain, wear and
/// hover highlight, which MaterialMan sets on top through a property block.
/// </para>
/// <para>
/// Pieces are checked when they load, and then a few hundred a frame in turn. That picks up paint other players add
/// or clear, and puts the paint back if something else swapped a painted material (MaterialVariation picks a
/// piece's material variant during its first second). The brush's preview is an override on top of the saved paint.
/// </para>
/// <para>
/// Painted pieces also get a marker property through MaterialMan, which makes Valheim Performance Optimizations
/// draw them normally instead of in its batches, which only know the vanilla materials.
/// </para>
/// </summary>
internal static class PaintedPieces
{
    private const int ChecksPerFrame = 300;

    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    /// <summary>
    /// A property no shader reads, set through MaterialMan on painted pieces. Renderers that draw pieces themselves
    /// rather than through their MeshRenderers (Valheim Performance Optimizations' indirect renderer) fall back to
    /// normal rendering for a piece with a property they don't know, so the painted materials show.
    /// </summary>
    private static readonly int PaintedFlag = Shader.PropertyToID("_PaintBrushPainted");

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
        public Material Original;
        public Material Painted;

        /// <summary>
        /// A counted copy from <see cref="PaintMaterials.Acquire"/>, released when the slot is cleared, rather than
        /// a preview.
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
        public bool Flagged;
        public readonly List<Slot> Slots = [];
    }

    private readonly struct MaterialInfo(bool paintable, Texture texture, bool cutout)
    {
        public readonly bool Paintable = paintable;
        public readonly Texture Texture = texture;
        public readonly bool Cutout = cutout;
    }

    private static readonly Dictionary<Piece, State> States = [];
    private static readonly Dictionary<int, MaterialInfo> Materials = [];
    private static readonly List<Renderer> RendererBuffer = [];
    private static int _cursor;

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
                MaterialInfo info = Info(PaintMaterials.Original(material));
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
    /// Checks a few hundred pieces a frame for paint changed by other players, and painted materials swapped by
    /// something else.
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
            if (known ? state.Stored != stored : !stored.IsNone)
            {
                state ??= GetState(piece);
                state.Stored = stored;
                Refresh(state);
            }
            else if (known && !SlotsIntact(state))
            {
                Repaint(state);
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

        Repaint(state);
    }

    private static void Repaint(State state)
    {
        PaintColor wanted = state.Preview ?? state.Stored;
        bool preview = state.Preview.HasValue;
        ClearSlots(state);
        state.Shown = wanted;
        state.ShownIsPreview = preview;
        if (!wanted.IsNone)
        {
            PaintSlots(state, wanted, preview);
        }

        SetFlag(state, state.Slots.Count > 0);
        Forget(state);
    }

    private static void SetFlag(State state, bool painted)
    {
        if (state.Flagged == painted || MaterialMan.instance == null || state.Piece == null)
        {
            return;
        }

        state.Flagged = painted;
        if (painted)
        {
            MaterialMan.instance.SetValue(state.Piece.gameObject, PaintedFlag, 1f);
        }
        else
        {
            MaterialMan.instance.ResetValue(state.Piece.gameObject, PaintedFlag);
        }
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

    /// <summary>
    /// Whether every painted material is still on its renderer.
    /// </summary>
    private static bool SlotsIntact(State state)
    {
        foreach (Slot slot in state.Slots)
        {
            if (slot.Renderer == null)
            {
                continue;
            }

            Material[] materials = slot.Renderer.sharedMaterials;
            if (slot.Index >= materials.Length || materials[slot.Index] != slot.Painted)
            {
                return false;
            }
        }

        return true;
    }

    private static void PaintSlots(State state, PaintColor paint, bool preview)
    {
        GatherRenderers(state.Piece);
        foreach (Renderer renderer in RendererBuffer)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                Material original = PaintMaterials.Original(materials[index]);
                MaterialInfo info = Info(original);
                if (!info.Paintable)
                {
                    continue;
                }

                Material painted = preview
                    ? PaintMaterials.Preview(original, info.Texture, info.Cutout, paint)
                    : PaintMaterials.Acquire(original, info.Texture, info.Cutout, paint);
                state.Slots.Add(new Slot
                {
                    Renderer = renderer,
                    Index = index,
                    Original = original,
                    Painted = painted,
                    Counted = !preview,
                });
                materials[index] = painted;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    /// <summary>
    /// Puts the original materials back (where the painted ones are still on) and releases the painted copies.
    /// </summary>
    private static void ClearSlots(State state)
    {
        foreach (Slot slot in state.Slots)
        {
            if (slot.Renderer != null)
            {
                Material[] materials = slot.Renderer.sharedMaterials;
                if (slot.Index < materials.Length && PaintMaterials.IsCopy(materials[slot.Index]))
                {
                    materials[slot.Index] = slot.Original;
                    slot.Renderer.sharedMaterials = materials;
                }
            }

            if (slot.Counted)
            {
                PaintMaterials.Release(slot.Original, state.Shown);
            }
        }

        state.Slots.Clear();
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
