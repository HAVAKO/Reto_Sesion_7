#!/usr/bin/env python3
"""
Caja de Mezuzá paramétrica — generador de STL (espejo del FeatureScript de Onshape).

Mismos parámetros y misma geometría que `onshape/MezuzaCase.fs`, para poder
imprimir directamente o verificar el diseño sin abrir Onshape.

Uso:
    pip install manifold3d trimesh numpy
    python mezuza.py                      # valores por defecto
    python mezuza.py --length 150 --shin-mode relieve --out ../stl
"""
import argparse
import math
import os

import numpy as np
from manifold3d import CrossSection, JoinType, Manifold
import trimesh

# ---------------------------------------------------------------------------
# Letra Shin (ש) — polígonos en caja unitaria (x 0..1, y 0..1).
# Idéntica a SHIN_POLYS en el FeatureScript.
# ---------------------------------------------------------------------------
SHIN_POLYS = [
    [(0.22, 0.0), (1.0, 0.0), (1.0, 0.15), (0.30, 0.15)],                       # base
    [(0.87, 0.0), (1.0, 0.0), (1.0, 0.84), (0.87, 0.84)],                       # brazo derecho
    [(0.78, 0.80), (1.0, 0.84), (1.0, 1.0), (0.78, 0.98)],                      # cabeza derecha
    [(0.66, 0.10), (0.82, 0.10), (0.58, 0.84), (0.44, 0.84)],                   # brazo central
    [(0.38, 0.80), (0.60, 0.84), (0.60, 1.0), (0.38, 0.98)],                    # cabeza central
    [(0.22, 0.0), (0.36, 0.15), (0.26, 0.24), (0.19, 0.42),
     (0.16, 0.84), (0.02, 0.84), (0.04, 0.40), (0.10, 0.16)],                   # brazo izquierdo
    [(-0.04, 0.80), (0.18, 0.84), (0.18, 1.0), (-0.04, 0.98)],                  # cabeza izquierda
]
SHIN_X0, SHIN_X1 = -0.04, 1.0  # extensión horizontal para centrar


def ccw(poly):
    a = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % len(poly)]
        a += x0 * y1 - x1 * y0
    return poly if a > 0 else poly[::-1]


def core_polygon(p):
    """Polígono núcleo convexo; el contorno exterior = núcleo ⊕ disco(r)."""
    r = p.corner_radius
    hb = p.width_bottom / 2 - r
    ht = p.width_top / 2 - r
    y0 = r
    yt = p.length - p.peak - r
    pts = [(-hb, y0), (hb, y0), (ht, yt)]
    if p.peak > 0.01:
        pts.append((0.0, p.length - r))
    pts.append((-ht, yt))
    return pts


def offset_convex(poly, d):
    """Desplaza un polígono convexo CCW: d>0 hacia fuera, d<0 hacia dentro (esquinas vivas)."""
    n = len(poly)
    lines = []
    for i in range(n):
        (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % n]
        dx, dy = x1 - x0, y1 - y0
        L = math.hypot(dx, dy)
        nx, ny = dy / L, -dx / L  # normal exterior (CCW)
        lines.append(((x0 + nx * d, y0 + ny * d), (dx, dy)))
    out = []
    for i in range(n):
        (p1, d1), (p2, d2) = lines[i - 1], lines[i]
        den = d1[0] * d2[1] - d1[1] * d2[0]
        t = ((p2[0] - p1[0]) * d2[1] - (p2[1] - p1[1]) * d2[0]) / den
        out.append((p1[0] + d1[0] * t, p1[1] + d1[1] * t))
    return out


def outline(p, inset, min_r=0.4):
    """Contorno redondeado desplazado hacia dentro `inset` mm (puede ser negativo)."""
    rho = max(p.corner_radius - inset, min_r)
    shrink = (p.corner_radius - inset) - rho  # <= 0
    core = offset_convex(core_polygon(p), shrink) if shrink < 0 else core_polygon(p)
    return CrossSection([core]).offset(rho, JoinType.Round, circular_segments=96)


def prism(cs, z0, z1):
    return Manifold.extrude(cs, z1 - z0).translate((0, 0, z0))


def shin_section(p):
    h = p.shin_height
    w = h * (SHIN_X1 - SHIN_X0)
    cx = 0.0
    cy = p.length - p.shin_from_top
    polys = []
    for poly in SHIN_POLYS:
        pts = [((x - SHIN_X0) * h - w / 2 + cx, (y - 0.5) * h + cy) for x, y in poly]
        polys.append(ccw(pts))
    cs = CrossSection()
    for q in polys:
        cs = cs + CrossSection([q])
    return cs


