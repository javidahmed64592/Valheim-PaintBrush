using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.Patches;

/// <summary>
/// MaterialMan's PropertyContainer (a private class) sets one property block on all of a piece's renderers
/// whenever the highlight, snow or Ashlands damage changes. Afterwards the paint is put back on top.
/// </summary>
[HarmonyPatch]
internal static class MaterialMan_PropertyContainer_UpdateBlock_Patch
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method(AccessTools.Inner(typeof(MaterialMan), "PropertyContainer"), "UpdateBlock");
    }

    [HarmonyPostfix]
    private static void Postfix(List<Renderer> ___m_assignedRenderers, MaterialPropertyBlock ___m_propertyBlock)
    {
        PaintedPieces.AfterMaterialManUpdate(___m_assignedRenderers, ___m_propertyBlock);
    }
}
