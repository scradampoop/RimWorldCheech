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

/// <summary>
/// Inherited from Anthrosonae source code. Not sure if it's needed.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
public static class PawnGenerator_GeneratePawn_Patch
{
    public static void Postfix(ref Pawn? __result) => __result?.drawer.renderer.SetAllGraphicsDirty();
}

public abstract class GeneFurPattern: Gene
{
    public FurPatternColorDef? furPatternColor;

    public Color colorOne;

    /// <summary>
    /// Only used by textures that have complex masks and aren't using the body graphic as a mask.
    /// </summary>
    public Color colorTwo;

    public override void PostAdd()
    {
        base.PostAdd();
        var extension = def.GetModExtension<FurPatternColors>();
        if (extension.predefinedFurPatternColors.TryRandomElementByWeight(x => x.selectionWeight, out var result))
        {
            furPatternColor = result;
            colorOne = furPatternColor.colorOne;
            colorTwo = furPatternColor.colorTwo;
            pawn?.drawer.renderer.SetAllGraphicsDirty();
        }
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Fur pattern colors",
                action = () => Find.WindowStack.Add(new Window_FurPatternColorPicker(pawn))
            };
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref furPatternColor, nameof(furPatternColor));
        Scribe_Values.Look(ref colorOne, nameof(colorOne));
        Scribe_Values.Look(ref colorTwo, nameof(colorTwo));
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