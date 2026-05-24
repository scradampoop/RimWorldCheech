namespace Cheechin;

/// <summary>
/// <paramref name="fallbackColor"/> will default back to white in order to prevent infinite recursion.
/// </summary>
public sealed class ColorRange(string? def, string? defHigh, ColorRange? fallbackColor, ColorRange? fallbackAlpha = null)
{
	public Color Low => Utility.Coalesce(def, fallbackColor?.Low ?? Color.white, fallbackAlpha?.Low);

	public Color High => Utility.Coalesce(defHigh ?? def, fallbackColor?.High ?? Color.white, fallbackAlpha?.High);

	public Color Average => Utility.Average(Low, High);

	public Color Variation => Utility.RandomBetween(Low, High);
}

/// <summary>
/// XML string fields and corresponding computed Color fields. RimWorld stores these as raw strings, parsed in ResolveReferences. Accepts "(R,G,B)" tuples and "RRGGBB" / "#RRGGBB" hex, including Alpha. High defaults to Low when omitted.
/// </summary>
public sealed class FurPatternTheme: DefModExtension
{
	/// <summary>
	/// skin is defaulted to white when omitted and is used as the final fallback for other colors. The skin alpha channel is also used as a fallback alpha for the ears, tail, and hair.
	/// </summary>
	public string? skin;
	public string? skinHigh;

	/// <summary>
	/// fill is used for the lower body and lower head layers, and is also effectively used for the ears and tail 'skin' layer in place of the red mask since the skin layer on those attachments is otherwise greyscale. When computing the fill for the ears and tail attachments, the alpha of the skin layer is used if we want to make a ghostlike effect.
	/// </summary>
	public string? fill;
	public string? fillHigh;

	/// <summary>
	/// accent is used by the upper body and upper head layers, and is also effectively used for the ears and tail 'skin' layer in place of the green mask since the skin layer on those attachments is otherwise greyscale. When computing the accent for the ears and tail attachments, the alpha of the skin layer is used if we want to make a ghostlike effect.
	/// </summary>
	public string? accent;
	public string? accentHigh;

	public string? headFill;
	public string? headFillHigh;

	public string? headAccent;
	public string? headAccentHigh;

	public string? earsFill;
	public string? earsFillHigh;

	public string? earsAccent;
	public string? earsAccentHigh;

	public string? tailFill;
	public string? tailFillHigh;

	public string? tailAccent;
	public string? tailAccentHigh;

	/// <summary>
	/// Hair fields and properties are not used yet since all Cheeches currently spawn as 'bald'.
	/// </summary>
	public string? hair;
	public string? hairHigh;

	/// <summary>
	/// Comma-delimited terms; a candidate fur pattern gene's defName must contain at least one of these (case-insensitive substring) to be considered acceptable for this theme. Empty/null means "any pattern is acceptable".
	/// </summary>
	public string? include;

	/// <summary>
	/// Comma-delimited terms; a candidate fur pattern gene's defName must contain none of these (case-insensitive substring) to be acceptable. Combined with <see cref="include"/> as an intersection: e.g. include="patches" + exclude="accent" allows GeneDef_Cheech_FurPatternFill_Patches but rejects GeneDef_Cheech_FurPatternAccent_Patches.
	/// </summary>
	public string? exclude;

	/// <summary>
	/// Weight of the "no pattern of this type" slot used in weighted selection for optional pattern gene types (fill/accent body and head). When null, defaults to the average of the candidate genes' selectionWeight values, which preserves the "skip is roughly as likely as one average pattern" semantic. Set to 0 to guarantee a pattern is always chosen (e.g. a striped theme should always have stripes). Ignored for ears/tail since those are required.
	/// </summary>
	public float? noPatternWeight;

