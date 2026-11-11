FeatureScript 2399;
import(path : "onshape/std/geometry.fs", version : "2399.0");

/*
 * ===========================================================================
 *  CAJA DE MEZUZÁ PARAMÉTRICA — Onshape Custom Feature
 * ===========================================================================
 *  Crea dos piezas listas para imprimir en 3D:
 *    1. "Mezuzá - Caja":  cuerpo hueco con hombros afinados y punta superior,
 *       borde frontal redondeado, ranura de marco, línea central con rombo,
 *       líneas inferiores y la letra Shin (ש) grabada (metida) o en relieve.
 *    2. "Mezuzá - Tapa":  placa trasera que se atornilla al marco de la puerta
 *       y sobre la que la caja entra a presión.
 *
 *  Geometría:
 *    El contorno es  núcleo ⊕ disco(r)  (polígono convexo redondeado), por lo
 *    que cualquier desplazamiento hacia dentro (paredes, ranuras, tapa) es
 *    exacto y siempre concéntrico al contorno exterior.
 *
 *  Sistema de coordenadas: plano XY = cara trasera (z = 0), +Z = frente,
 *  +Y = arriba. La tapa se coloca a la derecha de la caja.
 * ===========================================================================
 */

export enum ShinMode
{
    annotation { "Name" : "Grabado (metido)" }
    GRABADO,
    annotation { "Name" : "Relieve (saliente)" }
    RELIEVE
}

const LENGTH_BOUNDS = { (millimeter) : [50, 125, 300] } as LengthBoundSpec;
const WIDTH_B_BOUNDS = { (millimeter) : [15, 30, 80] } as LengthBoundSpec;
const WIDTH_T_BOUNDS = { (millimeter) : [15, 34, 80] } as LengthBoundSpec;
const PEAK_BOUNDS = { (millimeter) : [0, 9, 40] } as LengthBoundSpec;
const DEPTH_BOUNDS = { (millimeter) : [10, 22, 60] } as LengthBoundSpec;
const RADIUS_BOUNDS = { (millimeter) : [1, 6, 20] } as LengthBoundSpec;
const WALL_BOUNDS = { (millimeter) : [0.8, 2.2, 6] } as LengthBoundSpec;
const FRONT_BOUNDS = { (millimeter) : [1, 2.8, 8] } as LengthBoundSpec;
const FILLET_BOUNDS = { (millimeter) : [0, 3, 10] } as LengthBoundSpec;
const SHIN_H_BOUNDS = { (millimeter) : [5, 17, 60] } as LengthBoundSpec;
const SHIN_TOP_BOUNDS = { (millimeter) : [5, 30, 150] } as LengthBoundSpec;
const SHIN_D_BOUNDS = { (millimeter) : [0.2, 1.4, 5] } as LengthBoundSpec;
const SMALL_BOUNDS = { (millimeter) : [0, 2.5, 10] } as LengthBoundSpec;
const GROOVE_W_BOUNDS = { (millimeter) : [0.4, 1, 4] } as LengthBoundSpec;
const GROOVE_D_BOUNDS = { (millimeter) : [0.2, 0.8, 3] } as LengthBoundSpec;
const SPACING_BOUNDS = { (millimeter) : [1, 2.6, 10] } as LengthBoundSpec;
const LID_T_BOUNDS = { (millimeter) : [1.5, 3, 8] } as LengthBoundSpec;
const CLEAR_BOUNDS = { (millimeter) : [0, 0.2, 1] } as LengthBoundSpec;
const SCREW_BOUNDS = { (millimeter) : [2, 3.5, 6] } as LengthBoundSpec;
const HEAD_BOUNDS = { (millimeter) : [3, 7, 12] } as LengthBoundSpec;
const LINES_BOUNDS = { (unitless) : [0, 3, 10] } as IntegerBoundSpec;

