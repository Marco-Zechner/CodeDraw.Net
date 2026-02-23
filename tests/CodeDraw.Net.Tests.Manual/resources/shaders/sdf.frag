#version 450 core

in vec2 vUv;
in vec2 vWorldPx;

out vec4 oColor;

// -----------------------------------------------------------------------------
// Legacy per-draw uniforms
uniform vec4  uFillColor;
uniform vec4  uStrokeColor;
uniform float uStrokeThickness;
uniform float uFeatherPx;
uniform int   uHasFill;
uniform int   uHasStroke;
// -----------------------------------------------------------------------------

// --- Primitive types ---
const int SDF_CIRCLE      = 1;
const int SDF_RECT        = 2;
const int SDF_ROUNDEDRECT = 3;
const int SDF_SEGMENT     = 4;
const int SDF_TRIANGLE    = 5;
const int SDF_ELLIPSE     = 6;

// --- Ops ---
const int OP_UNION        = 1;
const int OP_INTERSECT    = 2;
const int OP_SUBTRACT     = 3;
const int OP_SMOOTH_UNION = 4;
const int OP_SMOOTH_INTER = 5;
const int OP_SMOOTH_SUB   = 6;

// --- Rule modes ---
const int RULE_DISABLED   = 0;
const int RULE_SD_LT      = 1;
const int RULE_SD_GT      = 2;
const int RULE_RANGE      = 3;
const int RULE_NEAR_VALUE = 4;

// -----------------------------------------------------------------------------
// SSBOs
// -----------------------------------------------------------------------------

struct Prim
{
    int type;
    int op;
    int matId;   // per-primitive material id
    int _pad1;

    mat4 worldToLocal;

    vec4 p0;
    vec4 p1;

    float k;
    float _pad2, _pad3, _pad4; // align to 16 bytes
};

layout(std430, binding=0) readonly buffer PrimBuffer
{
    int primCount;
    int _padA, _padB, _padC; // align to 16 bytes
    Prim prims[];
};

struct Material
{
    // Base paint (straight alpha)
    vec4  fillColor;       // rgba
    vec4  strokeColor;     // rgba
    float strokeThickness; // px
    float featherPx;       // px
    int   hasFill;         // 0/1
    int   hasStroke;       // 0/1

    int   ruleFirst;       // index into rules[]
    int   ruleCount;
    int   _pad0, _pad1;    // keep 16-byte alignment
};

layout(std430, binding=1) readonly buffer MaterialBuffer
{
    int materialCount;
    int _mPadA, _mPadB, _mPadC;
    Material materials[];
};

struct ColorRule
{
    int mode;       // 0 disabled, 1 sd<x, 2 sd>x, 3 range, 4 near value
    int _pad0;
    int _pad1;
    int _pad2;

    vec4 color;     // rgba (straight alpha)
    float a;        // threshold A (X or min or value)
    float b;        // threshold B (max or tol)
    float feather;  // transition width
    float _pad3;
};

layout(std430, binding=2) readonly buffer RuleBuffer
{
    int ruleCountTotal;
    int _rPadA, _rPadB, _rPadC;
    ColorRule rules[];
};

// -----------------------------------------------------------------------------
// Helpers
// -----------------------------------------------------------------------------

float clamp01(float x) { return clamp(x, 0.0, 1.0); }

float smoothMin(float a, float b, float k)
{
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return mix(b, a, h) - k * h * (1.0 - h);
}
float smoothMax(float a, float b, float k) { return -smoothMin(-a, -b, k); }

float fillAlpha(float sd, float feather)
{
    if (feather <= 0.0) return (sd < 0.0) ? 1.0 : 0.0;
    float t = (sd + feather) / (2.0 * feather);
    t = clamp01(t);
    return 1.0 - t;
}

float strokeAlpha(float sd, float halfT, float feather)
{
    float band = abs(sd) - halfT;
    return fillAlpha(band, feather);
}

// --- primitive SDFs (local space) ---

float sdfCircle(vec2 p, vec2 c, float r)
{
    return length(p - c) - r;
}

float sdfBox(vec2 p, vec2 c, vec2 eHalf)
{
    vec2 d = abs(p - c) - eHalf;
    float outside = length(max(d, vec2(0.0)));
    float inside = min(max(d.x, d.y), 0.0);
    return outside + inside;
}

float sdfRectMinMax(vec2 p, vec4 minMax)
{
    vec2 c = 0.5 * (minMax.xy + minMax.zw);
    vec2 e = 0.5 * (minMax.zw - minMax.xy);
    return sdfBox(p, c, e);
}

