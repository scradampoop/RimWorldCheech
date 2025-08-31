using RimWorld;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Cheechin;

public class FurColors: DefModExtension
{
    public List<FurColorDef> allowedFurColors;
}

public class FurColorDef: Def
{
    public Color primaryColor;
    public Color? secondaryColor;
    public float selectionWeight;
    public int displayOrder;
    //public List<GeneDef> genes;
    public bool blacklistPrimary;
    public bool blacklistSecondary;
}

//[HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.GraphicFor))]
//public static class PawnRenderNode_Head_GraphicFor_Patch
//{
//    [HarmonyPriority(int.MinValue)]
//    public static void Postfix(PawnRenderNode_Head __instance, Pawn pawn, ref Graphic? __result)
//    {
//        if (__result != null && pawn.Drawer.renderer.CurRotDrawMode != RotDrawMode.Dessicated)
//        {
//            var geneFurPatternAccent = pawn.GetGeneFurPatternAccent();

//            if (geneFurPatternAccent != null)
//                __result = geneFurPatternAccent.GetGraphicOverriden(__result, __instance, pawn);
//        }
//    }
//}

//[HarmonyPatch(typeof(PawnRenderNode_Fur), nameof(PawnRenderNode_Fur.GraphicFor))]
//public static class PawnRenderNode_Fur_GraphicFor_Patch
//{
//    [HarmonyPriority(int.MinValue)]
//    public static void Postfix(PawnRenderNode_Fur __instance, Pawn pawn, ref Graphic? __result)
//    {
//        if (
//            __instance.gene is GeneFurPatternAccent geneFurPatternAccent
//            && __result != null
//            && pawn.Drawer.renderer.CurRotDrawMode != RotDrawMode.Dessicated
//            && __result.path == pawn.story?.furDef.GetFurBodyGraphicPath(pawn)
//        )
//            __result = geneFurPatternAccent.GetGraphicOverriden(__result, __instance, pawn);
//    }
//}

//[HarmonyPatch(typeof(PawnRenderNode), nameof(PawnRenderNode.GraphicFor))]
//public static class PawnRenderNode_GraphicFor_Patch
//{
//    [HarmonyPriority(int.MinValue)]
//    public static void Postfix(PawnRenderNode __instance, Pawn pawn, ref Graphic? __result)
//    {
//        if (__result != null && __instance.Props.colorType != PawnRenderNodeProperties.AttachmentColorType.Custom)
//        {
//            var geneFurPatternAccent = pawn.GetGeneFurPatternAccent(activeCheck: pawn.IsMutant is false);

//            if (geneFurPatternAccent != null)
//                __result = geneFurPatternAccent.GetGraphicOverriden(__result, __instance, pawn);

//            if (geneFurPatternAccent == null && __instance.gene != null && __instance.gene.def.defName.StartsWith("Cheechin"))
//                __result = GeneFurPatternAccent.GetGraphicOverridenNoFurGene(__result, __instance, pawn);
//        }
//    }
//}

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
public static class PawnGenerator_GeneratePawn_Patch
{
    public static void Postfix(ref Pawn? __result) => __result?.GetGeneFurPatternAccent()?.ApplyColors();
}

public class PawnRenderNode_FurPatternAccent(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree)
{
    public override Graphic? GraphicFor(Pawn pawnGraphicFor)
    {
        // TODO: PawnRenderNode_FurPatternAccent is referenced by the base gene def, but the texture paths are defined by the derived gene defs. Is there a way to get the texture path and colors with fewer operations and without reflection? - or does it matter? Not sure how often this is called.
        var furPatternAccent = pawn.GetGeneFurPatternAccent()!;
        var bodyTypeGraphicPaths = furPatternAccent.def.renderNodeProperties.FirstOrDefault(r => r.bodyTypeGraphicPaths?.Any() == true).bodyTypeGraphicPaths;
        for (int index = 0; index < bodyTypeGraphicPaths.Count; ++index)
        {
            if (bodyTypeGraphicPaths[index].bodyType == pawnGraphicFor.story.bodyType)
            {
                return GraphicDatabase.Get<Graphic_Multi>(
                    path: bodyTypeGraphicPaths[index].texturePath,
                    shader: ShaderDatabase.CutoutSkinOverlay,
                    drawSize: Vector2.one,
                    color: furPatternAccent.colorOne,
                    colorTwo: furPatternAccent.colorTwo ?? Color.white,
                    data: null,
                    maskPath: pawnGraphicFor.story.furDef?.GetFurBodyGraphicPath(pawnGraphicFor) ?? pawnGraphicFor.story.bodyType.bodyNakedGraphicPath
                );
            }
        }
        return null;
    }
}

[HotSwappable]
public class GeneFurPatternAccent: Gene
{
    public FurColorDef? furColor;
    public Color colorOne;

    /// <summary>
    /// TBD: Either eliminate colorTwo or set to Color.white by default if we don't use it.
    /// </summary>
    public Color? colorTwo;

    public override void PostAdd()
    {
        base.PostAdd();
        SetFurColor();
    }