/** Letra Shin (ש) en una caja unitaria (x -0.04..1, y 0..1). Idéntica al script Python. */
const SHIN_POLYS = [
        [[0.22, 0.0], [1.0, 0.0], [1.0, 0.15], [0.30, 0.15]],                     // base
        [[0.87, 0.0], [1.0, 0.0], [1.0, 0.84], [0.87, 0.84]],                     // brazo derecho
        [[0.78, 0.80], [1.0, 0.84], [1.0, 1.0], [0.78, 0.98]],                    // cabeza derecha
        [[0.66, 0.10], [0.82, 0.10], [0.58, 0.84], [0.44, 0.84]],                 // brazo central
        [[0.38, 0.80], [0.60, 0.84], [0.60, 1.0], [0.38, 0.98]],                  // cabeza central
        [[0.22, 0.0], [0.36, 0.15], [0.26, 0.24], [0.19, 0.42],
            [0.16, 0.84], [0.02, 0.84], [0.04, 0.40], [0.10, 0.16]],               // brazo izquierdo
        [[-0.04, 0.80], [0.18, 0.84], [0.18, 1.0], [-0.04, 0.98]]                 // cabeza izquierda
    ];
const SHIN_X0 = -0.04;
const SHIN_X1 = 1.0;

annotation { "Feature Type Name" : "Caja de Mezuzá",
        "Feature Type Description" : "Caja de mezuzá paramétrica con la letra Shin, lista para impresión 3D." }