float sdfRoundedRectMinMax(vec2 p, vec4 minMax, float radius)
{
    vec2 c = 0.5 * (minMax.xy + minMax.zw);
    vec2 e = 0.5 * (minMax.zw - minMax.xy);
    float rad = clamp(radius, 0.0, min(e.x, e.y));
    vec2 q = abs(p - c) - (e - vec2(rad));
    float outside = length(max(q, vec2(0.0)));
    float inside = min(max(q.x, q.y), 0.0);
    return outside + inside - rad;
}

float distToSeg(vec2 p, vec2 a, vec2 b)
{
    vec2 ab = b - a;
    float denom = dot(ab, ab);
    if (denom <= 1e-12) return length(p - a);
    float t = clamp(dot(p - a, ab) / denom, 0.0, 1.0);
    vec2 q = a + ab * t;
    return length(p - q);
}

float sdfSegment(vec2 p, vec2 a, vec2 b, float radius)
{
    return distToSeg(p, a, b) - max(radius, 0.0);
}

float cross2(vec2 u, vec2 v) { return u.x * v.y - u.y * v.x; }

bool pointInTri(vec2 p, vec2 a, vec2 b, vec2 c)
{
    vec2 ab = b - a; vec2 ap = p - a;
    vec2 bc = c - b; vec2 bp = p - b;
    vec2 ca = a - c; vec2 cp = p - c;

    float c1 = cross2(ab, ap);
    float c2 = cross2(bc, bp);
    float c3 = cross2(ca, cp);

    bool hasNeg = (c1 < 0.0) || (c2 < 0.0) || (c3 < 0.0);
    bool hasPos = (c1 > 0.0) || (c2 > 0.0) || (c3 > 0.0);
    return !(hasNeg && hasPos);
}

float sdfTriangle(vec2 p, vec2 a, vec2 b, vec2 c)
{
    float d0 = distToSeg(p, a, b);
    float d1 = distToSeg(p, b, c);
    float d2 = distToSeg(p, c, a);
    float d = min(d0, min(d1, d2));
    return pointInTri(p, a, b, c) ? -d : d;
}

float sdfEllipseApprox(vec2 p, vec2 c, vec2 r)
{
    vec2 rr = max(r, vec2(1e-8));
    vec2 q = (p - c) / rr;
    return length(q) - 1.0;
}

float evalPrimLocal(int type, vec2 pLocal, Prim pr)
{
    if (type == SDF_CIRCLE)      return sdfCircle(pLocal, pr.p0.xy, pr.p0.z);
    if (type == SDF_RECT)        return sdfRectMinMax(pLocal, pr.p0);
    if (type == SDF_ROUNDEDRECT) return sdfRoundedRectMinMax(pLocal, pr.p0, pr.p1.x);
    if (type == SDF_SEGMENT)     return sdfSegment(pLocal, pr.p0.xy, pr.p0.zw, pr.p1.x);
    if (type == SDF_TRIANGLE)    return sdfTriangle(pLocal, pr.p0.xy, pr.p0.zw, pr.p1.xy);
    if (type == SDF_ELLIPSE)     return sdfEllipseApprox(pLocal, pr.p0.xy, pr.p1.xy);
    return 1e30;
}

// -----------------------------------------------------------------------------
// Material blending accumulator
// -----------------------------------------------------------------------------

struct AccMat
{
    float d;
    int m0;
    int m1;
    float w; // weight of m1 (0..1), m0 is (1-w)
};

AccMat accInit(float d, int m)
{
    AccMat a;
    a.d = d;
    a.m0 = m;
    a.m1 = m;
    a.w = 0.0;
    return a;
}

int accDominantMat(AccMat a)
{
    return (a.w > 0.5) ? a.m1 : a.m0;
}

AccMat accSetHard(AccMat a, float dNew, int mNew)
{
    a.d = dNew;
    a.m0 = mNew;
    a.m1 = mNew;
    a.w = 0.0;
    return a;
}

AccMat smoothUnionMat(AccMat a, float db, int mb, float k)
{
    float da = a.d;
    float kk = max(k, 1e-8);

    float h = clamp(0.5 + 0.5 * (db - da) / kk, 0.0, 1.0);
    float d = mix(db, da, h) - kk * h * (1.0 - h);

    // h near 1 => a wins; near 0 => b wins
    int ma = accDominantMat(a);

    AccMat r;
    r.d = d;
    r.m0 = mb;   // "other"
    r.m1 = ma;   // "winner-ish"
    r.w  = h;    // weight of ma
    return r;
}