	public bool AllowsPattern(string defName)
	{
		var includeTerms = include?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray() ?? [];
		var excludeTerms = exclude?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray() ?? [];

		return
			(includeTerms.Length == 0 || includeTerms.Any(t => defName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
			&& (excludeTerms.Length == 0 || !excludeTerms.Any(t => defName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
		;
	}

	public ColorRange SkinColor => new(skin, skinHigh, null);

	public ColorRange BodyFillColor => new(fill, fillHigh, SkinColor);
	public ColorRange BodyAccentColor => new(accent, accentHigh, BodyFillColor);

	public ColorRange HeadFillColor => new(headFill, headFillHigh, BodyFillColor);
	public ColorRange HeadAccentColor => new(headAccent, headAccentHigh, BodyAccentColor);

	public ColorRange EarsFillColor => new(earsFill, earsFillHigh, HeadFillColor, SkinColor);
	public ColorRange EarsAccentColor => new(earsAccent, earsAccentHigh, HeadAccentColor, SkinColor);

	public ColorRange TailFillColor => new(tailFill, tailFillHigh, BodyFillColor, SkinColor);
	public ColorRange TailAccentColor => new(tailAccent, tailAccentHigh, BodyAccentColor, SkinColor);

	public ColorRange HairColor => new(hair, hairHigh, HeadAccentColor, SkinColor);
}

[HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.SetXenotype), typeof(XenotypeDef))]
public static class HarmonyPatch_Pawn_GeneTracker_SetXenotype
{
	/// <summary>
	/// On Cheech xenotype assignment, lock in a single skin/theme gene first, then for each fur pattern gene type, filter candidates against the locked theme's include/exclude lists, dedupe to at most one, and re-roll the surviving gene's colors from the chosen theme.
	/// </summary>
	public static void Postfix(Pawn_GeneTracker __instance, XenotypeDef? xenotype)
	{
		if (!(xenotype?.defName?.Equals("XenotypeDef_Cheech", StringComparison.OrdinalIgnoreCase) ?? false))
			return;

		var random = new System.Random();

		// Locking in a single skin/theme gene weighted by GeneDef.selectionWeight (vanilla exclusionTags should already enforce one ahead of this method, but we dedupe defensively).
		var skinGenes = __instance.GenesListForReading.Where(g => g.def.defName.StartsWith("GeneDef_Cheech_Skin_", StringComparison.OrdinalIgnoreCase)).ToArray();
		var skinGeneToKeep = skinGenes.SelectByWeight(0f, random);

		foreach (var g in skinGenes.Where(g => g != skinGeneToKeep))
			__instance.RemoveGene(g);

		skinGeneToKeep?.overriddenByGene = null;

		var furPatternTheme = skinGeneToKeep?.def.GetModExtension<FurPatternTheme>();

		// For each pattern gene type, filter candidates against the theme, weighted-pick one (or none), and re-roll colors.
		foreach (var geneType in new[]{
			typeof(GeneFurPatternFill),
			typeof(GeneFurPatternAccent),
			typeof(GeneHeadPatternFill),
			typeof(GeneHeadPatternAccent),
			typeof(GeneEarsWithPattern),
			typeof(GeneTailWithPattern),
		})
		{
			var genes = __instance.GenesListForReading.Where(g => g.GetType() == geneType).ToArray();

			// Apply theme filter; ears/tail fall back to the unfiltered set if the filter would leave them empty, because a Cheech without ears or a tail is just sad.
			bool geneIsRequired = geneType == typeof(GeneEarsWithPattern) || geneType == typeof(GeneTailWithPattern);
			var geneCandidates = furPatternTheme == null ? genes : genes.Where(g => furPatternTheme.AllowsPattern(g.def.defName)).ToArray();

			if (geneIsRequired && geneCandidates.Length == 0)
				geneCandidates = genes;

			// Skip slot: 0 for required gene types or empty pools; otherwise the theme override or, by default, the average of candidate weights — keeping "no pattern" roughly as likely as any one average pattern, which mirrors the prior "+1 unweighted slot" behavior under uniform weights.
			var geneToKeep = geneCandidates.SelectByWeight(geneIsRequired || geneCandidates.Length == 0 ? 0f : furPatternTheme?.noPatternWeight ?? geneCandidates.Average(g => Math.Max(0f, g.def.selectionWeight)), random);

			foreach (var gene in genes.Where(g => g != geneToKeep))
				__instance.RemoveGene(gene);

			// We know it's a GeneFurPattern and we could have cast it earlier, but this also serves as a null check.
			if (geneToKeep is GeneFurPattern furPatternToKeep)
			{
				// Resetting colors to default so PostAdd re-rolls from the newly chosen skin/fur-pattern-theme gene.
				furPatternToKeep.colorOne = default;
				furPatternToKeep.colorTwo = default;
				furPatternToKeep.PostAdd();
				furPatternToKeep.overriddenByGene = null;
			}
		}
	}
}

public abstract class GeneFurPattern: Gene
{
	public Color colorOne;

	/// <summary>
	/// Only used by textures that have complex masks (e.g. ears and tail) and aren't using the body or head textures as a mask.
	/// </summary>
	public Color colorTwo;

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
		Scribe_Values.Look(ref colorOne, nameof(colorOne));
		Scribe_Values.Look(ref colorTwo, nameof(colorTwo));
		base.ExposeData();
	}
}

/// <summary>
/// Declaring separate classes for different gene types to set their initial color based on the skin them, and also so we can more readily find them via reflection.
/// </summary>
public sealed class GeneFurPatternFill: GeneFurPattern
{
	public override void PostAdd()
	{
		if (colorOne == default)
			colorOne = pawn.GetFurPatternTheme()?.BodyFillColor.Variation ?? pawn.story.SkinColor;

		base.PostAdd();
	}
}

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneFurPatternAccent: GeneFurPattern
{
	public override void PostAdd()
	{
		if (colorOne == default)
			colorOne = pawn.GetFurPatternTheme()?.BodyAccentColor.Variation ?? pawn.story.HairColor;

		base.PostAdd();
	}
}

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneHeadPatternFill: GeneFurPattern
{
	public override void PostAdd()
	{
		if (colorOne == default)
			colorOne = pawn.GetFurPatternTheme()?.HeadFillColor.Variation ?? pawn.story.SkinColor;

		base.PostAdd();
	}
}


/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneHeadPatternAccent: GeneFurPattern
{
	public override void PostAdd()
	{
		if (colorOne == default)
			colorOne = pawn.GetFurPatternTheme()?.HeadAccentColor.Variation ?? pawn.story.HairColor;

		base.PostAdd();
	}
}

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneEarsWithPattern: GeneFurPattern
{
	public override void PostAdd()
	{
		var furPatternTheme = pawn.GetFurPatternTheme();

		if (colorOne == default)
			colorOne = furPatternTheme?.EarsFillColor.Variation ?? pawn.story.SkinColor;

		if (colorTwo == default)
			colorTwo = furPatternTheme?.EarsAccentColor.Variation ?? pawn.story.HairColor;

		base.PostAdd();
	}
}

/// <inheritdoc cref="GeneFurPatternFill"/>
public sealed class GeneTailWithPattern: GeneFurPattern{
	public override void PostAdd()
	{
		var furPatternTheme = pawn.GetFurPatternTheme();

		if (colorOne == default)
			colorOne = furPatternTheme?.TailFillColor.Variation ?? pawn.story.SkinColor;

		if (colorTwo == default)
			colorTwo = furPatternTheme?.TailAccentColor.Variation ?? pawn.story.HairColor;

		base.PostAdd();
	}
}


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

public abstract class PawnRenderNode_HeadPattern<TGeneFurPattern>(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree) where TGeneFurPattern: GeneFurPattern
{
	public override Color ColorFor(Pawn pawnGraphicFor) => pawnGraphicFor.GetGene<TGeneFurPattern>()!.colorOne;

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

public sealed class PawnRenderNodeProperties_PatternMask: PawnRenderNodeProperties
{
	public string? maskPath;
}

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
			maskPath: geneEars.def.renderNodeProperties.OfType<PawnRenderNodeProperties_PatternMask>().FirstOrDefault()?.maskPath
		);
	}
}

public sealed class PawnRenderNode_TailWithPattern(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree): PawnRenderNode(pawn, props, tree)
{
	public override Graphic GraphicFor(Pawn pawnGraphicFor)
	{
		var geneTail = pawnGraphicFor.GetGene<GeneTailWithPattern>()!;
		return GraphicDatabase.Get<Graphic_Multi>(
			path: props.texPath,
			shader: ShaderDatabase.CutoutComplex,
			drawSize: Vector2.one,
			color: geneTail.colorOne,
			colorTwo: geneTail.colorTwo,
			data: null,
			maskPath: geneTail.def.renderNodeProperties.OfType<PawnRenderNodeProperties_PatternMask>().FirstOrDefault()?.maskPath
		);
	}
}