export const mezuzaCase = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Group Name" : "Forma", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Altura total" }
            isLength(definition.length, LENGTH_BOUNDS);

            annotation { "Name" : "Ancho inferior" }
            isLength(definition.widthBottom, WIDTH_B_BOUNDS);

            annotation { "Name" : "Ancho superior (hombros)" }
            isLength(definition.widthTop, WIDTH_T_BOUNDS);

            annotation { "Name" : "Altura de la punta (0 = recta)" }
            isLength(definition.peak, PEAK_BOUNDS);

            annotation { "Name" : "Profundidad" }
            isLength(definition.depth, DEPTH_BOUNDS);

            annotation { "Name" : "Radio de esquinas" }
            isLength(definition.cornerRadius, RADIUS_BOUNDS);

            annotation { "Name" : "Redondeo del borde frontal" }
            isLength(definition.frontFillet, FILLET_BOUNDS);
        }

        annotation { "Group Name" : "Paredes", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Espesor de pared" }
            isLength(definition.wall, WALL_BOUNDS);

            annotation { "Name" : "Espesor frontal" }
            isLength(definition.frontThickness, FRONT_BOUNDS);
        }

        annotation { "Group Name" : "Shin (ש)", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Estilo", "UIHint" : UIHint.SHOW_LABEL }
            definition.shinMode is ShinMode;

            annotation { "Name" : "Altura de la letra" }
            isLength(definition.shinHeight, SHIN_H_BOUNDS);

            annotation { "Name" : "Centro desde arriba" }
            isLength(definition.shinFromTop, SHIN_TOP_BOUNDS);

            annotation { "Name" : "Profundidad / altura" }
            isLength(definition.shinDepth, SHIN_D_BOUNDS);
        }

        annotation { "Group Name" : "Decoración", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Ranura de marco", "Default" : true }
            definition.frameGroove is boolean;

            annotation { "Name" : "Margen del marco" }
            isLength(definition.frameInset, SMALL_BOUNDS);

            annotation { "Name" : "Ancho de ranura" }
            isLength(definition.grooveWidth, GROOVE_W_BOUNDS);

            annotation { "Name" : "Profundidad de ranura" }
            isLength(definition.grooveDepth, GROOVE_D_BOUNDS);

            annotation { "Name" : "Línea central y líneas inferiores", "Default" : true }
            definition.decor is boolean;

            annotation { "Name" : "Número de líneas inferiores" }
            isInteger(definition.decorLines, LINES_BOUNDS);

            annotation { "Name" : "Separación de líneas" }
            isLength(definition.decorSpacing, SPACING_BOUNDS);
        }

        annotation { "Group Name" : "Tapa / montaje", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Crear tapa trasera", "Default" : true }
            definition.makeLid is boolean;

            annotation { "Name" : "Espesor de tapa" }
            isLength(definition.lidThickness, LID_T_BOUNDS);

            annotation { "Name" : "Holgura (ajuste a presión)" }
            isLength(definition.clearance, CLEAR_BOUNDS);

            annotation { "Name" : "Diámetro tornillo" }
            isLength(definition.screwD, SCREW_BOUNDS);

            annotation { "Name" : "Diámetro cabeza (avellanado)" }
            isLength(definition.screwHeadD, HEAD_BOUNDS);
        }
    }
    {
        validate(definition);

        const D = definition.depth;
        const f = min(definition.frontFillet, D - definition.frontThickness - 0.5 * millimeter);
        const noShift = vector(0, 0) * millimeter;

        // ---------------------------------------------------------------- cuerpo
        const caseQ = prism(context, id + "body", 0 * millimeter, D, function(sk)
            {
                sketchRounded(sk, "o", definition, 0 * millimeter, noShift);
            });

        if (f > 0.05 * millimeter)
        {
            const frontFace = qCoincidesWithPlane(qOwnedByBody(caseQ, EntityType.FACE), plane(vector(0, 0, 1) * D, vector(0, 0, 1)));
            opFillet(context, id + "frontFillet", {
                        "entities" : qAdjacent(frontFace, AdjacencyType.EDGE, EntityType.EDGE),
                        "radius" : f
                    });
        }

        // --------------------------------------------------------------- cavidad
        const cavity = prism(context, id + "cavity", -1 * millimeter, D - definition.frontThickness, function(sk)
            {
                sketchRounded(sk, "c", definition, definition.wall, noShift);
            });
        subtract(context, id + "cutCavity", caseQ, cavity);

        // ---------------------------------------------------------------- marco
        if (definition.frameGroove)
        {
            const a = definition.frameInset + f;
            const outerQ = prism(context, id + "frameOuter", D - definition.grooveDepth, D + millimeter, function(sk)
                {
                    sketchRounded(sk, "fo", definition, a, noShift);
                });
            const innerQ = prism(context, id + "frameInner", D - 2 * definition.grooveDepth, D + 2 * millimeter, function(sk)
                {
                    sketchRounded(sk, "fi", definition, a + definition.grooveWidth, noShift);
                });
            subtract(context, id + "frameRing", outerQ, innerQ);
            subtract(context, id + "cutFrame", caseQ, outerQ);
        }

        // ----------------------------------------------------------- decoración
        if (definition.decor)
        {
            decorate(context, id + "decor", definition, caseQ, f);
        }

        // ----------------------------------------------------------------- Shin
        const h = definition.shinHeight;
        const w = h * (SHIN_X1 - SHIN_X0);
        const cy = definition.length - definition.shinFromTop;
        const drawShin = function(sk)
            {
                for (var i = 0; i < size(SHIN_POLYS); i += 1)
                {
                    var pts = [];
                    for (var p in SHIN_POLYS[i])
                    {
                        pts = append(pts, vector((p[0] - SHIN_X0) * h - w / 2, (p[1] - 0.5) * h + cy));
                    }
                    pts = append(pts, pts[0]);
                    skPolyline(sk, "shin" ~ i, { "points" : pts });
                }
            };
        if (definition.shinMode == ShinMode.GRABADO)
        {
            const shinQ = prism(context, id + "shin", D - definition.shinDepth, D + millimeter, drawShin);
            subtract(context, id + "cutShin", caseQ, shinQ);
        }
        else
        {
            const shinQ = prism(context, id + "shin", D - 0.2 * millimeter, D + definition.shinDepth, drawShin);
            opBoolean(context, id + "addShin", {
                        "tools" : qUnion([caseQ, shinQ]),
                        "operationType" : BooleanOperationType.UNION
                    });
        }

        setProperty(context, { "entities" : caseQ, "propertyType" : PropertyType.NAME, "value" : "Mezuzá - Caja" });
        setProperty(context, { "entities" : caseQ, "propertyType" : PropertyType.APPEARANCE, "value" : color(0.86, 0.72, 0.46) });

        // ----------------------------------------------------------------- tapa
        if (definition.makeLid)
        {
            buildLid(context, id + "lid", definition);
        }
    });

// ===========================================================================
//  Funciones auxiliares
// ===========================================================================

function validate(d is map)
{
    if (d.cornerRadius <= d.wall + d.clearance)
        throw regenError("El radio de esquinas debe ser mayor que pared + holgura.", ["cornerRadius"]);
    if (d.shinMode == ShinMode.GRABADO && d.shinDepth > d.frontThickness - 0.6 * millimeter)
        throw regenError("El grabado de la Shin atravesaría la cara frontal: reduce la profundidad o aumenta el espesor frontal.", ["shinDepth"]);
    if (d.grooveDepth > d.frontThickness - 0.6 * millimeter)
        throw regenError("Las ranuras son demasiado profundas para el espesor frontal.", ["grooveDepth"]);
    if (d.cornerRadius * 2 >= min(d.widthBottom, d.widthTop))
        throw regenError("El radio de esquinas es demasiado grande para el ancho.", ["cornerRadius"]);
    if (d.screwHeadD <= d.screwD)
        throw regenError("La cabeza del tornillo debe ser mayor que su diámetro.", ["screwHeadD"]);
}

