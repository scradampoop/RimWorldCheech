using UnityEngine;
using Verse;
using HarmonyLib;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Cheechin;

[HarmonyPatch(typeof(PawnRenderNode_Fur), "GraphicFor")]
public static class VanillaExpandedFramework_PawnRenderNode_Fur_GraphicFor_Patch2ssss
{
    public static void Postfix(PawnRenderNode_Fur __instance, Pawn pawn, ref Graphic __result)
    {
        if (__instance.gene != null)
        {
            GeneExtension extension = __instance.gene.def.GetModExtension<GeneExtension>();
            if (extension != null)
            {
                if (extension.useMaskForFur)
                {
                    __result = pawn.genes.GenesListForReading.Where(x => x.Active).Any(g => g.def.GetModExtension<GeneExtension>()?.useSkinColorForFur ?? false) ?
                                                                           GraphicDatabase.Get<Graphic_Multi>(pawn.story.furDef.GetFurBodyGraphicPath(pawn), ShaderDatabase.CutoutComplex, Vector2.one, pawn.story.SkinColor) :
                                                                           GraphicDatabase.Get<Graphic_Multi>(pawn.story.furDef.GetFurBodyGraphicPath(pawn), ShaderDatabase.CutoutSkinOverlay, Vector2.one, pawn.story.HairColor);
                    GeneDef;
                    PawnRenderNode_Fur;
                    HeadTypeDef;
                    XenotypeDef;
                    GeneEarsBase;
                    Graphic_Multi;
                    Gene_Skin;
                    BodyOverlay;
                    BodyTypeDef;
                    BodyTypeGraphicData;
                    FurDef;
                    PawnRenderNode;
                    PawnRenderNode_Body;
                    PawnRenderNodeWorker_Body;
                    PawnRenderNodeWorker_Fur;
                    PawnRenderNode_Fur;
                    PawnRenderNodeProperties_Overlay;
                    PawnRenderNode_Tattoo_Body;
                    PawnRenderNodeWorker_Body_Tattoo;
                    PawnRenderNodeWorker_Overlay
                    PawnRenderNodeProperties_Tattoo;
                    GeneExtension;
                }
                else if (extension.useSkinColorForFur)
                {
                    __result = GraphicDatabase.Get<Graphic_Multi>(pawn.story.furDef.GetFurBodyGraphicPath(pawn), ShaderUtility.GetSkinShader(pawn), Vector2.one, pawn.story.SkinColor);

                }
                else if (extension.useSkinAndHairColorsForFur)
                {
                    __result = GraphicDatabase.Get<Graphic_Multi>(pawn.story.furDef.GetFurBodyGraphicPath(pawn), ShaderDatabase.CutoutComplex, Vector2.one, pawn.story.SkinColor, pawn.story.HairColor);
                }
                else if (extension.dontColourFur)
                {
                    __result = GraphicDatabase.Get<Graphic_Multi>(pawn.story.furDef.GetFurBodyGraphicPath(pawn), ShaderDatabase.Cutout, Vector2.one, Color.white);
                }
            }
        }
    }
}
public static readonly Shader Cutout = ShaderDatabase.LoadShader("Map/Cutout");
    public static readonly Shader CutoutHair = ShaderDatabase.LoadShader("Map/CutoutHair");
    public static readonly Shader CutoutPlant = ShaderDatabase.LoadShader("Map/CutoutPlant");
    public static readonly Shader CutoutComplex = ShaderDatabase.LoadShader("Map/CutoutComplex");
    public static readonly Shader CutoutComplexBlend = ShaderDatabase.LoadShader("Map/CutoutComplexBlend");
    public static readonly Shader CutoutSkinOverlay = ShaderDatabase.LoadShader("Map/CutoutSkinOverlay");
    public static readonly Shader CutoutSkin = ShaderDatabase.LoadShader("Map/CutoutSkin");
    public static readonly Shader CutoutSkinColorOverride = ShaderDatabase.LoadShader("Map/CutoutSkinOverride");
    public static readonly Shader CutoutWithOverlay = ShaderDatabase.LoadShader("Map/CutoutWithOverlay");
    public static readonly Shader CutoutOverlay = ShaderDatabase.LoadShader("Map/CutoutOverlay");
    public static readonly Shader Transparent = ShaderDatabase.LoadShader("Map/Transparent");
    public static readonly Shader TransparentPostLight = ShaderDatabase.LoadShader("Map/TransparentPostLight");
    public static readonly Shader SolidColor = ShaderDatabase.LoadShader("Map/SolidColor");
    public static readonly Shader SolidColorBehind = ShaderDatabase.LoadShader("Map/SolidColorBehind");
    public static readonly Shader VertexColor = ShaderDatabase.LoadShader("Map/VertexColor");
