using Jotunn.Managers;

using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.Brush;

/// <summary>
/// The splash where the brush hits a piece: vanilla's blood hit effect (a wet splash, flying drops and a splat
/// left on the surface), recoloured with the paint, and the squelch of a hit blob. Painting splashes the new
/// colour; clearing splashes the colour being removed.
/// </summary>
internal static class PaintSplash
{
    private const string BaseEffect = "vfx_BloodHit";
    private const string SoundName = "sfx_blob_hit";
    private const string SplashName = "PaintBrush_Splash";

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static GameObject _splash;
    private static GameObject _sound;

    /// <summary>
    /// Makes the splash from the blood effect. Its copy has no ZNetView, so it only shows for this player (others
    /// don't know its colour, and players without the mod don't know the copy); the sound stays the vanilla
    /// networked effect, so everyone nearby hears it.
    /// </summary>
    public static void Create()
    {
        _sound = PrefabManager.Instance.GetPrefab(SoundName);
        GameObject splash = PrefabManager.Instance.CreateClonedPrefab(SplashName, BaseEffect);
        if (splash == null)
        {
            Jotunn.Logger.LogWarning("Couldn't find " + BaseEffect + ", painting won't splash.");
            return;
        }

        ZNetView view = splash.GetComponent<ZNetView>();
        if (view != null)
        {
            Object.DestroyImmediate(view);
        }

        // Some of the blood's materials are tinted (the wet splash is slime green); the paint colour comes from
        // the particles, so the materials are made neutral.
        foreach (ParticleSystemRenderer renderer in splash.GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !material.HasProperty(ColorId) || IsGrey(material.GetColor(ColorId)))
                {
                    continue;
                }

                Material neutral = new(material) { name = material.name + "_paint" };
                neutral.SetColor(ColorId, Color.white);
                if (neutral.HasProperty(EmissionColorId))
                {
                    neutral.SetColor(EmissionColorId, Color.black);
                }

                materials[i] = neutral;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }

        _splash = splash;
    }

    /// <summary>
    /// Splashes paint where the brush hit a piece, facing out of the surface.
    /// </summary>
    public static void Play(Vector3 point, Vector3 normal, PaintColor paint)
    {
        if (_sound != null)
        {
            _ = Object.Instantiate(_sound, point, Quaternion.identity);
        }

        if (_splash == null || paint.IsNone)
        {
            return;
        }

        GameObject splash = Object.Instantiate(_splash, point, Quaternion.LookRotation(normal));
        float tame = PaintTextures.Tame(paint);
        Color color = new(paint.R * tame / 255f, paint.G * tame / 255f, paint.B * tame / 255f);
        foreach (ParticleSystem particles in splash.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
        {
            ParticleSystem.MainModule main = particles.main;
            ParticleSystem.MinMaxGradient start = main.startColor;
            main.startColor = start.mode == ParticleSystemGradientMode.TwoColors
                ? new ParticleSystem.MinMaxGradient(WithAlpha(color * 0.75f, start.colorMin.a), WithAlpha(color, start.colorMax.a))
                : new ParticleSystem.MinMaxGradient(WithAlpha(color, start.color.a));
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static bool IsGrey(Color color)
    {
        return Mathf.Abs(color.r - color.g) < 0.02f && Mathf.Abs(color.g - color.b) < 0.02f;
    }
}