/** Polígono núcleo convexo (CCW). El contorno exterior es núcleo ⊕ disco(cornerRadius). */
function corePolygon(d is map) returns array
{
    const r = d.cornerRadius;
    const hb = d.widthBottom / 2 - r;
    const ht = d.widthTop / 2 - r;
    const y0 = r;
    const yt = d.length - d.peak - r;
    var pts = [vector(-hb, y0), vector(hb, y0), vector(ht, yt)];
    if (d.peak > 0.01 * millimeter)
        pts = append(pts, vector(0 * millimeter, d.length - r));
    pts = append(pts, vector(-ht, yt));
    return pts;
}

/** Normal exterior (unitaria) de la arista a→b de un polígono CCW. */
function edgeNormal(a is Vector, b is Vector) returns Vector
{
    const t = (b - a) / norm(b - a);
    return vector(t[1], -t[0]);
}

/** Desplaza un polígono convexo CCW (dist > 0 hacia fuera, < 0 hacia dentro). */
function offsetConvex(poly is array, dist is ValueWithUnits) returns array
{
    const n = size(poly);
    var out = [];
    for (var i = 0; i < n; i += 1)
    {
        const n1 = edgeNormal(poly[(i + n - 1) % n], poly[i]);
        const n2 = edgeNormal(poly[i], poly[(i + 1) % n]);
        out = append(out, poly[i] + dist * (n1 + n2) / (1 + dot(n1, n2)));
    }
    return out;
}

/** Dibuja el contorno redondeado desplazado `inset` hacia dentro (exacto). */
function sketchRounded(sk is Sketch, prefix is string, d is map, inset is ValueWithUnits, shift is Vector)
{
    const minR = 0.4 * millimeter;
    var rho = d.cornerRadius - inset;
    var core = corePolygon(d);
    if (rho < minR)
    {
        core = offsetConvex(core, rho - minR);
        rho = minR;
    }
    const n = size(core);
    for (var i = 0; i < n; i += 1)
    {
        const a = core[(i + n - 1) % n];
        const b = core[i];
        const c = core[(i + 1) % n];
        const n1 = edgeNormal(a, b);
        const n2 = edgeNormal(b, c);
        const m = (n1 + n2) / norm(n1 + n2);
        skArc(sk, prefix ~ "a" ~ i, {
                    "start" : b + n1 * rho + shift,
                    "mid" : b + m * rho + shift,
                    "end" : b + n2 * rho + shift
                });
        skLineSegment(sk, prefix ~ "l" ~ i, {
                    "start" : b + n2 * rho + shift,
                    "end" : c + n2 * rho + shift
                });
    }
}

/** Croquis en el plano z = z0 y extrusión hasta z1. Devuelve los cuerpos creados. */
function prism(context is Context, id is Id, z0 is ValueWithUnits, z1 is ValueWithUnits, drawFn is function) returns Query
{
    const pl = plane(vector(0, 0, 1) * z0, vector(0, 0, 1), vector(1, 0, 0));
    const sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : pl });
    drawFn(sk);
    skSolve(sk);
    opExtrude(context, id + "ex", {
                "entities" : qSketchRegion(id + "sk"),
                "direction" : vector(0, 0, 1),
                "endBound" : BoundingType.BLIND,
                "endDepth" : z1 - z0
            });
    opDeleteBodies(context, id + "del", { "entities" : qCreatedBy(id + "sk", EntityType.BODY) });
    return qCreatedBy(id + "ex", EntityType.BODY);
}

function subtract(context is Context, id is Id, targets is Query, tools is Query)
{
    opBoolean(context, id, {
                "tools" : tools,
                "targets" : targets,
                "operationType" : BooleanOperationType.SUBTRACTION
            });
}