def build_case(p):
    D = p.depth
    f = min(p.front_fillet, D - p.wall - 0.5)
    # Cuerpo exterior con borde frontal redondeado (convexo → hull de capas)
    layers = [prism(outline(p, 0), 0, D - f)]
    n = 12
    for i in range(1, n + 1):
        th = (math.pi / 2) * i / n
        z = D - f + f * math.sin(th)
        ins = f * (1 - math.cos(th))
        layers.append(prism(outline(p, ins), z - 0.01, z))
    body = Manifold.batch_hull(layers)

    # Cavidad (abierta por detrás)
    body -= prism(outline(p, p.wall), -1, D - p.front_thickness)

    top = D
    # Ranura de marco en la cara frontal
    if p.frame_groove:
        ring = outline(p, f + p.frame_inset) - outline(p, f + p.frame_inset + p.groove_width)
        body -= prism(ring, top - p.groove_depth, top + 1)

    # Decoración: líneas horizontales inferiores + línea vertical central
    if p.decor:
        inner = outline(p, f + p.frame_inset + p.groove_width + 2.0)
        y = f + p.frame_inset + p.groove_width + 6.0
        deco = CrossSection()
        ys = []
        for i in range(p.decor_lines):
            yy = y + i * p.decor_spacing
            ys.append(yy)
            deco = deco + CrossSection.square((200, p.groove_width)).translate((-100, yy))
        shin_bottom = p.length - p.shin_from_top - p.shin_height / 2
        v0 = (ys[-1] if ys else y) + p.decor_spacing + 1.0
        v1 = shin_bottom - 6.0
        if v1 - v0 > 3:
            deco = deco + CrossSection.square((p.groove_width, v1 - v0)).translate((-p.groove_width / 2, v0))
            # pequeño rombo como remate bajo la Shin
            s = 1.6
            deco = deco + CrossSection([[(0, v1 + 0.6), (s, v1 + 0.6 + s), (0, v1 + 0.6 + 2 * s), (-s, v1 + 0.6 + s)]])
        deco = deco ^ inner
        body -= prism(deco, top - p.groove_depth, top + 1)

    # Shin
    shin = shin_section(p)
    if p.shin_mode == "grabado":
        body -= prism(shin, top - p.shin_depth, top + 1)
    else:
        body += prism(shin, top - 0.2, top + p.shin_depth)
    return body


def build_back(p):
    """Tapa trasera / placa de montaje: entra a presión en la cavidad."""
    t = p.lid_thickness
    plate = prism(outline(p, p.wall + p.clearance), 0, t)
    # Muesca para palanca (abrir la caja con un destornillador)
    # Agujeros avellanados para tornillos (arriba y abajo)
    ys = [p.wall + 14.0, p.length - p.peak - p.wall - 14.0]
    for yy in ys:
        hole = Manifold.cylinder(t + 2, p.screw_d / 2, p.screw_d / 2, 48).translate((0, yy, -1))
        cs_h = (p.screw_head_d - p.screw_d) / 2
        cone = Manifold.cylinder(cs_h + 0.01, p.screw_d / 2, p.screw_head_d / 2, 48).translate((0, yy, t - cs_h))
        plate = plate - hole - cone
    return plate


def to_trimesh(m):
    mesh = m.to_mesh()
    return trimesh.Trimesh(np.asarray(mesh.vert_properties)[:, :3], np.asarray(mesh.tri_verts), process=True)


def parse():
    a = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = a.add_argument
    g("--length", type=float, default=125.0, help="Altura total (mm)")
    g("--width-bottom", type=float, default=30.0, help="Ancho inferior (mm)")
    g("--width-top", type=float, default=34.0, help="Ancho en hombros superiores (mm)")
    g("--peak", type=float, default=9.0, help="Altura del pico superior (0 = recto)")
    g("--depth", type=float, default=22.0, help="Profundidad total (mm)")
    g("--corner-radius", type=float, default=6.0)
    g("--wall", type=float, default=2.2, help="Espesor de paredes laterales")
    g("--front-thickness", type=float, default=2.8, help="Espesor de la cara frontal")
    g("--front-fillet", type=float, default=3.0, help="Redondeo del borde frontal")
    g("--shin-height", type=float, default=17.0)
    g("--shin-from-top", type=float, default=30.0, help="Distancia del centro de la Shin al extremo superior")
    g("--shin-depth", type=float, default=1.4, help="Profundidad del grabado / altura del relieve")
    g("--shin-mode", choices=["grabado", "relieve"], default="grabado")
    g("--frame-groove", type=int, default=1)
    g("--frame-inset", type=float, default=2.5)
    g("--groove-width", type=float, default=1.0)
    g("--groove-depth", type=float, default=0.8)
    g("--decor", type=int, default=1)
    g("--decor-lines", type=int, default=3)
    g("--decor-spacing", type=float, default=2.6)
    g("--lid-thickness", type=float, default=3.0)
    g("--clearance", type=float, default=0.2, help="Holgura tapa/caja")
    g("--screw-d", type=float, default=3.5)
    g("--screw-head-d", type=float, default=7.0)
    g("--out", default=os.path.join(os.path.dirname(__file__), "..", "stl"))
    return a.parse_args()


if __name__ == "__main__":
    p = parse()
    assert p.front_thickness > p.shin_depth + 0.6 or p.shin_mode == "relieve", "grabado demasiado profundo"
    assert p.corner_radius > p.wall, "corner-radius debe ser mayor que wall"
    os.makedirs(p.out, exist_ok=True)
    case, back = build_case(p), build_back(p)
    for name, m in (("mezuza_caja", case), ("mezuza_tapa", back)):
        tm = to_trimesh(m)
        path = os.path.join(p.out, f"{name}.stl")
        tm.export(path)
        print(f"{path}: {len(tm.faces)} caras, estanco={tm.is_watertight}, vol={tm.volume/1000:.1f} cm³, "
              f"bbox={np.round(tm.extents, 1).tolist()} mm")
