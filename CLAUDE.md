# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository. When considering user prompts, researching, and making updates, Claude should automatically update the CLAUDE.md file to fix any inaccuracies or omissions that will help with future contexts.

## Project Overview

This is a RimWorld mod called **Cheech Xenotype** — a catlike xenotype where pawns are called "Cheechers" or "Cheeches". The mod is built on RimWorld's Biotech DLC xenotype system and requires Harmony. It targets RimWorld 1.6. The root C# namespace is `Cheechin`.

The mod is heavily visual: its primary features are layered fur patterns with per-pawn color customization. Cheechers have a base coat, an optional lower-layer fill pattern (e.g. tuxedo, patches), and an optional upper-layer accent pattern (e.g. jaguar spots, stripes), each independently colorable. Head, ears, and tail have analogous pattern genes. Colors are set by the player via a custom `Window_FurPatternColorPicker` accessible through the styling station.

## Building

The C# project lives at `1.6/src/1.6.csproj`. Build from Visual Studio or:

```
dotnet build 1.6/src/1.6.csproj
```

Output DLL is written directly to `1.6/Assemblies/CheechXenotype.dll`. There is no test suite — validation is done by loading the mod in-game.

RimWorld assemblies are referenced from `C:\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\`. If the path differs on a new machine, update the `HintPath` entries in `1.6/src/1.6.csproj`.

A full decompile of `Assembly-CSharp.dll` is available at `C:\rw\Assembly-CSharp\` (namespaced subfolders, e.g. `Verse\PawnRenderTree.cs`, `RimWorld\Pawn_GeneTracker.cs`, `RimWorld\PortraitsCache.cs`). When you need to understand a RimWorld internal — method bodies, field visibility, call paths, etc. — Grep/Read those files directly instead of reflection-loading the DLL. It's faster, more accurate, and works without needing to resolve Unity dependency assemblies.

The project targets `.NET Framework 4.8` with `LangVersion 14` (C# 14). Nullable reference types are enabled. `z_scratchpad.cs` is deliberately excluded from compilation.

## Architecture

### Rendering pipeline

RimWorld's gene system renders pawns via `PawnRenderNode` subclasses wired up in `<renderNodeProperties>` in XML gene defs. This mod defines a small hierarchy:

- `PawnRenderNode_BaseCoat` — renders the base coat fur (layer 5) using the pawn's skin color. Uses `CutoutSkinOverlay` shader.
- `PawnRenderNode_FurPattern<TGeneFurPattern>` (abstract) — renders body pattern textures (layers 6/7) with `CutoutSkinOverlay`, using the fur body graphic as a mask so patterns respect body shape.
- `PawnRenderNode_HeadPattern<TGeneFurPattern>` (abstract) — same idea for the head, masking against `headType.graphicPath`.
- `PawnRenderNode_EarsWithPattern` — renders ear textures with `CutoutComplex` (supports two-color masks).

Concrete sealed subclasses (`PawnRenderNode_FurPatternFill`, `PawnRenderNode_FurPatternAccent`, etc.) exist purely so XML defs can reference them by name — generics can't be referenced from XML.

**Layer-order gotcha:** `<baseLayer>` only sets the matrix Z (via `PawnRenderNodeWorker.LayerFor` → `AltitudeFor`). World rendering composes correctly because its camera honors that Z, but portraits are rendered into a flat `RenderTexture` with no depth, so they composite in literal draw-call order. Draw-call order is built by `PawnRenderNode.AppendRequests` walking `children` in array-index order, and `DynamicPawnRenderNodeSetup_Genes.GetDynamicNodes` builds that array by iterating `Pawn_GeneTracker.GenesListForReading` in list order — which is just `xenogenes` concatenated with `endogenes`, each in addition order. That means (a) a swapped gene's new instance lands at the end of its list and ends up drawing over its supposed-to-be-higher-layer siblings, and (b) endogenes always render after all xenogenes regardless of `baseLayer`, so a high-layer endogene can hide a low-layer xenogene's graphic. The fix lives in `HarmonyPatch_DynamicPawnRenderNodeSetup_Genes_GetDynamicNodes` (in `1.6/src/GeneFurPattern.cs`): a postfix that stable-sorts the yielded `(node, parent)` tuples by `node.Props.baseLayer` once, after the xeno-then-endo merge, fixing both axes (swap order and xeno/endo interleaving) at the source. After a gene swap, `Window_FurPatternColorPicker.RebuildRenderTreeForGeneChange` still has to synchronously call `pawn.Drawer.renderer.renderTree.SetDirty()` and `PortraitsCache.SetDirty(pawn)` to make sure the rebuild actually happens before the next portrait fetch — `PawnRenderer.SetAllGraphicsDirty()` does the same dirty-marking but defers through `LongEventHandler.ExecuteWhenFinished` and bails if the tree isn't yet `Resolved`, so it can miss the next portrait. Call both synchronous ones, and still chain `SetAllGraphicsDirty()` so silhouette + global-texture-atlas caches catch up too.

### Gene class hierarchy

All pattern genes inherit from `GeneFurPattern : Gene`, which stores `colorOne` and `colorTwo`, randomly selects initial colors from predefined presets on `PostAdd`, and serializes those colors via `ExposeData`. Separate sealed subclasses (`GeneFurPatternFill`, `GeneFurPatternAccent`, `GeneHeadPatternFill`, `GeneHeadPatternAccent`, `GeneEarsWithPattern`, `GeneTailWithPattern`) are used so they can be found by type via LINQ/reflection without string matching.

### Color system

`FurPatternColors` (a `DefModExtension` on each pattern gene def) holds a list of `FurPatternColorDef` entries (weighted color presets). On `PostAdd`, a random preset is selected to initialize `colorOne`/`colorTwo`.

`Window_FurPatternColorPicker` lets players change all 9 color slots (skin + body fill/accent + head fill/accent + ears fill/accent + tail fill/accent) simultaneously. It pairs a live portrait preview and a row of pattern-slot radio buttons (mod-specific) with an HSV/RGB/Hex color picker UI ported from Fluffy's ColourPicker (originally https://github.com/fluffy-mods/ColourPicker as of 2025-09-14) — picker rectangle, hue/alpha sliders, current/previous swatches, and a persistent "recent colors" strip backed by `RecentColours` (which serializes to `Config/ColourPicker.xml`). The `TextField<T>` helper for live-validated numeric/hex inputs lives in the same file.

`Utility.cs` has convenience extensions: `GetGene<T>`, `IsSameXenotypeAs`, `CalculateBrightnessLevel`, `WithBrightness`, and `ToDefVal` (Color → XML string).

### Harmony patches

- `Building_StylingStation_GetFloatMenuOptions_Patch` — injects a "Change fur pattern colors" option into the styling station float menu when the selected pawn has an active `GeneFurPattern` gene.
- `HarmonyPatch_Pawn_GeneTracker_SetXenotype` — when a pawn is assigned the Cheech xenotype, ensures at most one gene of each pattern type is kept (randomly), preventing duplicate layers.

### XML / Def structure

All content lives under `1.6/`:
- `Defs/` — one file per gene group. `GeneDef_Cheech_Fur.xml` also defines the `FurDef`, `GeneCategoryDef`s, `JobDef`, and `FurPatternColorDef` presets.
- `Patches/Patch.xml` — XML patches against vanilla defs.
- Textures are under the repo root `Textures/Cheech/` (not under `1.6/`) and are referenced in defs by path relative to `Textures/`.

Each body-type-specific texture set requires variants: `Male`, `Female`, `Hulk`, `Fat`, `Thin`, `Child` (and `Baby` mapped to `Child`), each with `_south`, `_north`, `_east` directions.

PSD source files for textures are in `1.6/src/psd/`.

### Krafs.Publicizer

The project uses [Krafs.Publicizer](https://github.com/krafs/Publicizer) to access private RimWorld members: `PawnRenderNode.props`, `Pawn.drawer`, `Dialog_ColorPickerBase.focusableControlNames`, and `Dialog_ColorPickerBase.ButSize`.