/** Líneas horizontales inferiores + línea vertical central rematada con un rombo. */
function decorate(context is Context, id is Id, d is map, caseQ is Query, f is ValueWithUnits)
{
    const D = d.depth;
    const gw = d.grooveWidth;
    const z0 = D - d.grooveDepth;
    const big = 200 * millimeter;
    const y = f + d.frameInset + gw + 6 * millimeter;

    var lastY = y - d.decorSpacing;
    const linesQ = prism(context, id + "lines", z0, D + millimeter, function(sk)
        {
            for (var i = 0; i < d.decorLines; i += 1)
            {
                const yy = y + i * d.decorSpacing;
                skRectangle(sk, "r" ~ i, { "firstCorner" : vector(-big / 2, yy), "secondCorner" : vector(big / 2, yy + gw) });
            }
        });
    if (d.decorLines > 0)
        lastY = y + (d.decorLines - 1) * d.decorSpacing;

    // Recorta las líneas al interior del marco: máscara = caja grande − interior
    const maskQ = prism(context, id + "mask", z0 - millimeter, D + 2 * millimeter, function(sk)
        {
            skRectangle(sk, "m", { "firstCorner" : vector(-big, -big), "secondCorner" : vector(big, d.length + big) });
        });
    const innerQ = prism(context, id + "inner", z0 - 2 * millimeter, D + 3 * millimeter, function(sk)
        {
            sketchRounded(sk, "i", d, f + d.frameInset + gw + 2 * millimeter, vector(0, 0) * millimeter);
        });
    subtract(context, id + "makeMask", maskQ, innerQ);
    if (d.decorLines > 0)
    {
        subtract(context, id + "trimLines", linesQ, maskQ);
        subtract(context, id + "cutLines", caseQ, linesQ);
    }
    opDeleteBodies(context, id + "delMask", { "entities" : maskQ });

    // Línea central con rombo bajo la Shin
    const shinBottom = d.length - d.shinFromTop - d.shinHeight / 2;
    const v0 = lastY + d.decorSpacing + 1 * millimeter;
    const v1 = shinBottom - 6 * millimeter;
    if (v1 - v0 > 3 * millimeter)
    {
        const s = 1.6 * millimeter;
        const zero = 0 * millimeter;
        const stemQ = prism(context, id + "stem", z0, D + millimeter, function(sk)
            {
                skRectangle(sk, "v", { "firstCorner" : vector(-gw / 2, v0), "secondCorner" : vector(gw / 2, v1) });
                const c = v1 + 0.6 * millimeter;
                skPolyline(sk, "rombo", { "points" : [vector(zero, c), vector(s, c + s), vector(zero, c + 2 * s), vector(-s, c + s), vector(zero, c)] });
            });
        subtract(context, id + "cutStem", caseQ, stemQ);
    }
}

/** Placa trasera de montaje con dos agujeros avellanados. Se coloca a la derecha de la caja. */
function buildLid(context is Context, id is Id, d is map)
{
    const t = d.lidThickness;
    const dx = max(d.widthBottom, d.widthTop) + 10 * millimeter;
    const shift = vector(dx, 0 * millimeter);
    const lidQ = prism(context, id + "plate", 0 * millimeter, t, function(sk)
        {
            sketchRounded(sk, "t", d, d.wall + d.clearance, shift);
        });

    const ys = [d.wall + 14 * millimeter, d.length - d.peak - d.wall - 14 * millimeter];
    const sink = (d.screwHeadD - d.screwD) / 2;
    for (var i = 0; i < size(ys); i += 1)
    {
        const c = vector(dx, ys[i], 0 * millimeter);
        fCylinder(context, id + ("hole" ~ i), {
                    "bottomCenter" : c - vector(0, 0, 1) * millimeter,
                    "topCenter" : c + vector(0, 0, 1) * (t + millimeter),
                    "radius" : d.screwD / 2
                });
        fCone(context, id + ("sink" ~ i), {
                    "bottomCenter" : c + vector(0, 0, 1) * (t - sink),
                    "topCenter" : c + vector(0, 0, 1) * (t + 0.01 * millimeter),
                    "bottomRadius" : d.screwD / 2,
                    "topRadius" : d.screwHeadD / 2 + 0.01 * millimeter
                });
        subtract(context, id + ("cut" ~ i), lidQ, qUnion([qCreatedBy(id + ("hole" ~ i), EntityType.BODY),
                        qCreatedBy(id + ("sink" ~ i), EntityType.BODY)]));
    }
    setProperty(context, { "entities" : lidQ, "propertyType" : PropertyType.NAME, "value" : "Mezuzá - Tapa" });
    setProperty(context, { "entities" : lidQ, "propertyType" : PropertyType.APPEARANCE, "value" : color(0.55, 0.45, 0.30) });
}
