"""Write final dithered CutoutComplex masks for a Cheech tail into Textures/Cheech/Tail/m/.

Builds on gen_tail_mask.py (run from the repo root):
  - fill    = tail silhouette eroded 5px (mid-outline), so no tint bleeds onto the outline
  - weight  = linear ramp along the tail's geodesic distance from the tip: 1 (accent, green)
              for the first tip_frac of the tail length, fading to 0 (fill, red) over feather_frac
  - output  = Floyd-Steinberg dither of that weight into pure red (255,0,0) / pure green (0,255,0),
              black outside the fill (see CLAUDE.md "Two-color masks" for why it's dithered)

The tail length is measured on the south view and reused for north/east, so the tip is the same
size in every facing. The tip end is the endpoint lowest on the canvas (south/north) or leftmost
(east), i.e. the end farthest from the body.

--band seeds the tip distance from the whole far end (pixels within 10% of the far end, measured
from the base) instead of a single endpoint pixel, giving a straight crossover on short, blobby
tails (e.g. the bob tail) where a single-pixel seed makes a lopsided diagonal ramp.

Usage: python src/psd/gen_tail_masks_dither.py <TexName> <MaskName> [tip_frac] [feather_frac] [--band] [--preview DIR]
  e.g. python src/psd/gen_tail_masks_dither.py Long_Tail Long_Tail_Tip
"""
import os
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from gen_tail_mask import TAIL_DIR, load_silhouette, erode_to_mid_outline, geodesic_bfs, find_endpoints, preview

ERODE_PX = 5

def tip_and_base(fill, direction):
    A, B, _ = find_endpoints(fill)
    if direction == "east":
        return (A, B) if A[1] < B[1] else (B, A)
    return (A, B) if A[0] > B[0] else (B, A)

def floyd_steinberg(weight, region):
    """Dither weight (0..1) to {0,1} inside region; error only diffuses within region."""
    w = weight.astype(float).copy()
    out = np.zeros(w.shape, bool)
    ys, xs = np.where(region)
    h, wd = w.shape
    for y in range(ys.min(), ys.max() + 1):
        for x in range(xs.min(), xs.max() + 1):
            if not region[y, x]:
                continue
            old = w[y, x]
            new = 1.0 if old >= 0.5 else 0.0
            out[y, x] = new == 1.0
            err = old - new
            for dy, dx, k in ((0, 1, 7/16), (1, -1, 3/16), (1, 0, 5/16), (1, 1, 1/16)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < wd and region[ny, nx]:
                    w[ny, nx] += err * k
    return out

def build(tex, direction, tip_len, feather, band):
    a, alpha, lum, sil = load_silhouette(f"{TAIL_DIR}/{tex}_{direction}.png")
    fill = erode_to_mid_outline(sil, ERODE_PX)
    tip, base = tip_and_base(fill, direction)
    if band:
        from_base = geodesic_bfs(fill, base)
        far = np.where(np.isfinite(from_base), from_base, -1)
        seeds = fill & (far >= far.max() * 0.9)
        dist = np.full(fill.shape, np.inf)
        for s in zip(*np.where(seeds)):
            dist = np.minimum(dist, geodesic_bfs(fill, s))
    else:
        dist = geodesic_bfs(fill, tip)
    w = np.clip((tip_len - dist) / max(feather, 1.0), 0.0, 1.0)
    w[~np.isfinite(dist)] = 0.0
    green = floyd_steinberg(w, fill)
    out = np.zeros((*fill.shape, 3), np.uint8)
    out[fill & ~green, 0] = 255
    out[fill & green, 1] = 255
    return out, alpha, lum, tip, base

def south_length(tex):
    _, _, _, sil = load_silhouette(f"{TAIL_DIR}/{tex}_south.png")
    fill = erode_to_mid_outline(sil, ERODE_PX)
    _, B, dA = find_endpoints(fill)
    return dA[B]

if __name__ == "__main__":
    args = [s for s in sys.argv[1:]]
    band = "--band" in args
    preview_dir = None
    if "--preview" in args:
        i = args.index("--preview"); preview_dir = args[i + 1]; del args[i:i + 2]
    args = [s for s in args if s != "--band"]
    tex, mask = args[0], args[1]
    tip_frac = float(args[2]) if len(args) > 2 else 0.20
    feather_frac = float(args[3]) if len(args) > 3 else 0.08
    total = south_length(tex)
    for direction in ("south", "north", "east"):
        out, alpha, lum, tip, base = build(tex, direction, total * tip_frac, total * feather_frac, band)
        path = f"{TAIL_DIR}/m/{mask}_{direction}.png"
        Image.fromarray(out, "RGB").save(path, optimize=True)
        print(f"{path}: tip {tip} base {base} length(south) {total:.0f}")
        if preview_dir:
            rgba = np.zeros((*out.shape[:2], 4), np.uint8); rgba[..., :3] = out; rgba[..., 3] = 255
            preview(alpha, lum, rgba, np.array([0.85, 0.45, 0.18]), np.array([0.97, 0.93, 0.85])).save(f"{preview_dir}/{mask}_{direction}.png")
