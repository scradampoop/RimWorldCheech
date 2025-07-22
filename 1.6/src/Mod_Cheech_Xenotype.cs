using HarmonyLib;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Cheechin;

public class ModSettings_Cheech_Xenotype : ModSettings
{
    public bool tempPlaceholderSetting = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref tempPlaceholderSetting, nameof(tempPlaceholderSetting));
    }
}

public class Mod_Cheech_Xenotype : Mod
{
    public static Mod_Cheech_Xenotype mod;
    public static ModSettings_Cheech_Xenotype settings;

    public Mod_Cheech_Xenotype(ModContentPack content) : base(content)
    {
        mod = this;
        settings = GetSettings<ModSettings_Cheech_Xenotype>();
        new Harmony("Xenotype.Cheech").PatchAll(Assembly.GetExecutingAssembly());
    }

    public override string SettingsCategory() => "Xenotype - Cheech";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.CheckboxLabeled("Placeholder setting.", ref settings.tempPlaceholderSetting, "This doesn't do anything. This is just a placeholder for if/when Adam adds settings to this mod.");
        listing.End();
    }
}

[DefOf]
public static class DefOf_Cheech_XenotypeMod
{
    static DefOf_Cheech_XenotypeMod()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(DefOf_Cheech_XenotypeMod));
    }

    public static XenotypeDef XenotypeDef_Cheech;

    public static GeneDef GeneDef_Cheech_Pheromones;
}


public static class UtilCheech
{
    public static bool SameXenotype(Pawn? pawn, Pawn? other) => pawn?.genes?.Xenotype == other?.genes?.Xenotype;
}

public class Thought_PheromoneAttraction : Thought_SituationalSocial
{
    public override float OpinionOffset()
    {
        return UtilCheech.SameXenotype(pawn, OtherPawn()) ? (OtherPawn().genes.HasGene(DefOf_Cheech_XenotypeMod.GeneDef_Cheech_Pheromones) ? 20 : 0) : 0;
    }
}

public class ThoughtWorker_PheromoneAttraction : ThoughtWorker
{
    protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
    {
        if (!UtilCheech.SameXenotype(p, otherPawn) || !RelationsUtility.PawnsKnowEachOther(p, otherPawn))
        {
            return false;
        }
        if (otherPawn.genes.HasGene(DefOf_Cheech_XenotypeMod.GeneDef_Cheech_Pheromones))
        {
            return ThoughtState.ActiveAtStage(1);
        }
        return false;
    }
}

public class PawnRenderNode_BodyPattern(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree)
{
    public override Graphic? GraphicFor(Pawn pawnGraphicFor)
    {
        for (int index = 0; index < props.bodyTypeGraphicPaths.Count; ++index)
        {
            if (props.bodyTypeGraphicPaths[index].bodyType == pawnGraphicFor.story.bodyType)
            {
                return GraphicDatabase.Get<Graphic_Multi>(
                    path: props.bodyTypeGraphicPaths[index].texturePath,
                    shader: ShaderDatabase.CutoutSkinOverlay,
                    drawSize: Vector2.one,
                    color: props.colorType == PawnRenderNodeProperties.AttachmentColorType.Hair ? pawnGraphicFor.story.HairColor : pawnGraphicFor.story.SkinColor,
                    colorTwo: Color.white,
                    data: null,
                    maskPath: pawnGraphicFor.story.furDef?.GetFurBodyGraphicPath(pawnGraphicFor) ?? pawnGraphicFor.story.bodyType.bodyNakedGraphicPath
                );
            }
        }
        return null;
    }
}