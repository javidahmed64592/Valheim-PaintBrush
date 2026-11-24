using BepInEx;

using HarmonyLib;

using Jotunn.Utils;

using PaintBrush.Brush;
using PaintBrush.Core;
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
    public const string PluginVersion = "0.1.0";

    private Harmony _harmony;

    private void Awake()
    {
        PhantomBrush.BindConfig(Config);
        PaintTextures.BindConfig(Config);
        Translations.Load();
        BrushPrefabs.Register();

        _harmony = new Harmony(PluginGUID);
        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Unity message")]
    private void Update()
    {
        Player player = Player.m_localPlayer;
        PhantomBrush.Update(player);
        BrushState.Sync(player);
        PaintPreview.Update(player);
        PaintedPieces.Update();
        PaintPanel.Update(player);
    }
}
