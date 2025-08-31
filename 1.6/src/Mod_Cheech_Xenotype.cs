using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Cheechin;

public class ModSettings_Cheech_Xenotype: ModSettings
{
    public bool tempPlaceholderSetting = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref tempPlaceholderSetting, nameof(tempPlaceholderSetting));
    }
}

public class Mod_Cheech_Xenotype: Mod
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

public class Thought_PheromoneAttraction: Thought_SituationalSocial
{
    public override float OpinionOffset() => pawn.IsSameXenotypeAs(OtherPawn()) ? (OtherPawn().genes.HasGene(DefsOf.GeneDef_Cheech_Pheromones) ? 20 : 0) : 0;
}

public class ThoughtWorker_PheromoneAttraction: ThoughtWorker
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

/// <summary>
/// TODO: What is this attribute even for? - if it makes something hot-swappable while the app is running, how would anything even detect this, particularly since it's in a specific namespace?
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class HotSwappableAttribute: Attribute;

[HarmonyPatch(typeof(Building_StylingStation), nameof(Building_StylingStation.GetFloatMenuOptions))]
public static class Building_StylingStation_GetFloatMenuOptions_Patch
{
    public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> options, Pawn selPawn, Building_StylingStation __instance)
    {
        foreach (var option in options)
        {
            yield return option;
            if (option.Label == "ChangeStyle".Translate().CapitalizeFirst())
            {
                GeneFurPatternAccent geneFurPatternAccent = selPawn.GetGeneFurPatternAccent();
                if (geneFurPatternAccent != null)
                {
                    yield return FloatMenuUtility.DecoratePrioritizedTask(new("ColorPicker.ChangeFurPatternColors".Translate().CapitalizeFirst(), delegate
                    {
                        selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefsOf.Cheechin_ChangeFurPatternColors, __instance), JobTag.Misc);
                    }), selPawn, __instance);
                }
            }
        }
    }
}

public class JobDriver_ChangeFurPatternColors: JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        if (ModLister.CheckIdeology("Styling station"))
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedOrNull(TargetIndex.A);
            // TODO: Lambda syntax?
            yield return Toils_General.Do(delegate
            {
                Find.WindowStack.Add(new Window_ColorPicker(pawn.GetGeneFurPatternAccent()));
            });
        }
    }
}

[DefOf, HotSwappable, StaticConstructorOnStartup]
public static class DefsOf
{
    static DefsOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DefsOf));

    public static GeneDef GeneDef_Cheech_Pheromones;

    public static JobDef Cheechin_ChangeFurPatternColors;
}