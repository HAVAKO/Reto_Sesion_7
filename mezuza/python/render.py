"""Vista previa PNG de los STL con un rasterizador z-buffer simple (sin GPU)."""
import sys, os, math, numpy as np, trimesh
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt

def rot(elev, azim):
    a, e = math.radians(azim), math.radians(elev)
    Ry = np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]])
    Rx = np.array([[1, 0, 0], [0, math.cos(e), -math.sin(e)], [0, math.sin(e), math.cos(e)]])
    return Rx @ Ry

def render(tm, elev, azim, W=420, H=640, color=np.array([0.86, 0.72, 0.46])):
    v = (tm.vertices - tm.bounds.mean(0)) @ rot(elev, azim).T
    s = 0.9 * min(W / np.ptp(v[:, 0]), H / np.ptp(v[:, 1]))
    P = np.c_[v[:, 0] * s + W / 2, -v[:, 1] * s + H / 2, v[:, 2]]
    n = tm.face_normals @ rot(elev, azim).T
    L = np.array([-0.4, 0.5, 0.8]); L /= np.linalg.norm(L)
    img = np.ones((H, W, 3)); zb = np.full((H, W), -np.inf)
    for fi, f in enumerate(tm.faces):
        if n[fi, 2] <= 0: continue
        a, b, c = P[f]
        x0, x1 = int(max(min(a[0], b[0], c[0]), 0)), int(min(max(a[0], b[0], c[0]) + 1, W))
        y0, y1 = int(max(min(a[1], b[1], c[1]), 0)), int(min(max(a[1], b[1], c[1]) + 1, H))
        if x1 <= x0 or y1 <= y0: continue
        X, Y = np.meshgrid(np.arange(x0, x1) + .5, np.arange(y0, y1) + .5)
        d = (b[1]-c[1])*(a[0]-c[0]) + (c[0]-b[0])*(a[1]-c[1])
        if abs(d) < 1e-12: continue
        w0 = ((b[1]-c[1])*(X-c[0]) + (c[0]-b[0])*(Y-c[1])) / d
        w1 = ((c[1]-a[1])*(X-c[0]) + (a[0]-c[0])*(Y-c[1])) / d
        w2 = 1 - w0 - w1
        m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        z = w0*a[2] + w1*b[2] + w2*c[2]
        sub = zb[y0:y1, x0:x1]; m &= z > sub
        sub[m] = z[m]
        diff = max(n[fi] @ L, 0); h = L + np.array([0, 0, 1]); h /= np.linalg.norm(h)
        spec = max(n[fi] @ h, 0) ** 40
        col = np.clip(color * (0.30 + 0.70 * diff) + 0.35 * spec, 0, 1)
        img[y0:y1, x0:x1][m] = col
    from scipy.ndimage import maximum_filter
    zf = np.where(np.isfinite(zb), zb, -1e9)
    occ = np.clip((maximum_filter(zf, size=9) - zf) / (0.8 * s), 0, 1)
    img *= (1 - 0.6 * occ)[..., None]
    img[~np.isfinite(zb)] = 1
    return img

d, out = sys.argv[1], sys.argv[2]
caja = trimesh.load(os.path.join(d, "mezuza_caja.stl"))
tapa = trimesh.load(os.path.join(d, "mezuza_tapa.stl"))
views = [(caja, 6, -10, "Frente"), (caja, 15, -35, "3/4"), (caja, 20, 150, "Trasera (hueca)"), (tapa, 20, -20, "Tapa / placa")]
fig, axs = plt.subplots(1, 4, figsize=(14, 6), facecolor="white")
for ax, (m, e, a, t) in zip(axs, views):
    ax.imshow(render(m, e, a)); ax.set_title(t); ax.axis("off")
plt.tight_layout(); fig.savefig(out, dpi=100)
