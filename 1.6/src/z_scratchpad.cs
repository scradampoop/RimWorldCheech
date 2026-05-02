using UnityEngine;
using Verse;
using HarmonyLib;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Cheechin;

/*
## Garbage:
   
   misc. commands scradam is keeping somewhere for reference:
   
   mklink /J "C:\\Steam\\steamapps\\common\\RimWorld\\Mods\\CheechXenoType" "C:\\rw\\RimWorldCheech"
   mklink /J "C:\\rw\\RimWorld" "C:\\Steam\\steamapps\\common\\RimWorld"
   mklink /J "C:\\rw\\workshop" "C:\\Steam\\steamapps\\workshop\\content\\294100"
    */
/// <summary>
/// This is just a garbage file scradam keeps around to quickly go to definitions in referenced assemblies and review source code and figure stuff out. This is garbage that is not part of the mod.
/// </summary>
[HarmonyPatch(typeof(PawnRenderNode_Fur), "GraphicFor")]
public static class VanillaExpandedFramework_PawnRenderNode_Fur_GraphicFor_Patch2ssss
{
    public static bool TryGetColorFromHex(string hex, out Color color)
    {
        color = Color.white;
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        if (hex.Length != 6 && hex.Length != 8)
            return false;

        int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
        int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
        int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
        int a = 255;

        if (hex.Length == 8)
            a = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);

        color = GenColor.FromBytes(r, g, b, a);
        return true;
    }

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
                    PawnRenderNodeWorker_AttachmentBody
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
}
public class TattooDef : StyleItemDef
{
    public TattooType tattooType;
    public bool visibleNorth = true;

    public override Graphic GraphicFor(Pawn pawn, Color color)
    {
        if (this.noGraphic)
            return (Graphic) null;
        string maskPath = this.tattooType == TattooType.Body ? pawn.story.bodyType.bodyNakedGraphicPath : pawn.story.headType.graphicPath;
        return GraphicDatabase.Get<Graphic_Multi>(this.texPath, this.overrideShaderTypeDef?.Shader ?? ShaderDatabase.CutoutSkinOverlay, Vector2.one, color, Color.white, (GraphicData) null, maskPath);
    }
}
public abstract class PawnRenderNode_Tattoo : PawnRenderNode
{
    protected const float TattooOpacity = 0.8f;

    public PawnRenderNode_Tattoo(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
        : base(pawn, props, tree)
    {
    }

    public override Color ColorFor(Pawn pawn)
    {
        Color color = base.ColorFor(pawn);
        color.a *= 0.8f;
        return color;
    }
}
public class PawnRenderNode_Tattoo_Head(
    Pawn pawn,
    PawnRenderNodeProperties props,
    PawnRenderTree tree) : PawnRenderNode_Tattoo(pawn, props, tree)
{
    public override GraphicMeshSet MeshSetFor(Pawn pawn)
    {
        return pawn.style?.FaceTattoo == null || pawn.style.FaceTattoo.noGraphic ? (GraphicMeshSet) null : HumanlikeMeshPoolUtility.GetHumanlikeHairSetForPawn(pawn);
    }

    public override Graphic GraphicFor(Pawn pawn)
    {
        if (!ModLister.CheckIdeology("Head tattoo"))
            return (Graphic) null;
        return pawn.style?.FaceTattoo == null || pawn.style.FaceTattoo.noGraphic ? (Graphic) null : pawn.style.FaceTattoo.GraphicFor(pawn, this.ColorFor(pawn));
    }
}