AccMat smoothIntersectMat(AccMat a, float db, int mb, float k)
{
    // smoothMax(a, b, k) == -smoothMin(-a, -b, k)
    // Blend factor is analogous but with signs.
    float da = a.d;
    float kk = max(k, 1e-8);

    // Use the smooth-min blending factor on negated distances
    float h = clamp(0.5 + 0.5 * ((-db) - (-da)) / kk, 0.0, 1.0); // == clamp(0.5 + 0.5*(da-db)/k)
    float d = - (mix(-db, -da, h) - kk * h * (1.0 - h));

    // For intersection, larger distance dominates (the limiting surface).
    // With this h, when da > db, h tends to 1 => a dominates.
    int ma = accDominantMat(a);

    AccMat r;
    r.d = d;
    r.m0 = mb;
    r.m1 = ma;
    r.w  = h;
    return r;
}

// -----------------------------------------------------------------------------
// Color rules: "last wins" overwrite (also enables outside rendering by changing alpha)
// -----------------------------------------------------------------------------

float ruleMask(ColorRule r, float sd)
{
    if (r.mode == RULE_SD_LT)
    {
        if (r.feather <= 0.0) return (sd < r.a) ? 1.0 : 0.0;
        return 1.0 - smoothstep(r.a - r.feather, r.a + r.feather, sd);
    }
    if (r.mode == RULE_SD_GT)
    {
        if (r.feather <= 0.0) return (sd > r.a) ? 1.0 : 0.0;
        return smoothstep(r.a - r.feather, r.a + r.feather, sd);
    }
    if (r.mode == RULE_RANGE)
    {
        float lo = r.a, hi = r.b;
        if (lo > hi) { float t = lo; lo = hi; hi = t; }
        if (r.feather <= 0.0) return (sd >= lo && sd <= hi) ? 1.0 : 0.0;

        float m1 = smoothstep(lo - r.feather, lo + r.feather, sd);
        float m2 = 1.0 - smoothstep(hi - r.feather, hi + r.feather, sd);
        return clamp(m1 * m2, 0.0, 1.0);
    }
    if (r.mode == RULE_NEAR_VALUE)
    {
        float d = abs(sd - r.a);
        float tol = max(r.b, 0.0);
        if (r.feather <= 0.0) return (d <= tol) ? 1.0 : 0.0;
        return 1.0 - smoothstep(tol - r.feather, tol + r.feather, d);
    }
    return 0.0;
}

// last-wins overwrite of full RGBA (so rules can make outside visible)
vec4 applyRulesRGBA(vec4 base, float sd, int firstRule, int count)
{
    vec4 col = base;
    for (int i = 0; i < count; i++)
    {
        ColorRule r = rules[firstRule + i];
        if (r.mode == RULE_DISABLED) continue;

        float m = ruleMask(r, sd);
        // overwrite RGBA
        col = mix(col, r.color, m);
    }
    return col;
}

// -----------------------------------------------------------------------------
// Scene evaluation (distance + materials)
// -----------------------------------------------------------------------------

