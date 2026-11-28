using BepInEx;

using HarmonyLib;

using Jotunn.Utils;

using PaintBrush.Brush;
using PaintBrush.Core;
using PaintBrush.History;
using PaintBrush.Painting;
using PaintBrush.UI;

namespace PaintBrush;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
public class PaintBrushPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "javidahmed64592.paintbrush";
    public const string PluginName = "Paint Brush";
    public const string PluginVersion = "0.5.1";

    private Harmony _harmony;

    private void Awake()
    {
        PhantomBrush.BindConfig(Config);
        PaintTextures.BindConfig(Config);
        ConnectedPieces.BindConfig(Config);
        PaintHistory.BindConfig(Config);
        Translations.Load();

        _harmony = new Harmony(PluginGUID);
        if (!SafePatcher.PatchAll(_harmony, Config, Logger))
        {
            return;
        }

        BrushPrefabs.Register();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Unity message")]
    private void Update()
    {
        if (!SafePatcher.ModEnabled)
        {
            return;
        }

        Player player = Player.m_localPlayer;
        PhantomBrush.Update(player);
        BrushState.Sync(player);
        BrushModel.Update(player);
        if (player == null)
        {
            PaintHistory.Reset();
        }
        else if (BrushTools.Selected(player) != BrushTool.None)
        {
            PaintHistory.UpdateKeys(player);
        }

        PaintPreview.Update(player);
        PaintedPieces.Update();
        PaintPanel.Update(player);
    }
}