    public void ApplyColors()
    {
        if (pawn != null)
        {
            pawn.story.skinColorOverride = colorTwo;
            pawn.drawer.renderer.SetAllGraphicsDirty();
        }
    }

    private void SetFurColor()
    {
        var extension = def.GetModExtension<FurColors>();
        if (extension.allowedFurColors.TryRandomElementByWeight(x => x.selectionWeight, out var result))
        {
            furColor = result;
            colorOne = furColor.primaryColor;
            colorTwo = furColor.secondaryColor;
            ApplyColors();
        }
    }

    public Graphic GetGraphicOverriden(Graphic original, PawnRenderNode source, Pawn pawn)
    {
        if (furColor is null)
            SetFurColor();

        var color1 = GetColorOne(source);
        var color2 = GetColorTwo(source);

        if (colorTwo != null && (color1.IndistinguishableFrom(color2) is false)) //are your fur colors even different?
        {
            if (original.Shader == ShaderTypeDefOf.CutoutComplex.Shader //is this a part with a CutoutComplex shader?
                || (source is PawnRenderNode_Head && pawn.story.headType?.requiredGenes != null
                && pawn.story.headType.requiredGenes.Any(x => typeof(GeneFurPatternAccent).IsAssignableFrom(x.geneClass)))) //or is this a furgene head?
            {                                                                                                              //I wish heads had a shaderType
                return (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(original.path,
                ShaderTypeDefOf.CutoutComplex.Shader, Vector2.one, color1, color2);
            }
        }

        if (source.Props.colorType == PawnRenderNodeProperties.AttachmentColorType.Hair)
            return (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(original.path, ShaderTypeDefOf.Cutout.Shader, Vector2.one, color1);

        return (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(original.path, ShaderTypeDefOf.Cutout.Shader, Vector2.one, color2);
    }

    public static Graphic GetGraphicOverridenNoFurGene(Graphic original, PawnRenderNode source, Pawn pawn)
    {
        var color1 = PostProcessNoFurGene(source, pawn, pawn.story.HairColor);
        var color2 = new Color();

        if ((source.gene.def.defName.StartsWith("Cheechin") && source.gene.def.defName.EndsWith("Ears")) || source.gene.def.defName == "Cheechin_AnthrodrakeTail")
        {
            color2 = PostProcessNoFurGene(source, pawn, pawn.story.SkinColor);
        }
        else
        {
            color2.r = ((1f - color1.r) * 0.8f) + color1.r;
            color2.b = ((1f - color1.b) * 0.8f) + color1.b;
            color2.g = ((1f - color1.g) * 0.8f) + color1.g;
            color2.a = 1f;
        }

        if (source.gene.def.defName == "Cheechin_AnthrogoatEars" || source.gene.def.defName == "Cheechin_AnthrogoatEars_Droopy")
            (color1, color2) = (color2, color1);

        return (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(original.path, ShaderTypeDefOf.CutoutComplex.Shader, Vector2.one, color1, color2);
    }

    public Color GetColorOne(PawnRenderNode source)
    {
        if (ModsConfig.AnomalyActive)
        {
            if (pawn.IsShambler)
                return MutantUtility.GetShamblerColor(colorOne);

            if (pawn.IsMutant && pawn.mutant.Def.useCorpseGraphics && pawn.mutant.rotStage == RotStage.Rotting)
                return PawnRenderUtility.GetRottenColor(colorOne);
        }

        return PostProcess(source, colorOne);
    }

    public Color GetColorTwo(PawnRenderNode source)
    {
        if (ModsConfig.AnomalyActive && colorTwo.HasValue)
        {
            if (pawn.IsShambler)
                return MutantUtility.GetShamblerColor(colorTwo.Value);

            if (pawn.IsMutant && pawn.mutant.Def.useCorpseGraphics && pawn.mutant.rotStage == RotStage.Rotting)
                return PawnRenderUtility.GetRottenColor(colorTwo.Value);

        }

        if (pawn.IsMutant && pawn.mutant.def.skinColorOverride.HasValue)
            return PostProcess(source, pawn.mutant.def.skinColorOverride.Value);

        if (colorTwo is null)
            return Color.white;

        var color = colorTwo.Value;
        color = PostProcess(source, color);
        return color;
    }

    private Color PostProcess(PawnRenderNode source, Color color)
    {
        color *= source.props.colorRGBPostFactor;

        if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Rotting)
            color = PawnRenderUtility.GetRottenColor(color);

        return color;
    }

    private static Color PostProcessNoFurGene(PawnRenderNode source, Pawn pawn, Color color)
    {
        color *= source.props.colorRGBPostFactor;

        if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Rotting)
            color = PawnRenderUtility.GetRottenColor(color);

        return color;
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Change fur",
                action = () => Find.WindowStack.Add(new Window_ColorPicker(this))
            };
        }
    }
        
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref furColor, "furColor");
        Scribe_Values.Look(ref colorOne, "colorOne");
        Scribe_Values.Look(ref colorTwo, "colorTwo");
    }
}