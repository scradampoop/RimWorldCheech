using System.Reflection;
using System.Xml;
using Verse.AI;

namespace Cheechin;

public sealed class ModSettings_Cheech_Xenotype: ModSettings
{
	public bool tempPlaceholderSetting = true;

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref tempPlaceholderSetting, nameof(tempPlaceholderSetting));
	}
}

public sealed class Mod_Cheech_Xenotype: Mod
{
	public static Mod_Cheech_Xenotype? mod;
	public static ModSettings_Cheech_Xenotype? settings;

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
		listing.CheckboxLabeled("Placeholder setting.", ref settings!.tempPlaceholderSetting, "This doesn't do anything. This is just a placeholder for if/when Adam adds settings to this mod.");
		listing.End();
	}
}

public sealed class Thought_PheromoneAttraction: Thought_SituationalSocial
{
	public override float OpinionOffset() => pawn.IsSameXenotypeAs(OtherPawn()) ? (OtherPawn().genes.HasGene(DefsOf.GeneDef_Cheech_Pheromones) ? 20 : 0) : 0;
}

public sealed class ThoughtWorker_PheromoneAttraction: ThoughtWorker
{
	protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
	{
		if (!p.IsSameXenotypeAs(otherPawn) || !RelationsUtility.PawnsKnowEachOther(p, otherPawn))
			return false;

		if (otherPawn.genes.HasGene(DefsOf.GeneDef_Cheech_Pheromones))
			return ThoughtState.ActiveAtStage(1);

		return false;
	}
}

public sealed class JobDriver_ChangeFurPatternColors: JobDriver
{
	public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

	protected override IEnumerable<Toil> MakeNewToils()
	{
		if (ModLister.CheckIdeology("Styling station"))
		{
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedOrNull(TargetIndex.A);
			yield return Toils_General.Do(() => Find.WindowStack.Add(new Window_FurPatternColorPicker(pawn)));
		}
	}
}

/// <summary>
/// A formal first name plus an optional everyday nickname — cats famously carry a 'government name' on file at the vet
/// and a separate nickname everyone actually uses. Authored compactly in XML as <c>&lt;li&gt;Formal Name&lt;/li&gt;</c> or
/// <c>&lt;li&gt;Formal Name|Nickname&lt;/li&gt;</c>; when this name is rolled, a present <see cref="nick"/> becomes the pawn's
/// <see cref="NameTriple.Nick"/> (overriding the vanilla-derived one). Nicknames deliberately track only first names, not surnames.
/// </summary>
public sealed class CheechName
{
	/// <summary>
	/// The formal first name (the part on file at the vet).
	/// </summary>
	public string first = null!;

	/// <summary>
	/// The everyday nickname, or <c>null</c> if they just go by their <see cref="first"/> name.
	/// </summary>
	public string? nick;

	/// <summary>
	/// Custom XML parser invoked by reflection during def load (<c>DirectXmlToObject.LoadDataFromXmlCustomMethodName</c>). The element's text is the formal
	/// name, with an optional <c>"| nickname"</c> suffix — so a name list reads as plain <c>&lt;li&gt;</c> strings whether a given entry has a nick.
	/// </summary>
	public void LoadDataFromXmlCustom(XmlNode xmlNode)
	{
		first = xmlNode.InnerText.Trim();
		int pipeCharIndex = first.IndexOf('|');
		if (pipeCharIndex < 0)
			return;

		nick = first.Substring(pipeCharIndex + 1).Trim();
		first = first.Substring(0, pipeCharIndex).Trim();
	}
}

/// <summary>
/// A pool of custom first/last names for cheeches, authored in XML. Multiple defs of this type are aggregated by
/// <see cref="HarmonyPatch_PawnGenerator_GeneratePawn"/>, so name lists can be split across several files. Fill <see cref="firstNames"/>
/// and <see cref="lastNames"/> for a unisex pool; the gender-specific first-name lists are optional and merge in on top.
/// </summary>
public sealed class NameSetDef_Cheech: Def
{
	/// <summary>Unisex first names — always eligible, merged with the gender-specific pool when picking.</summary>
	public List<CheechName> firstNames = [];

	/// <summary>First names offered only to male cheeches (in addition to <see cref="firstNames"/>).</summary>
	public List<CheechName> firstNamesMale = [];

	/// <summary>First names offered only to female cheeches (in addition to <see cref="firstNames"/>).</summary>
	public List<CheechName> firstNamesFemale = [];

	/// <summary>Surnames, used for every gender.</summary>
	public List<string> lastNames = [];
}

[DefOf, StaticConstructorOnStartup]
public static class DefsOf
{
	static DefsOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DefsOf));

	public static GeneDef GeneDef_Cheech_Pheromones;

	public static JobDef Cheechin_ChangeFurPatternColors;
}