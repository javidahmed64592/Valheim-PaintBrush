using System.Collections.Generic;

using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

using UnityEngine;

namespace PaintBrush.Brush;

/// <summary>
/// The paint brush item, its piece table and its tools. The item is a copy of the hammer, with its own table and
/// no recipe: it's only ever taken out with the equip key (see <see cref="PhantomBrush"/>). The tools are copies of
/// the hammer's Repair tool, which aims at built pieces without a placement ghost; clicking with one runs
/// <see cref="BrushTools.TryUse"/> instead of repairing.
/// </summary>
internal static class BrushPrefabs
{
    public const string ItemName = "PaintBrush";
    public const string TableName = "_PaintBrushPieceTable";
    public const string PaintToolName = "PaintBrush_Paint";
    public const string ClearToolName = "PaintBrush_Clear";

    private const string BaseItem = "Hammer";
    private const string BaseTool = "piece_repair";

    private static readonly List<Piece> Tools = [];

    public static void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable += Create;
    }

    /// <summary>
    /// Vanilla puts Repair-type tools on a single button in the category list, which tools without tabs (like the
    /// brush) don't show. While the menu builds its buttons the brush tools are ordinary pieces, so they go in the
    /// grid; the rest of the time they're Repair tools again, which aim at built pieces without a ghost.
    /// </summary>
    public static void SetToolsAsRepair(bool repair)
    {
        foreach (Piece tool in Tools)
        {
            if (tool != null)
            {
                tool.m_repairPiece = repair;
            }
        }
    }

    public static bool IsBrushTable(PieceTable table)
    {
        return table != null && table.name == TableName;
    }

    private static void Create()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Create;

        CustomPieceTable table = new(TableName, new PieceTableConfig
        {
            CanRemovePieces = false,
        });
        table.PieceTable.m_hideAdvancedMenu = true;
        table.PieceTable.m_canRemoveFeasts = false;
        _ = PieceManager.Instance.AddPieceTable(table);

        CustomItem item = new(ItemName, BaseItem, new ItemConfig
        {
            Name = "$paintbrush_item",
            Description = "$paintbrush_item_description",
            PieceTable = TableName,
        });
        ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
        shared.m_useDurability = false;
        shared.m_canBeReparied = false;
        _ = ItemManager.Instance.AddItem(item);

        AddTool(PaintToolName, "$paintbrush_piece_paint", "$paintbrush_piece_paint_description", ToolIcons.Paint());
        AddTool(ClearToolName, "$paintbrush_piece_clear", "$paintbrush_piece_clear_description", ToolIcons.Clear());
    }

    private static void AddTool(string name, string displayName, string description, Sprite icon)
    {
        CustomPiece piece = new(name, BaseTool, new PieceConfig
        {
            Name = displayName,
            Description = description,
            PieceTable = TableName,
            Icon = icon,
        });
        piece.Piece.m_resources = [];
        piece.Piece.m_craftingStation = null;
        piece.Piece.m_repairPiece = true;
        _ = PieceManager.Instance.AddPiece(piece);
        Tools.Add(piece.Piece);
    }
}
