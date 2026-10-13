"""Generate two-color CutoutComplex masks for Cheech tail textures.

Mask convention (RimWorld CutoutComplex):
  red   channel -> colorOne  (theme tailFill)
  green channel -> colorTwo  (theme tailAccent)
  black         -> no tint
The base texture supplies shading/outline + silhouette (via alpha); the mask
is a multiply, so the dark outline stays dark regardless and greyscale shading
is retained. We fill the silhouette eroded to ~mid-outline so no red rim bleeds
onto the anti-aliased outer edge.
"""
import sys
from collections import deque
import numpy as np
from PIL import Image
from scipy import ndimage

TAIL_DIR = "Textures/Cheech/Tail"

def load_silhouette(path):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    alpha = a[:, :, 3]
    lum = a[:, :, :3].astype(float).mean(axis=2)  # greyscale shading
    sil = alpha > 16
    return a, alpha, lum, sil

def erode_to_mid_outline(sil, px):
    return ndimage.binary_erosion(sil, iterations=px)

def geodesic_bfs(mask, start):
    """8-connected BFS step-distance over True pixels. Returns dist array (np.inf outside)."""
    h, w = mask.shape
    dist = np.full((h, w), np.inf)
    sy, sx = start
    dist[sy, sx] = 0
    q = deque([(sy, sx)])
    nbrs = [(-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)]
    while q:
        y, x = q.popleft()
        d = dist[y, x] + 1
        for dy, dx in nbrs:
            ny, nx = y+dy, x+dx
            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and d < dist[ny, nx]:
                dist[ny, nx] = d
                q.append((ny, nx))
    return dist

def find_endpoints(mask):
    ys, xs = np.where(mask)
    seed = (int(ys.mean()), int(xs.mean()))
    # nearest opaque pixel to centroid as valid seed
    if not mask[seed]:
        i = np.argmin((ys-seed[0])**2 + (xs-seed[1])**2)
        seed = (ys[i], xs[i])
    d0 = geodesic_bfs(mask, seed)
    a = np.unravel_index(np.argmax(np.where(np.isfinite(d0), d0, -1)), d0.shape)
    dA = geodesic_bfs(mask, a)
    b = np.unravel_index(np.argmax(np.where(np.isfinite(dA), dA, -1)), dA.shape)
    return a, b, dA  # dA = geodesic dist from end A, total length = dA[b]

def build_mask(fill_mask, tip_end, tip_frac=0.20, feather_frac=0.08):
    dist = geodesic_bfs(fill_mask, tip_end)
    finite = dist[np.isfinite(dist)]
    total = finite.max()
    tip_len = total * tip_frac
    feather = total * feather_frac
    w = np.clip((tip_len - dist) / max(feather, 1.0), 0.0, 1.0)
    w[~np.isfinite(dist)] = 0.0
    green = (w * 255).astype(float)
    red = ((1.0 - w) * 255).astype(float)
    red[~fill_mask] = 0
    green[~fill_mask] = 0
    # soft blur to match existing hand-painted masks
    red = ndimage.gaussian_filter(red, 2.0)
    green = ndimage.gaussian_filter(green, 2.0)
    red[~fill_mask] = 0
    green[~fill_mask] = 0
    out = np.zeros((*fill_mask.shape, 4), np.uint8)
    out[:, :, 0] = red.clip(0, 255)
    out[:, :, 1] = green.clip(0, 255)
    out[:, :, 3] = 255  # opaque mask; sampling uses base alpha
    return out

def preview(base_alpha, lum, mask, fill_rgb, accent_rgb):
    """Emulate CutoutComplex multiply: out = lum * lerp(fill, accent, green/(red+green))."""
    r = mask[:, :, 0].astype(float)
    g = mask[:, :, 1].astype(float)
    denom = np.maximum(r + g, 1e-6)
    t = g / denom  # 0 -> fill, 1 -> accent
    tint = (np.stack([fill_rgb]*1, 0).reshape(1,1,3) * (1-t)[..., None]
            + np.stack([accent_rgb]*1, 0).reshape(1,1,3) * t[..., None])
    shade = (lum / 255.0)[..., None]
    rgb = (shade * tint * 255).clip(0, 255)
    out = np.zeros((*lum.shape, 4), np.uint8)
    out[:, :, :3] = rgb.astype(np.uint8)
    out[:, :, 3] = base_alpha
    # white background flatten for easy viewing
    bg = np.full((*lum.shape, 3), 255, np.uint8)
    af = (base_alpha/255.0)[..., None]
    flat = (rgb*af + bg*(1-af)).clip(0,255).astype(np.uint8)
    return Image.fromarray(flat, "RGB")

if __name__ == "__main__":
    direction = sys.argv[1] if len(sys.argv) > 1 else "south"
    erode_px = int(sys.argv[2]) if len(sys.argv) > 2 else 5
    base = f"{TAIL_DIR}/Cheechy_Tail_{direction}.png"
    a, alpha, lum, sil = load_silhouette(base)
    fill = erode_to_mid_outline(sil, erode_px)
    A, B, _ = find_endpoints(fill)
    print(direction, "endpoint A", A, "endpoint B", B)
    fill_rgb = np.array([0.85, 0.45, 0.18])   # sample orange tailFill
    accent_rgb = np.array([0.97, 0.93, 0.85]) # sample cream tailAccent
    for tag, end in [("A", A), ("B", B)]:
        m = build_mask(fill, end)
        prev = preview(alpha, lum, m, fill_rgb, accent_rgb)
        prev.save(f"{TAIL_DIR}/_preview_{direction}_tip{tag}.png")
        print("  wrote preview tip", tag, "at", end)
