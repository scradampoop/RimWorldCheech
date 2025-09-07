namespace Cheechin;

public sealed class FurPatternColors: DefModExtension
{
    public List<FurPatternColorDef> predefinedFurPatternColors;
}

public sealed class FurPatternColorDef: Def
{
    public Color colorOne;

    /// <summary>
    /// TBD: Either eliminate colorTwo or set to Color.white by default if we don't use it (yet?).
    /// </summary>
    public Color? colorTwo;

    public float selectionWeight;

    public int displayOrder;

    public bool blacklistPrimary;
}

/// <summary>
/// TBD: Based on Anthrosonae version. Skeptical this patch is necessary here, or at least, should be able to just call SetAllGraphicsDirty from given pawn param instead of the genes.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
public static class PawnGenerator_GeneratePawn_Patch
{
    public static void Postfix(ref Pawn? __result)
    {
        // __result.drawer.renderer.SetAllGraphicsDirty();
        __result?.GetGeneFurPatternFill()?.ApplyColors();
        __result?.GetGeneFurPatternAccent()?.ApplyColors();
    }
}

public class PawnRenderNode_FurPatternFill(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree)
{
    public override Graphic? GraphicFor(Pawn pawnGraphicFor) => GeneFurPattern.GraphicFor(pawnGraphicFor.GetGeneFurPatternFill()!);
}

public class PawnRenderNode_FurPatternAccent(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree)
{
    public override Graphic? GraphicFor(Pawn pawnGraphicFor) => GeneFurPattern.GraphicFor(pawnGraphicFor.GetGeneFurPatternAccent()!);
}

/// <summary>
/// Declaring separate classes for fill vs accent because the rendering code doesn't seem to like multiple render nodes of the same type.
/// </summary>
public sealed class GeneFurPatternFill: GeneFurPattern;

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneFurPatternAccent: GeneFurPattern;

public abstract class GeneFurPattern: Gene
{
    public FurPatternColorDef? furPatternColor;

    public Color colorOne;

    /// <summary>
    /// TBD: Either eliminate colorTwo or set to Color.white by default if we don't use it (yet?).
    /// </summary>
    public Color? colorTwo;

    /// <summary>
    /// Common renderer for both the fill and accent pattern genes.
    /// </summary>
    public static Graphic? GraphicFor(GeneFurPattern geneFurPattern)
    {
        // TODO: PawnRenderNode_FurPatternAccent and Fill are referenced by the base gene def, but the texture paths are defined by the derived gene defs. Is there a way to get the texture path and colors with fewer operations and without reflection? - or does it matter? Not sure how often this is called.
        var bodyTypeGraphicPaths = geneFurPattern.def.renderNodeProperties.FirstOrDefault(r => r.bodyTypeGraphicPaths?.Any() == true).bodyTypeGraphicPaths;
        var story = geneFurPattern.pawn.story;
        for (int index = 0; index < bodyTypeGraphicPaths.Count; ++index)
        {
            if (bodyTypeGraphicPaths[index].bodyType == story.bodyType)
            {
                return GraphicDatabase.Get<Graphic_Multi>(
                    path: bodyTypeGraphicPaths[index].texturePath,
                    shader: ShaderDatabase.CutoutSkinOverlay,
                    drawSize: Vector2.one,
                    color: geneFurPattern.colorOne,
                    colorTwo: geneFurPattern.colorTwo ?? Color.white,
                    data: null,
                    maskPath: story.furDef?.GetFurBodyGraphicPath(geneFurPattern.pawn) ?? story.bodyType.bodyNakedGraphicPath
                );
            }
        }
        return null;
    }

    public override void PostAdd()
    {
        base.PostAdd();
        var extension = def.GetModExtension<FurPatternColors>();
        if (extension.predefinedFurPatternColors.TryRandomElementByWeight(x => x.selectionWeight, out var result))
        {
            furPatternColor = result;
            colorOne = furPatternColor.colorOne;
            colorTwo = furPatternColor.colorTwo;
            ApplyColors();
        }
    }

    public void ApplyColors() => pawn?.drawer.renderer.SetAllGraphicsDirty();

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