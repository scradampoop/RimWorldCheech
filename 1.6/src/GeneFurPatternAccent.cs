namespace Cheechin;

public sealed class FurPatternColors: DefModExtension
{
    public List<FurPatternColorDef> predefinedFurPatternColors;
}

public sealed class FurPatternColorDef: Def
{
    public Color colorOne;

    /// <summary>
    /// Only used by textures that have complex masks and aren't using the body or head graphic as a mask.
    /// </summary>
    public Color colorTwo;

    /// <summary>
    /// The likelihood of this color being selected at random from the pool of predefined colors during pawn generation.
    /// </summary>
    public float selectionWeight;
}

[HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.SetXenotype), typeof(XenotypeDef))]
public static class HarmonyPatch_Pawn_GeneTracker_SetXenotype
{
    /// <summary>
    /// This removes all but at most one gene of each type, just to keep the pawn's gene list simpler than not.
    /// </summary>
    public static void Postfix(Pawn_GeneTracker __instance, XenotypeDef xenotype)
    {
        if (!(xenotype?.defName?.Equals("XenotypeDef_Cheech", StringComparison.OrdinalIgnoreCase) ?? false))
            return;

        var random = new System.Random();

        foreach (var geneType in new[]{
            typeof(GeneFurPatternFill),
            typeof(GeneFurPatternAccent),
            typeof(GeneHeadPatternFill),
            typeof(GeneHeadPatternAccent),
        })
        {
            var genes = __instance.GenesListForReading.Where(g => g.GetType() == geneType).ToArray();
            int geneIndexToKeep = random.Next(genes.Length + 1);
            var geneToKeep = geneIndexToKeep >= genes.Length ? null : genes[geneIndexToKeep];

            foreach (var gene in genes.Where(g => g != geneToKeep))
                __instance.RemoveGene(gene);

            if (geneToKeep != null)
                geneToKeep.overriddenByGene = null;
        }
    }
}

//[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
//public static class PawnGenerator_GeneratePawn_Patch
//{
//    public static void Postfix(ref Pawn? __result)
//    {
//        __result?.drawer.renderer.SetAllGraphicsDirty();
//    }
//}

public abstract class GeneFurPattern: Gene
{
    public Color colorOne;

    /// <summary>
    /// Only used by textures that have complex masks and aren't using the body graphic as a mask.
    /// </summary>
    public Color colorTwo;

    public override void PostAdd()
    {
        //var endoBodyAccents = pawn.genes.Endogenes.OfType<GeneHeadPatternAccent>();
        //var xenoBodyAccents = pawn.genes.Xenogenes.OfType<GeneHeadPatternAccent>();
        //var furryEndoGenes = pawn.genes.Endogenes
        //    .Where(g => 
        //        g.def.defName.Equals("GeneDef_Cheech_Fur_Tufted", StringComparison.OrdinalIgnoreCase)
        //        || g is GeneFurPatternFill
        //        || g is GeneFurPatternAccent
        //    )
        //    .OrderBy(g => g is GeneFurPatternAccent)
        //    .ThenBy(g => g is GeneFurPatternFill)
        //    .ThenBy(g => g.def.defName.Equals("GeneDef_Cheech_Fur_Tufted", StringComparison.OrdinalIgnoreCase))
        //    .ToArray();

        //foreach (var gene in furryEndoGenes)
        //{
        //    pawn.genes.Endogenes.Remove(gene);
        //    pawn.genes.Endogenes.Add(gene);
        //}

        //var xenoCoatGenes = pawn.genes.Xenogenes.Where(g => g.def.defName.Equals("GeneDef_Cheech_Fur_Tufted", StringComparison.OrdinalIgnoreCase)).ToArray();
        //foreach (var gene in xenoCoatGenes)
        //{
        //    pawn.genes.Xenogenes.Remove(gene);
        //    //pawn.genes.Xenogenes.Add(gene);
        //}

        var extension = def.GetModExtension<FurPatternColors>();
        if (extension.predefinedFurPatternColors.TryRandomElementByWeight(x => x.selectionWeight, out var randomPatternColor))
        {
            if (colorOne == default)
                colorOne = randomPatternColor.colorOne;

            if (colorTwo == default)
                colorTwo = randomPatternColor.colorTwo;
        }
        base.PostAdd();
    }

    //public override void PostRemove()
    //{
    //    var furryEndoGenes = pawn.genes.Endogenes
    //        .Where(g => g.overriddenByGene == this)
    //        .ToArray();

    //    if (furryEndoGenes.Length > 0)
    //    {
    //        furryEndoGenes[0].overriddenByGene = null;
    //        foreach(var gene in furryEndoGenes.Skip(1))
    //            gene.overriddenByGene = furryEndoGenes[0];
    //    }

    //    base.PostRemove();
    //}

    public override IEnumerable<Gizmo> GetGizmos()
    {
        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Fur Colors",
                action = () => Find.WindowStack.Add(new Window_FurPatternColorPicker(pawn))
            };
        }
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref colorOne, nameof(colorOne), Color.black);
        Scribe_Values.Look(ref colorTwo, nameof(colorTwo), Color.white);
        base.ExposeData();
    }
}