AccMat sceneEval(vec2 pWorld)
{
    // If there are no prims, return "empty".
    if (primCount <= 0)
    return accInit(1e30, 0);

    // Subtraction block accumulation: A - union(Bs) at end
    bool  hasSub    = false;
    bool  smoothSub = false;
    float kSub      = 0.0;
    float dbUnion   = 1e30;
    int   mbUnion   = 0; // we don't use it by default (edge-style-from-B would use this)

    AccMat acc;

    // i=0 defines the base A (mat comes from prim[0].matId)
    {
        Prim pr0 = prims[0];
        vec2 pLocal0 = (pr0.worldToLocal * vec4(pWorld, 0.0, 1.0)).xy;
        float d0 = evalPrimLocal(pr0.type, pLocal0, pr0);
        acc = accInit(d0, pr0.matId);
    }

    int n = primCount;
    for (int i = 1; i < n; i++)
    {
        Prim pr = prims[i];
        vec2 pLocal = (pr.worldToLocal * vec4(pWorld, 0.0, 1.0)).xy;
        float d = evalPrimLocal(pr.type, pLocal, pr);

        // Collect subtractors into a union distance, applied once at end.
        if (pr.op == OP_SUBTRACT || pr.op == OP_SMOOTH_SUB)
        {
            hasSub = true;

            float kk = max(pr.k, 0.0);
            if (pr.op == OP_SMOOTH_SUB && kk > 0.0)
            {
                smoothSub = true;
                kSub = max(kSub, kk);

                if (dbUnion > 1e29) { dbUnion = d; mbUnion = pr.matId; }
                else dbUnion = smoothMin(dbUnion, d, kk);
            }
            else
            {
                if (dbUnion > 1e29) { dbUnion = d; mbUnion = pr.matId; }
                else dbUnion = min(dbUnion, d);
            }
            continue;
        }

        // Normal ops:
        if (pr.op == OP_UNION)
        {
            if (d < acc.d) acc = accSetHard(acc, d, pr.matId);
            else acc.d = min(acc.d, d);
            continue;
        }

        if (pr.op == OP_INTERSECT)
        {
            if (d > acc.d) acc = accSetHard(acc, d, pr.matId);
            else acc.d = max(acc.d, d);
            continue;
        }

        if (pr.op == OP_SMOOTH_UNION)
        {
            float kk = max(pr.k, 0.0);
            if (kk > 0.0) acc = smoothUnionMat(acc, d, pr.matId, kk);
            else
            {
                if (d < acc.d) acc = accSetHard(acc, d, pr.matId);
                else acc.d = min(acc.d, d);
            }
            continue;
        }

        if (pr.op == OP_SMOOTH_INTER)
        {
            float kk = max(pr.k, 0.0);
            if (kk > 0.0) acc = smoothIntersectMat(acc, d, pr.matId, kk);
            else
            {
                if (d > acc.d) acc = accSetHard(acc, d, pr.matId);
                else acc.d = max(acc.d, d);
            }
            continue;
        }

        // OP_SUBTRACT here (non-canonical path) -> keep A's mat
        if (pr.op == OP_SUBTRACT)
        {
            acc.d = max(acc.d, -d);
            continue;
        }

        // OP_SMOOTH_SUB here (non-canonical path) -> keep A's mat
        if (pr.op == OP_SMOOTH_SUB)
        {
            float kk = max(pr.k, 0.0);
            acc.d = (kk > 0.0) ? smoothMax(acc.d, -d, kk) : max(acc.d, -d);
            continue;
        }
    }

    // Apply subtraction ONCE at the end: A - union(Bs)
    if (hasSub)
    {
        if (smoothSub && kSub > 0.0) acc.d = smoothMax(acc.d, -dbUnion, kSub);
        else                         acc.d = max(acc.d, -dbUnion);

        // NOTE (optional future): "use subtract material on seam" can be implemented here
        // by creating a 2-mat blend near the boundary using dbUnion and acc.d gradients.
        // For now: material remains the A-side material (acc keeps its mats).
    }

    return acc;
}

// -----------------------------------------------------------------------------
// Shading (stroke-over-fill) then apply rules to final RGBA (last wins)
// -----------------------------------------------------------------------------

vec4 shadeMaterial(int matId, float sd)
{
    // Safety
    if (matId < 0 || matId >= materialCount)
    return vec4(0.0);

    Material m = materials[matId];

    // Base style for this material
    vec4 fillC   = m.fillColor;
    vec4 strokeC = m.strokeColor;

    float feather = max(m.featherPx, 0.0);

    float aFill = 0.0;
    if (m.hasFill != 0)
    aFill = fillAlpha(sd, feather) * fillC.a;

    float aStroke = 0.0;
    if (m.hasStroke != 0 && strokeC.a > 0.0 && m.strokeThickness > 0.0)
    aStroke = strokeAlpha(sd, 0.5 * m.strokeThickness, feather) * strokeC.a;

    // stroke-over-fill
    vec3 col = vec3(0.0);
    float a  = 0.0;

    if (aFill > 0.0)
    {
        col = fillC.rgb;
        a   = aFill;
    }

    if (aStroke > 0.0)
    {
        float outA = aStroke + a * (1.0 - aStroke);
        vec3 outC  = (strokeC.rgb * aStroke + col * a * (1.0 - aStroke)) / max(outA, 1e-8);
        col = outC;
        a   = outA;
    }

    vec4 rgba = vec4(col, a);

    // Apply rule stack (can override alpha too -> enables outside rendering)
    if (m.ruleCount > 0)
    rgba = applyRulesRGBA(rgba, sd, m.ruleFirst, m.ruleCount);

    return rgba;
}

void main()
{
    AccMat acc = sceneEval(vWorldPx);
    float sd   = acc.d;

    // Blend the two materials if present
    vec4 c0 = shadeMaterial(acc.m0, sd);
    vec4 c1 = shadeMaterial(acc.m1, sd);
    vec4 outC = mix(c0, c1, clamp01(acc.w));

    // Kill extremely tiny alpha
    if (outC.a <= 0.0) discard;

    oColor = outC;
}