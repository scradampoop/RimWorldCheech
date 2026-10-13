# CLAUDE.md

This file guides Claude Code (claude.ai/code) when working in this repository. When a prompt reveals that something here is inaccurate or missing, update this file as part of your work so future sessions start with correct context.

## Project Overview

**Cheech Xenotype** is a RimWorld 1.6 + 1.7 mod — a catlike Biotech xenotype whose pawns are "Cheechers"/"Cheeches". Requires Harmony and the Biotech DLC. Root C# namespace is `Cheechin`; mod `packageId` is `Xenotype.Cheech`.

The mod is heavily visual. A Cheech is a stack of independently-colorable fur layers:
- a **base coat** (skin-colored fur covering the body),
- an optional **fill** pattern (lower layer — tuxedo, patches, mottling),
- an optional **accent** pattern (upper layer — stripes, spots, rosettes),
- analogous **head** fill/accent patterns, plus **ears** and a **tail** (each two-color).

A pawn's overall look is driven by a **skin/theme gene** (see Color & theme system below). Players can recolor every layer and swap pattern genes at the styling station via `Window_FurPatternColorPicker`.

## Building

```
dotnet build src/CheechXenotype.csproj
```

- Solution: `Cheech.Xenotype.slnx` (XML solution format). Besides the code project, it holds `ModRoot.csproj`, which isn't built (`<Build Project="false" />`); it's just a view of the mod's content folders in the IDE.