/// <summary>
/// Declaring separate classes for different gene types so we can more readily find them via reflection.
/// </summary>
public sealed class GeneFurPatternFill: GeneFurPattern;

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneFurPatternAccent: GeneFurPattern;

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneHeadPatternFill: GeneFurPattern;

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneHeadPatternAccent: GeneFurPattern;

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneEarsWithPattern: GeneFurPattern;

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneTailWithPattern: GeneFurPattern;

/// <summary>
/// Because we don't want to override skin with hair color like <see cref="PawnRenderNode_Fur"/> does.
/// </summary>
public class PawnRenderNode_BaseCoat(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree)
{
    protected override Shader DefaultShader => ShaderDatabase.CutoutSkinOverlay;

    public override Graphic GraphicFor(Pawn pawnGraphicFor) => GraphicDatabase.Get<Graphic_Multi>(pawnGraphicFor.story.furDef.GetFurBodyGraphicPath(pawnGraphicFor), ShaderFor(pawnGraphicFor), Vector2.one,  ColorFor(pawnGraphicFor));
}

public abstract class PawnRenderNode_FurPattern<TGeneFurPattern>(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree) where TGeneFurPattern: GeneFurPattern
{
    public override Color ColorFor(Pawn pawnGraphicFor) => pawnGraphicFor.GetGene<TGeneFurPattern>()!.colorOne;

    /// <summary>
    /// Common body renderer for both the fill and accent pattern genes.
    /// </summary>
    public override Graphic? GraphicFor(Pawn pawnGraphicFor)
    {
        // PawnRenderNode_FurPatternAccent and Fill are referenced by the base gene def, but the texture paths are defined by the derived gene defs. Is there a way to get the texture path and colors with fewer operations and without reflection? - or does it matter? Not sure how often this is called.
        var geneFurPattern = pawnGraphicFor.GetGene<TGeneFurPattern>()!;
        var bodyTypeGraphicPaths = geneFurPattern.def.renderNodeProperties.FirstOrDefault(r => r.bodyTypeGraphicPaths?.Any() == true).bodyTypeGraphicPaths;
        var story = pawnGraphicFor.story;
        for (int index = 0; index < bodyTypeGraphicPaths.Count; ++index)
        {
            if (bodyTypeGraphicPaths[index].bodyType == story.bodyType)
            {
                return GraphicDatabase.Get<Graphic_Multi>(
                    path: bodyTypeGraphicPaths[index].texturePath,
                    shader: ShaderDatabase.CutoutSkinOverlay,
                    drawSize: Vector2.one,
                    color: geneFurPattern.colorOne,
                    colorTwo: geneFurPattern.colorTwo,
                    data: null,
                    maskPath: story.furDef?.GetFurBodyGraphicPath(pawnGraphicFor) ?? story.bodyType.bodyNakedGraphicPath
                );
            }
        }
        return null;
    }
}

public abstract class PawnRenderNode_HeadPattern<TGeneFurPattern>(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode_FurPattern<TGeneFurPattern>(pawn, props, tree) where TGeneFurPattern: GeneFurPattern
{
    /// <summary>
    /// Common head pattern renderer for both the fill and accent pattern genes.
    /// </summary>
    public override Graphic GraphicFor(Pawn pawnGraphicFor)
    {
        var geneHeadPattern = pawnGraphicFor.GetGene<TGeneFurPattern>()!;
        return GraphicDatabase.Get<Graphic_Multi>(
            path: geneHeadPattern.def.renderNodeProperties.FirstOrDefault(r => r.texPath != null).texPath,
            shader: ShaderDatabase.CutoutSkinOverlay,
            drawSize: Vector2.one,
            color: geneHeadPattern.colorOne,
            colorTwo: geneHeadPattern.colorTwo,
            data: null,
            maskPath: pawnGraphicFor.story.headType.graphicPath
        );
    }
}

/// <summary>
/// We probably can't put generics into XML defs, so we have a few concrete pointers for the XML defs to reference.
/// </summary>
public sealed class PawnRenderNode_FurPatternFill(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode_FurPattern<GeneFurPatternFill>(pawn, props, tree);

/// <inheritdoc cref="PawnRenderNode_FurPatternFill"/>
public sealed class PawnRenderNode_FurPatternAccent(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode_FurPattern<GeneFurPatternAccent>(pawn, props, tree);

/// <inheritdoc cref="PawnRenderNode_FurPatternFill"/>
public sealed class PawnRenderNode_HeadPatternFill(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode_HeadPattern<GeneHeadPatternFill>(pawn, props, tree);

/// <inheritdoc cref="PawnRenderNode_FurPatternFill"/>
public sealed class PawnRenderNode_HeadPatternAccent(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode_HeadPattern<GeneHeadPatternAccent>(pawn, props, tree);

/// <inheritdoc cref="PawnRenderNode_FurPatternFill"/>
public sealed class PawnRenderNode_EarsWithPattern(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode_AttachmentHead(pawn, props, tree)
{
    public override Graphic GraphicFor(Pawn pawnGraphicFor)
    {
        var geneEars = pawnGraphicFor.GetGene<GeneEarsWithPattern>()!;
        return GraphicDatabase.Get<Graphic_Multi>(
            path: props.texPath,
            shader: ShaderDatabase.CutoutComplex,
            drawSize: Vector2.one,
            color: geneEars.colorOne,
            colorTwo: geneEars.colorTwo,
            data: null,
            maskPath: null
        );
    }
}