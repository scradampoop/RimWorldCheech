using Verse.AI;

namespace Cheechin;

[HarmonyPatch(typeof(Building_StylingStation), nameof(Building_StylingStation.GetFloatMenuOptions))]
public static class HarmonyPatch_Building_StylingStation_GetFloatMenuOptions
{
	public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> options, Pawn selPawn, Building_StylingStation __instance)
	{
		foreach (var option in options)
		{
			yield return option;
			if (option.Label == "ChangeStyle".Translate().CapitalizeFirst() && selPawn.genes?.GenesListForReading.OfType<GeneFurPattern>().Any(p => p.Active) == true)
			{
				yield return FloatMenuUtility.DecoratePrioritizedTask(
					new(
						"ColorPicker.ChangeFurPatternColors".Translate().CapitalizeFirst(),
						() => selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefsOf.Cheechin_ChangeFurPatternColors, __instance), JobTag.Misc)
					),
					selPawn,
					__instance
				);
			}
		}
	}
}

/// <summary>
/// Stable-sort dynamic gene render nodes by <c>Props.baseLayer</c> after the vanilla setup has yielded them. Otherwise <c>PawnRenderTree.SetupDynamicNodes</c> would receive them in <c>GenesListForReading</c> order (all xenogenes then all endogenes, each list in addition order),
/// which means: (a) a swapped gene's new instance is appended to the end of its list and ends up drawing over its supposed-to-be-higher-layer siblings, and (b) endogenes always render after all xenogenes regardless of <c>baseLayer</c>, which can hide xenogene graphics behind
/// a higher-layered endogene. The world renderer dodges this because its draw matrix Z incorporates <c>baseLayer</c> via <c>PawnRenderNodeWorker.AltitudeFor</c>; the portrait renders flat into a RenderTexture with no depth, so the children-array order is the only thing that
/// decides composition. Sorting here fixes both axes (swap order and xeno-vs-endo interleaving) in one place.
/// </summary>
[HarmonyPatch(typeof(DynamicPawnRenderNodeSetup_Genes), nameof(DynamicPawnRenderNodeSetup_Genes.GetDynamicNodes))]
public static class HarmonyPatch_DynamicPawnRenderNodeSetup_Genes_GetDynamicNodes
{
	public static void Postfix(ref IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> __result)
	{
		// Don't mess with it if it doesn't have any fur genes.
		if (!__result.Any(t => t.node.gene is GeneFurPattern))
			return;

		// OrderBy is a stable LINQ sort, so equal-layer nodes keep their original yield order. ToArray materializes the iterator once so a consumer's foreach doesn't re-enumerate it.
		__result = __result.OrderBy(t => t.node.Props.baseLayer).ToArray();
	}
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