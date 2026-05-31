# CLAUDE.md

This file guides Claude Code (claude.ai/code) when working in this repository. When a prompt reveals that something here is inaccurate or missing, update this file as part of your work so future sessions start with correct context.

## Project Overview

**Cheech Xenotype** is a RimWorld 1.6 mod — a catlike Biotech xenotype whose pawns are "Cheechers"/"Cheeches". Requires Harmony and the Biotech DLC. Root C# namespace is `Cheechin`; mod `packageId` is `Xenotype.Cheech`.

The mod is heavily visual. A Cheech is a stack of independently-colorable fur layers:
- a **base coat** (skin-colored fur covering the body),
- an optional **fill** pattern (lower layer — tuxedo, patches, mottling),
- an optional **accent** pattern (upper layer — stripes, spots, rosettes),
- analogous **head** fill/accent patterns, plus **ears** and a **tail** (each two-color).

A pawn's overall look is driven by a **skin/theme gene** (see Color & theme system below). Players can recolor every layer and swap pattern genes at the styling station via `Window_FurPatternColorPicker`.

## Building

```
dotnet build 1.6/src/1.6.csproj
```

- Project: `1.6/src/1.6.csproj`. Output: `1.6/Assemblies/CheechXenotype.dll` (`OutputPath` is `..\Assemblies`).
- `net48`, `LangVersion 14` (C# 14), nullable enabled, `Release` only, no debug symbols.
- **Global usings live in the csproj** (`<Using>` items: `RimWorld`, `HarmonyLib`, `UnityEngine`, `Verse`, `System`, `System.Collections.Generic`, `System.Linq`). Source files generally have no `using` lines — if you add a file needing another namespace (e.g. `System.IO`, `Verse.AI`), add a per-file `using` like the existing files do, or extend the csproj.
- `z_scratchpad.cs` is excluded from compilation (`<Compile Remove>`); treat it as a junk drawer.
- No test suite — validation is in-game. To smoke-test logic, build and load the mod, then use the **DEV: Fur Colors** dev gizmo on a Cheech (see `GeneFurPattern.GetGizmos`).

RimWorld assemblies are referenced by absolute `HintPath` from `C:\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\`. On a new machine, update the `<Reference>` paths in the csproj.

### Reading RimWorld internals

A full decompile of `Assembly-CSharp.dll` is at **`C:\rw\Assembly-CSharp\`** (namespaced subfolders, e.g. `Verse\PawnRenderTree.cs`, `RimWorld\Pawn_GeneTracker.cs`, `RimWorld\PortraitsCache.cs`, `RimWorld\PawnGenerator.cs`). When you need a method body, field visibility, or call path, **Grep/Read those files directly** — faster and more reliable than reflection-loading the DLL.

### Krafs.Publicizer

[Krafs.Publicizer](https://github.com/krafs/Publicizer) exposes these private members (declared in the csproj): `PawnRenderNode.props`, `Pawn.drawer`, `Dialog_ColorPickerBase.focusableControlNames`, `Dialog_ColorPickerBase.ButSize`. Add a `<Publicize>` item if you need another.

## Architecture

### Color & theme system (the heart of the mod)

The current model:

- **`FurPatternTheme`** (`DefModExtension`, in `GeneFurPattern.cs`) lives on each **skin gene** — the `GeneDef_Cheech_Skin_*` defs in `GeneDef_Cheech_Fur.xml`, which inherit vanilla `GeneSkinColorOverride`. A theme is the single source of truth for a Cheech's palette and which patterns it may wear. The presence of a `FurPatternTheme` on an active `GeneDef_Cheech_Skin_*` gene is what marks a pawn as a cheech (see `Utility.GetFurPatternTheme` and the `GeneratePawn` patch).
- Themes hold raw string color fields parsed lazily: `skin`, `fill`, `accent`, `headFill`, `headAccent`, `earsFill`, `earsAccent`, `tailFill`, `tailAccent`, `hair`, each with an optional `…High` partner. Strings accept `(R,G,B[,A])` tuples or `RRGGBB`/`#RRGGBB[AA]` hex (parsed by `Utility.ParseDefColor`).
- **`ColorRange`** wraps a low/high pair and exposes `Low`, `High`, `Average`, and `Variation` (a per-pawn random pick between low and high). Each layer's `ColorRange` declares a **fallback chain**: e.g. `EarsFillColor` → `HeadFillColor` → `BodyFillColor` → `SkinColor` → white, with skin's alpha threaded through as a fallback alpha for ears/tail (the "ghostlike" effect). Omitting `…High` makes High default to Low. Read the `ColorRange` properties on `FurPatternTheme` for the exact cascade before changing color logic.
- **Pattern selection** is also theme-driven via `include`/`exclude` (comma-delimited, case-insensitive substring match against candidate gene `defName`s) and `noPatternWeight` (weight of the "no pattern" slot for optional body/head fill+accent; `0` forces a pattern). `FurPatternTheme.AllowsPattern(defName)` is the intersection of include and exclude.

`Utility.cs` color helpers: `ParseDefColor`, `Coalesce`, `ColorWithAlpha`, `Average`, `RandomBetween`, `ToDefVal` (Color/float → XML string), `CalculateBrightnessLevel`, `WithBrightness`. Also `GetGene<T>`, `GetFurPatternTheme`, `IsSameXenotypeAs`, and `SelectByWeight<TGene>` (weighted random pick with an optional "skip" slot; clamps negative/NaN weights to 0, falls back to uniform if all weights are 0).

### Gene class hierarchy

All pattern genes inherit `GeneFurPattern : Gene` (`GeneFurPattern.cs`), which stores `colorOne` + `colorTwo`, serializes them in `ExposeData`, and exposes the **DEV: Fur Colors** gizmo. Each concrete subclass overrides `PostAdd` to roll its initial color(s) from the pawn's `FurPatternTheme` (via the matching `ColorRange.Variation`), falling back to the pawn's vanilla skin/hair color if there's no theme. Subclasses exist as distinct types so they can be found by `GetType()`/LINQ without string matching:

- `GeneFurPatternFill`, `GeneFurPatternAccent` — body layers (use `colorOne`).
- `GeneHeadPatternFill`, `GeneHeadPatternAccent` — head layers (use `colorOne`).
- `GeneEarsWithPattern`, `GeneTailWithPattern` — ears/tail (use **both** `colorOne` and `colorTwo`, since their textures carry complex two-color masks).

### Rendering pipeline

Pawns render via `PawnRenderNode` subclasses wired up in `<renderNodeProperties>` in the XML gene defs (all in `GeneFurPattern.cs`):

- `PawnRenderNode_BaseCoat` — base coat fur (layer 5), pawn skin color, `CutoutSkinOverlay`. Subclasses `PawnRenderNode` directly (not vanilla `PawnRenderNode_Fur`, to avoid hair-color override).
- `PawnRenderNode_FurPattern<TGene>` (abstract) — body patterns, `CutoutSkinOverlay`, masked against the fur body graphic so patterns respect body shape. Resolves the body-type-specific texture from `def.renderNodeProperties[].bodyTypeGraphicPaths`.
- `PawnRenderNode_HeadPattern<TGene>` (abstract) — head patterns, masked against `headType.graphicPath`.
- `PawnRenderNode_EarsWithPattern` (subclasses `PawnRenderNode_AttachmentHead`) and `PawnRenderNode_TailWithPattern` — `CutoutComplex` two-color masks; mask path comes from `PawnRenderNodeProperties_PatternMask.maskPath`.

Concrete sealed subclasses (`PawnRenderNode_FurPatternFill`, `…Accent`, `…HeadPatternFill`, `…HeadPatternAccent`) exist only so XML can reference them by name — generics can't be named in XML. `PawnRenderNodeProperties_PatternMask` is a `PawnRenderNodeProperties` subclass adding `maskPath`.

**Layer-order gotcha (portraits):** `<baseLayer>` only sets the draw matrix Z (`PawnRenderNodeWorker.LayerFor` → `AltitudeFor`). World rendering honors Z via its camera, but **portraits render flat into a depthless `RenderTexture`, so they composite in literal draw-call order.** Draw order comes from `PawnRenderNode.AppendRequests` walking `children` in array order, built by `DynamicPawnRenderNodeSetup_Genes.GetDynamicNodes` iterating `Pawn_GeneTracker.GenesListForReading` (= `xenogenes` then `endogenes`, each in addition order). Consequences: (a) a swapped gene's new instance lands at the end of its list and draws over higher-layer siblings; (b) endogenes always draw after all xenogenes regardless of `baseLayer`. **Fix:** `HarmonyPatch_DynamicPawnRenderNodeSetup_Genes_GetDynamicNodes` (in `HarmonyPatches.cs`) stable-sorts the yielded `(node, parent)` tuples by `node.Props.baseLayer` once, after the xeno→endo merge — fixing both axes at the source. It early-outs if no `GeneFurPattern` node is present.

After a gene swap, `Window_FurPatternColorPicker.RebuildRenderTreeForGeneChange` must **synchronously** call `pawn.drawer.renderer.renderTree.SetDirty()` + `PortraitsCache.SetDirty(pawn)` before the next portrait fetch — `PawnRenderer.SetAllGraphicsDirty()` alone defers via `LongEventHandler.ExecuteWhenFinished` and bails if the tree isn't `Resolved`, so it can miss the next portrait. It still chains `SetAllGraphicsDirty()` so silhouette + global-texture-atlas caches catch up.

### Harmony patches (`HarmonyPatches.cs`)

- `HarmonyPatch_Building_StylingStation_GetFloatMenuOptions` — injects "Change fur pattern colors" into the styling station float menu (after vanilla "Change style") when the pawn has an active `GeneFurPattern`. Queues `JobDriver_ChangeFurPatternColors`, which opens the color picker.
- `HarmonyPatch_DynamicPawnRenderNodeSetup_Genes_GetDynamicNodes` — the layer-order fix above.
- `HarmonyPatch_Pawn_GeneTracker_SetXenotype` — on Cheech xenotype assignment: weighted-pick one `GeneDef_Cheech_Skin_*` theme gene; then for each pattern gene type, filter candidates by the theme's include/exclude, weighted-pick one (or none for optional body/head types — ears/tail are required and fall back to the unfiltered set), dedupe the rest, and re-roll the survivor's colors from the chosen theme.
- `HarmonyPatch_PawnGenerator_GeneratePawn` — postfix on the *finished* pawn (runs after `PawnGenerator` overwrites hair/beard, so it's later than the `SetXenotype` postfix): applies per-pawn `skinColorOverride` variation and tints hair from the theme, and shaves head/beard by default (`KeepHairChance`/`KeepBeardChance`). Deliberately avoids vanilla Bald/NoBeard restriction genes so players can still restyle.

### Color picker UI (`Window_FurPatternColorPicker.cs`)

Single ~960-line file. Lets the player edit all **9 color slots** at once: `Skin`(0), `BodyFill`(1), `BodyAccent`(2), `HeadFill`(3), `HeadAccent`(4), `EarsFill`(5), `EarsAccent`(6), `TailFill`(7), `TailAccent`(8). Slot→gene `colorN` mapping: `EarsAccent` and `TailAccent` write `colorTwo`; everything else writes `colorOne` (see the `Rt`/`Ot` helpers). The Skin slot is synthetic — it edits `story.skinColorOverride`, not a gene.

It pairs a live portrait preview + pattern-slot select list with an HSV/RGB/Hex picker mostly ported from **Fluffy's ColourPicker** (https://github.com/fluffy-mods/ColourPicker, as of 2025-09-14): picker rect, hue/alpha sliders, current/previous swatches, and a persistent recent-colors strip via `RecentColours` (serialized to `Config/ColourPicker.xml`). The `TextField<T>` live-validated input helper is also from there. Players can also **swap the pattern gene** via a select list filling a slot (`SlotGeneType`/`IsOptionalSlot`/`SwapGene`); gene swaps apply immediately (Cancel rolls back colors but not swaps).

### Other gameplay (`Mod_Cheech_Xenotype.cs`)

- `Mod_Cheech_Xenotype` — entry point; runs `Harmony.PatchAll`. `ModSettings_Cheech_Xenotype` currently has only a placeholder setting.
- Pheromones: `Thought_PheromoneAttraction` + `ThoughtWorker_PheromoneAttraction` — same-xenotype social opinion bonus when the other pawn has `GeneDef_Cheech_Pheromones`.
- `JobDriver_ChangeFurPatternColors` — walks to the styling station and opens the picker.
- `DefsOf` — `[DefOf]` cache for `GeneDef_Cheech_Pheromones`, `Cheechin_ChangeFurPatternColors`.

### XML / Def structure

All content under `1.6/`:
- `Defs/` — one file per gene group. `GeneDef_Cheech_Fur.xml` is the big one: it defines `FurDef_Cheech_Tufts`, the base-coat gene, the `GeneCategoryDef`s, the `Cheechin_ChangeFurPatternColors` `JobDef`, and **all `GeneDef_Cheech_Skin_*` theme genes**. Other gene files: Ears, Head, HeadPatternFill/Accent, FurPatternFill/Accent, Tail, Pheromones. Also `XenotypeDef_Cheech`, `PawnKindDef_Cheech`, `FactionDef_Cheech`, `RulePackDef_Cheech` (name generation).
- `Patches/Patch.xml` — vanilla def patches (mostly adding the base-coat gene to nudity-thought `nullifyingGenes` so furred Cheeches aren't "naked", + a Ghoul `disablesGenes` entry).
- `Languages/English/Keyed/LanguageData.xml` — translation keys (e.g. `ColorPicker.ChangeFurPatternColors`).

Textures live at the **repo root** under `Textures/Cheech/{Ears,Fur,Head,Icons,Tail}` (not under `1.6/`), referenced in defs by path relative to `Textures/`. Each body-type texture set needs `Male`, `Female`, `Hulk`, `Fat`, `Thin`, `Child` (with `Baby` mapped to `Child`), each in `_south`/`_north`/`_east` directions. PSD sources are in `1.6/src/psd/`.
