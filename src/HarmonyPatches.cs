using Verse.AI;

namespace Cheechin;

/// <summary>
/// Makes it so a pawn can access the fur pattern color picker UI via the styling station.
/// </summary>
[HarmonyPatch(typeof(Building_StylingStation), nameof(Building_StylingStation.GetFloatMenuOptions))]
public static class HarmonyPatch_Building_StylingStation_GetFloatMenuOptions
{
	/// <inheritdoc cref="HarmonyPatch_Building_StylingStation_GetFloatMenuOptions"/>
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
	/// <inheritdoc cref="HarmonyPatch_DynamicPawnRenderNodeSetup_Genes_GetDynamicNodes"/>
	public static void Postfix(ref IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> __result)
	{
		// Don't mess with it if it doesn't have any fur genes.
		if (!__result.Any(t => t.node.gene is GeneFurPattern))
			return;

		// OrderBy is a stable LINQ sort, so equal-layer nodes keep their original yield order. ToArray materializes the iterator once so a consumer's foreach doesn't re-enumerate it.
		__result = __result.OrderBy(t => t.node.Props.baseLayer).ToArray();
	}
}

/// <summary>
/// On cheech xenotype assignment, lock in a single skin/theme gene first, then for each fur pattern gene type, filter candidates against the locked theme's include/exclude lists, dedupe to at most one, and re-roll the surviving gene's colors from the chosen theme.
/// </summary>
[HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.SetXenotype), typeof(XenotypeDef))]
public static class HarmonyPatch_Pawn_GeneTracker_SetXenotype
{
	/// <inheritdoc cref="HarmonyPatch_Pawn_GeneTracker_SetXenotype"/>
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

/// <summary>
/// Applies the resolved skin theme's story-level visuals to a freshly generated cheech: per-pawn skin color variation, and a shaved head/face by default (keeping the rolled hair/beard only rarely). Runs as a postfix
/// on the finished pawn because <see cref="PawnGenerator"/> overwrites <c>story.hairDef</c>/<c>beardDef</c> (and the hair color) after gene application, so the <see cref="HarmonyPatch_Pawn_GeneTracker_SetXenotype"/>
/// postfix is too early for these. We deliberately do NOT use the vanilla Bald/NoBeard restriction genes, so nothing blocks the player from restyling at the styling station afterward.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
public static class HarmonyPatch_PawnGenerator_GeneratePawn
{
	/// <summary>
	/// Sparingly letting a freshly generated cheech keep the hair/beard the vanilla generator rolled. Otherwise, they spawn shaved.
	/// </summary>
	private const float KeepHairChance = 0.10f;
	private const float KeepBeardChance = 0.05f;

	/// <summary>
	/// Independent per-pawn chance to draw the first name from the custom pool instead of keeping vanilla's.
	/// </summary>
	private const float FirstNameFromCheechPoolChance = 0.7f;

	/// <summary>
	/// Independent per-pawn chance to draw the surname from the custom pool instead of keeping vanilla's.
	/// </summary>
	private const float LastNameFromCheechPoolChance = 0.7f;

	/// <inheritdoc cref="HarmonyPatch_PawnGenerator_GeneratePawn"/>
	public static void Postfix(Pawn? __result)
	{
		if (__result?.genes == null || __result.story == null)
			return;

		// The FurPatternTheme lives only on Cheech skin genes, so its presence is what marks this pawn as a cheech.
		var theme = __result.genes.GenesListForReading.FirstOrDefault(g => g.Active && g.def.defName.StartsWith("GeneDef_Cheech_Skin_", StringComparison.OrdinalIgnoreCase))?.def.GetModExtension<FurPatternTheme>();
		if (theme == null)
			return;

		// Replacing the freshly generated cheech's first and/or last name with one drawn from the NameSetDef_Cheech pools. First and last are rolled independently (each at FirstNameFromCheechPoolChance or LastNameFromCheechPoolChance), so a pawn can end up with a custom first + vanilla last, the reverse, both, or neither.
		if (__result.Name is NameTriple originalName)
		{
			var sets = DefDatabase<NameSetDef_Cheech>.AllDefsListForReading;
			string firstName = originalName.First;
			string lastName = originalName.Last;
			CheechName? pickedFirstAndNick = null;

			// Vanilla names babies "Baby" (player faction). Keeping that intact and only considering a surname swap for babies.
			if (!__result.DevelopmentalStage.Baby() && Rand.Chance(FirstNameFromCheechPoolChance))
			{
				var firstNameCandidates = new List<CheechName>();
				foreach (var set in sets)
				{
					firstNameCandidates.AddRange(set.firstNames);
					if (__result.gender == Gender.Male)
						firstNameCandidates.AddRange(set.firstNamesMale);
					else if (__result.gender == Gender.Female)
						firstNameCandidates.AddRange(set.firstNamesFemale);
				}

				if (firstNameCandidates.TryRandomElement(out pickedFirstAndNick!))
					firstName = pickedFirstAndNick.first;
			}

			if (Rand.Chance(LastNameFromCheechPoolChance))
			{
				var lastNameCandidates = new List<string>();
				foreach (var set in sets)
					lastNameCandidates.AddRange(set.lastNames);
				lastNameCandidates.TryRandomElement(out lastName);
			}

			if (firstName != originalName.First || lastName != originalName.Last)
			{
				string nickname = originalName.Nick;

				// A custom first name can carry its own nickname (the cat's everyday name vs. its 'government name' they keep at the vet); that wins outright.
				if (!string.IsNullOrWhiteSpace(pickedFirstAndNick?.nick))
					nickname = pickedFirstAndNick!.nick!;
				// Otherwise, preserve the vanilla nickname, but if it was just an echo of the first/last we replaced, re-echo the new value so the pawn doesn't display a stale name fragment. (Vanilla shuffled nicks are often a copy of first/last.)
				else if (nickname == originalName.First)
					nickname = firstName;
				else if (nickname == originalName.Last)
					nickname = lastName;

				__result.Name = new NameTriple(firstName, nickname, lastName);
			}
		}

		// Per-pawn skin variation drawn from the theme's skin..skinHigh range — single source of truth with the fur and hair colors. (Bypasses vanilla's GeneTuning.SkinColorValueRange clamp, which is intended: Cheech skins are meant to be vivid sometimes.)
		__result.story.skinColorOverride = theme.SkinColor.Variation;
		// Tinting whatever hair/beard survived to match the theme. Setting story.HairColor also colors the beard.
		__result.story.HairColor = theme.HairColor.Variation;

		// Shaved by default; keeping the rolled hair/beard only sparingly. CanWantBeard already gates which pawns can grow one (e.g. male vs female).
		if (!(__result.story.hairDef != null && __result.story.hairDef != HairDefOf.Bald && Rand.Chance(KeepHairChance)))
			__result.story.hairDef = HairDefOf.Bald;

		if (__result.style != null && !(__result.style.CanWantBeard && Rand.Chance(KeepBeardChance)))
			__result.style.beardDef = BeardDefOf.NoBeard;
	}
}