- **One multitargeted project builds every supported game version** (`<TargetFrameworks>net48;net10.0</TargetFrameworks>`). Each TFM maps to a game version via the `RimWorldVersion` / `RimWorldManaged` properties at the top of the csproj:
  | TFM | Game | References from | Output |
  |---|---|---|---|
  | `net48` | 1.6 (Unity/Mono) | `C:\rw\RimWorld16\RimWorldWin64_Data\Managed\` (offloaded copy of the 1.6 install) — `Assembly-CSharp` + `UnityEngine.*Module` DLLs | `1.6/Assemblies/` |
  | `net10.0` | 1.7 (Waylith engine, .NET 10) | `C:\Steam\steamapps\common\RimWorld\` (game root; Steam's install is now 1.7) — `Assembly-CSharp` + `UnityStubs` | `1.7/Assemblies/` |
- Version-specific code: use `#if RW_1_6` / `#if RW_1_7` (defined per TFM in the csproj), or the SDK's `NETFRAMEWORK` / `NET`.
- To drop a version, remove its TFM from `<TargetFrameworks>`; its last-built DLL stays in place (and freeze its defs — see XML / Def structure).
- `AppendTargetFrameworkToOutputPath` is `false` (so output lands directly in `<version>/Assemblies`), which also drops the TFM from `obj\` — hence the explicit per-TFM `IntermediateOutputPath`. Don't remove it, or the two targets overwrite each other's intermediates.
- `LangVersion 14` (C# 14), nullable enabled, no debug symbols, `Optimize` on unconditionally (`dotnet build` uses the Debug configuration by default, but it produces the same thing).
- **Global usings live in the csproj** (`<Using>` items: `RimWorld`, `HarmonyLib`, `UnityEngine`, `Verse`, `System`, `System.Collections.Generic`, `System.Linq`). Source files generally have no `using` lines — if you add a file needing another namespace (e.g. `System.IO`, `Verse.AI`), add a per-file `using` like the existing files do, or extend the csproj.
- `z_scratchpad.cs` is excluded from compilation (`<Compile Remove>`); treat it as a junk drawer.
- No test suite — validation is in-game. To smoke-test logic, build and load the mod, then use the **DEV: Fur Colors** dev gizmo on a Cheech (see `GeneFurPattern.GetGizmos`).

RimWorld assemblies are referenced via `$(RimWorldManaged)` (set per TFM). On a new machine, update the two `RimWorldManaged` paths in the csproj.

### RimWorld 1.7 (Waylith) notes

1.7 replaced Unity with Ludeon's own **Waylith** engine on a custom .NET 10 runtime (`RimWorld.runtimeconfig.json`: `net10.0`, `Microsoft.NETCore.App 10.0.10-waylithclr.dev`). Its managed DLLs (`Assembly-CSharp.dll`, `Waylith*.dll`, `UnityStubs.dll`, the BCL) sit in the **game root**, not `RimWorldWin64_Data\Managed\`. There is no Unity: the runtime redirects `UnityEngine*`/`Unity.*` assembly references to `UnityStubs.dll`, which provides the `UnityEngine` namespace, and turns on many `WaylithClr.Lax*` compatibility switches — which is why the net48 1.6 DLL also loads and works on 1.7 unchanged. The Harmony mod ships a separate net10 `0Harmony.dll` for 1.7 via its own `LoadFolders.xml` — and it is **Harmony 3.0** (1.6 gets 2.4.3). We compile both targets against `Lib.Harmony` 2.4.3; that works on 1.7 because the runtime binds `0Harmony` by name and Harmony 3 keeps the API we use (`PatchAll`, `[HarmonyPatch]`, postfix injections). Harmony 3 adds things (e.g. Infix patches) and was still `-rc.2` in its repo as of 2026-10-12. `ModUpdating.txt` has no 1.7 changelog.

What else is in the 1.7 install (surveyed 2026-10-12; shader/texture items verified in the decompiles):
- **Assemblies worth decompiling:** `Assembly-CSharp` (the game, still ~all of it), `UnityStubs` (Ludeon's reimplementation of the Unity API — read it when `Texture2D`/`Material`/`RenderTexture`/`GUI`/`Graphics` behave differently from Unity), `RimWorld.dll` (small host: bootstrap + `RimWorldWaylith.Rendering` map/world/post-FX/screen- and camera-capture passes), and `Waylith.dll` (the engine/renderer — only needed for deep rendering work). Skip `Brutal.*` (low-level Vulkan/GLFW/ImGui/Slang bindings), `AssetsTools.NET`/`AssetRipper`/`Fmod5Sharp` (they read legacy Unity asset bundles), and the BCL.
- **`Assembly-CSharp` 1.6 → 1.7 type diff** is small: +66/−26. New: `Verse.ShaderDef`, `ShaderDefRegistry`, `ShaderWarmup`, `MaterialDef` (+ `Material{Color,Float,Texture,Vector,Named}Property`, `MaterialDefUtility`), `LudeonTK.GenTexture`, and a dev frame profiler (`Verse.ProfilerUI.*`, `Window_FrameProfiler`, with a VRAM table). Removed: only Unity Burst/codegen artifacts and a few bundled libraries now supplied by the BCL. `ShaderDatabase` still exposes the same static fields (`CutoutComplex`, `CutoutSkinOverlay`, …).
- **Shaders are now plain-text GLSL plus XML defs.** Each shader is a `ShaderDef` (`Data/Core/Defs/ShaderDefs/Shaders.{Map,World}.xml`: file name, blend/depth/stencil, render queue) whose source is in `Data/Core/Shaders/{Map,World}/*.glsl`, compiled to SPIR-V at runtime (`*.spv-cache` beside each file). `CutoutComplex.frag.glsl` confirms the two-color mask math documented below (`f *= lerp(1, _Color, mask.r); f *= lerp(1, _ColorTwo, mask.g)`, with `MaskGate.glsl` gating the mask). **Mods can ship shaders:** `ShaderDefRegistry.RegisterShaderDirectories` adds `<loadFolder>/Shaders/` for every running mod as an override search directory (so a mod's `.glsl` file also shadows a vanilla one of the same name), registers every `ShaderDef` in the DefDatabase, and watches mod-folder (`ContentSource.ModsFolder`) shader directories for hot reload. `ShaderDatabase.LoadShader` now resolves a `ShaderTypeDef` path by its last segment as a `ShaderDef` defName (no more `Resources.Load`); an unknown name no longer warns and falls back to the default shader — it becomes a late-bound shader. A custom shader would be 1.7-only (1.6 can't load GLSL), so its defs/files belong in `1.7/` and any C# use behind `#if RW_1_7`. `MaterialDef` lets XML define materials (shader, textures, floats, colors, keywords), apparently looked up by texture resource path.
- **Textures go through a new pipeline:** `ModContentLoader` now also accepts `.psd` and `.dds` (a `.dds` shadows a same-named `.png`). With the texture-compression pref on, mod textures are block-compressed via `UnityStubs` `Texture2D.Compress` → Waylith (`TextureCompressionSettings`, configured in `RimWorld-dll/Bootstrap.cs`): default target **BC7** (BC1 disabled; single-channel images become BC4), skipped for textures ≤16px, paths containing `/LUTs/`, or files whose name ends in **`_Uncompressed`**. 1.6 used Unity's BC3/DXT5 compute encoder. BC7 handles a pure red/green dither within a 4×4 block well, so the masks should be fine, but mip generation also changed (compute shaders under `Rendering/Shaders/Compute/`: Kaiser/DPID filters, alpha-coverage preservation) — recheck the dithered ear/tail masks when zoomed out on 1.7. `GraphicData` gained `textureRepeats` and `borderMipMaps`. Core art now ships in `Assets.zip` archives (game root and each DLC folder) rather than Unity asset bundles.
- **Runtime switches in `RimWorld.runtimeconfig.json` that matter to mods:** `JitPatchableAssemblies: Assembly-CSharp` (Harmony patches on game code are safe from inlining; patching `UnityStubs`/BCL methods may not be — all our patches target `Assembly-CSharp`), `DynamicMethodOwnerIsDeclaringType` / `JitNoInlinePatchableIntoDynamicMethods` (more Harmony support), `SkipAccessChecks` (private access works at runtime, which is what makes Publicizer-built code safe; the compiler still needs Publicizer), `MonoStringHashCode` / `MonoFloatFormatPrecision` (keep Mono's string hashing and float `ToString` output, so `ToDefVal` formatting is unchanged).

### Reading RimWorld internals

Decompiles (JetBrains dotPeek output, namespaced subfolders). When you need a method body, field visibility, or call path, **Grep/Read these directly** — faster and more reliable than reflection-loading DLLs:
- **`C:\rw\Assembly-CSharp\`** — **1.7** game code (e.g. `Verse\PawnRenderTree.cs`, `RimWorld\Pawn_GeneTracker.cs`, `RimWorld\PortraitsCache.cs`, `RimWorld\PawnGenerator.cs`).
- **`C:\rw\Assembly-CSharp16\`** — 1.6 game code, **but from build 1.6.9328**, older than the final 1.6.9676 in `C:\rw\RimWorld16`. Diffs against it mix late-1.6 patch changes with 1.7 changes (e.g. gravship/sealable classes look "new"); re-decompile `C:\rw\RimWorld16\RimWorldWin64_Data\Managed\Assembly-CSharp.dll` for an exact 1.6 baseline. Decompiler headers differ, so skip the first ~5 lines when diffing.
- **`C:\rw\UnityStubs\`** — 1.7's Unity API reimplementation. **`C:\rw\Waylith\`** — the engine/renderer. **`C:\rw\RimWorld-dll\`** — the 1.7 host (`Bootstrap.cs` configures texture compression, etc.).
- **`C:\rw\Harmony\`** — Harmony *source* (git clone, not a decompile): `master` = 2.4.3, `origin/v3` = 3.0.

### Krafs.Publicizer

[Krafs.Publicizer](https://github.com/krafs/Publicizer) exposes these private members (declared in the csproj): `PawnRenderNode.props`, `Pawn.drawer`, `Dialog_ColorPickerBase.focusableControlNames`, `Dialog_ColorPickerBase.ButSize` (the two `Dialog_ColorPickerBase` entries are currently unused by the code). Add a `<Publicize>` item if you need another. Publicizer is compile-time only (it builds a publicized reference copy of `Assembly-CSharp` per TFM); at runtime, Mono (1.6) and WaylithClr's `SkipAccessChecks` (1.7) both allow the resulting private access.

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

**Layer-order gotcha (portraits):** `<baseLayer>` only sets the draw matrix Z (`PawnRenderNodeWorker.LayerFor` → `AltitudeFor`). World rendering honors Z via its camera, but **portraits draw each node immediately (`PawnCacheRenderer.OnPostRender` → `PawnRenderTree.Draw` with `parms.DrawNow` → `GenDraw.DrawMeshNowOrLater` → `Graphics.DrawMeshNow`), so they composite in literal draw-call order. The portrait `RenderTexture` does have a 24-bit depth buffer, but in practice it doesn't sort these blended overlay draws. The same is true in 1.6 and 1.7.** Draw order comes from `PawnRenderNode.AppendRequests` walking `children` in array order, built by `DynamicPawnRenderNodeSetup_Genes.GetDynamicNodes` iterating `Pawn_GeneTracker.GenesListForReading` (= `xenogenes` then `endogenes`, each in addition order). Consequences: (a) a swapped gene's new instance lands at the end of its list and draws over higher-layer siblings; (b) endogenes always draw after all xenogenes regardless of `baseLayer`. **Fix:** `HarmonyPatch_DynamicPawnRenderNodeSetup_Genes_GetDynamicNodes` (in `HarmonyPatches.cs`) stable-sorts the yielded `(node, parent)` tuples by `node.Props.baseLayer` once, after the xeno→endo merge — fixing both axes at the source. It early-outs if no `GeneFurPattern` node is present.

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

Mod layout (multi-version, using RimWorld's default load-folder lookup — there is deliberately **no `LoadFolders.xml`**):
- root — `About/`, `Defs/`, `Patches/`, `Languages/`, `Textures/`, shared by all versions. 1.7 is the source of truth; edit defs here.
- `1.6/`, `1.7/` — each holds only that version's `Assemblies/` (build output).
- `src/` — the C# project, PSDs (`src/psd/`), and helper scripts. Not loaded by the game.

How loading works (`ModContentPack.InitLoadFolders` in the decompile): `About/About.xml` is read from a fixed path, outside the load-folder system. Every other content type looks for its own named subfolder (`Defs/`, `Patches/`, `Assemblies/`, `Textures/`, `Sounds/`, `Languages/`) inside each folder in the mod's load-folder list — never recursing into sibling folders like `1.6/` or `src/`. Without `LoadFolders.xml`, that list is `[best version folder, Common/ if present, root]`. The best version folder is the newest one at or below the running version, or, if there is none, *any* version folder, even a newer one. Later folders override earlier ones for files at the same relative path (version folder wins over root).

To **freeze** a version (stop building/updating it): remove its TFM from the csproj, then move root `Defs/` + `Patches/` into `1.7/` (still live) and copy them into the frozen version's folder. Still no `LoadFolders.xml` needed. After that, don't rename or delete textures the frozen defs still reference — `Textures/` stays shared.

Def/patch files:
- `Defs/` — one file per gene group. `GeneDef_Cheech_Fur.xml` is the big one: it defines `FurDef_Cheech_Tufts`, the base-coat gene, the `GeneCategoryDef`s, the `Cheechin_ChangeFurPatternColors` `JobDef`, and **all `GeneDef_Cheech_Skin_*` theme genes**. Other gene files: Ears, Head, HeadPatternFill/Accent, FurPatternFill/Accent, Tail, Pheromones. Also `XenotypeDef_Cheech`, `PawnKindDef_Cheech`, `FactionDef_Cheech`, `RulePackDef_Cheech` (name generation).
- `Patches/Patch.xml` — vanilla def patches (mostly adding the base-coat gene to nudity-thought `nullifyingGenes` so furred Cheeches aren't "naked", + a Ghoul `disablesGenes` entry).
- `Languages/English/Keyed/LanguageData.xml` — translation keys (e.g. `ColorPicker.ChangeFurPatternColors`).

Textures live at the **repo root** under `Textures/Cheech/{Ears,Fur,Head,Icons,Tail}` (not under a version folder), referenced in defs by path relative to `Textures/`. Each body-type texture set needs `Male`, `Female`, `Hulk`, `Fat`, `Thin`, `Child` (with `Baby` mapped to `Child`), each in `_south`/`_north`/`_east` directions. PSD sources are in `src/psd/`.

**Two-color masks (`CutoutComplex`, ears/tail `m/` folders):** the shader is *multiplicative* — roughly `tex × lerp(1, colorOne, mask.r) × lerp(1, colorTwo, mask.g)`. Consequences: (a) wherever neither channel is at 255, part of the untinted (near-white) base texture leaks through, so a soft red↔green crossfade (e.g. `(128,128,0)`) shows a light "halo" even with dark colors; (b) yellow `(255,255,0)` multiplies the two colors together, so accent areas must be **pure green** (red = 0) to show the picker color at full vibrancy. Paint crossovers as **red → yellow → green** (red holds 255 until the midpoint then fades; green ramps to 255 by the midpoint), so `max(r,g)` stays 255. A per-pixel **dither** between pure red and pure green also avoids the halo up close; mod textures load with trilinear filtering + mipmaps (`ModContentLoader`), so the GPU averages the dither back toward `(128,128)` only at zoomed-out mip levels, where it reads as a soft blend. On 2026-10-12 the tail and ear masks were converted to a Floyd–Steinberg red/green dither (requested after trying the red→yellow→green version); the PSDs in `src/psd/` were not. **Accent-tip tail masks** (`*_Tail_Tip`) are generated by `src/psd/gen_tail_masks_dither.py <TexName> <MaskName> [tip_frac] [feather_frac] [--band]` (run from the repo root; needs numpy, Pillow, scipy; imports `gen_tail_mask.py`): silhouette eroded 5px, green for the first `tip_frac` (default 0.2) of the geodesic tail length (measured on south), fading to red over `feather_frac` (0.08), then dithered. The bob tail used `0.35 0.15 --band`.

Tail genes in `GeneDef_Cheech_Tail.xml`: each texture gets an abstract outline def (texPath, `drawData`, `bodyTypeOffsets`) plus concrete mask variants that only add a `PatternMask` li. `Cheechy_Tail` and `Long_Tail` are drawn in the same canvas frame (base end at top), so their offsets differ only by the small base-end shift. The bob tail has no stem to hide behind the body, so its offsets were fitted to each body type's rear edge. Black/dark mask areas are intentional "untinted" zones (e.g. inner ears, so the texture's pink/grey fill shows) — keep their coverage when reworking masks; only red↔green crossovers need fixing.
