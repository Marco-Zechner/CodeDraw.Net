#version 450 core

in vec2 vUv;
in vec2 vWorldPx;

out vec4 oColor;

// Compile-time cap
const int MAX_BLEND_SDFS = 8;

// Runtime control (1..MAX_BLEND_SDFS).
uniform int uMaxBlendSdfs = 4;

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
const int RULE_DISABLED     = 0;
const int RULE_SD_LT        = 1;
const int RULE_SD_GT        = 2;
const int RULE_RANGE        = 3;
const int RULE_NEAR_VALUE   = 4;
const int RULE_GRADIENT     = 5;
const int RULE_GRADIENT_STEP= 6;

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
    int mode;
    int _pad0;
    int _pad1;
    int _pad2;

    // colorA (straight alpha)
    vec4 color;

    // thresholds / params:
    // a = sdMin, b = sdMax for gradient modes
    float a;
    float b;

    // feather for other modes, and for gradient modes it's optional edge feathering
    float feather;

    // step size in pixels for RULE_GRADIENT_STEP (>= 0). ignored otherwise.
    float step;

    // colorB for gradient modes (straight alpha). ignored otherwise.
    vec4 color2;
};

layout(std430, binding=2) readonly buffer RuleBuffer
{
    int ruleCountTotal;
    int _rPadA, _rPadB, _rPadC;
    ColorRule rules[];
};

// -----------------------------------------------------------------------------
// Gradient helpers
// -----------------------------------------------------------------------------

float safeInvRange(float lo, float hi)
{
    float d = hi - lo;
    return (abs(d) < 1e-8) ? 0.0 : (1.0 / d);
}

float saturate(float x) { return clamp(x, 0.0, 1.0); }

// Continuous gradient factor in [0..1] over sd in [a..b].
float gradientT(ColorRule r, float sd)
{
    float lo = r.a;
    float hi = r.b;
    if (lo > hi) { float tmp = lo; lo = hi; hi = tmp; }

    float inv = safeInvRange(lo, hi);
    float t = (sd - lo) * inv;
    return saturate(t);
}

// Stepped gradient: quantize sd to steps of size r.step within [a..b].
float gradientTStep(ColorRule r, float sd)
{
    float lo = r.a;
    float hi = r.b;
    if (lo > hi) { float tmp = lo; lo = hi; hi = tmp; }

    float stepPx = max(r.step, 1e-6); // avoid div0
    float sdQ = lo + floor((sd - lo) / stepPx) * stepPx;

    float inv = safeInvRange(lo, hi);
    float t = (sdQ - lo) * inv;
    return saturate(t);
}

// Optional: feather the edges of the [a..b] window so the gradient only applies inside.
// This returns 0 outside, ~1 inside, with soft transitions at lo/hi when feather>0.
float rangeWindowMask(float sd, float lo, float hi, float feather)
{
    if (feather <= 0.0)
    return (sd >= lo && sd <= hi) ? 1.0 : 0.0;

    float m1 = smoothstep(lo - feather, lo + feather, sd);
    float m2 = 1.0 - smoothstep(hi - feather, hi + feather, sd);
    return clamp(m1 * m2, 0.0, 1.0);
}

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

    // For gradient modes, mask is "inside [a..b]" (optionally feathered).
    if (r.mode == RULE_GRADIENT || r.mode == RULE_GRADIENT_STEP)
    {
        float lo = min(r.a, r.b);
        float hi = max(r.a, r.b);
        return rangeWindowMask(sd, lo, hi, r.feather);
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

        if (r.mode == RULE_GRADIENT)
        {
            float m = ruleMask(r, sd);
            float t = gradientT(r, sd);
            vec4 g = mix(r.color, r.color2, t);
            col = mix(col, g, m);
            continue;
        }

        if (r.mode == RULE_GRADIENT_STEP)
        {
            float m = ruleMask(r, sd);
            float t = gradientTStep(r, sd);
            vec4 g = mix(r.color, r.color2, t);
            col = mix(col, g, m);
            continue;
        }

        // existing rules:
        float m = ruleMask(r, sd);
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

// Pushes a candidate (delta + matId) into a small sorted top-K list (ascending delta).
void pushCandidate(inout float deltas[MAX_BLEND_SDFS],
inout int   mats[MAX_BLEND_SDFS],
int k,
float delta,
int matId)
{
    // Reject obviously far candidates early (optional)
    // if (delta > 1e6) return;

    // Insert sort into fixed array
    // Find insertion pos
    int pos = k;
    for (int i = 0; i < k; i++)
    {
        if (delta < deltas[i]) { pos = i; break; }
    }
    if (pos >= k) return;

    // Shift down
    for (int i = k - 1; i > pos; i--)
    {
        deltas[i] = deltas[i - 1];
        mats[i]   = mats[i - 1];
    }

    deltas[pos] = delta;
    mats[pos]   = matId;
}

// Blend K materials by softmax weights.
// sd is the final scene distance; deltas are (d_i - sdMin).
vec4 blendMaterialsSoftmax(float sd,
float deltas[MAX_BLEND_SDFS],
int mats[MAX_BLEND_SDFS],
int k,
float beta)
{
    // beta controls how "wide" the mix is:
    // larger beta => sharper, smaller beta => more gooey blending
    // beta unit is 1/px (since sd is in px).
    beta = max(beta, 1e-6);

    // Compute weights
    float wSum = 0.0;
    float w[MAX_BLEND_SDFS];

    for (int i = 0; i < k; i++)
    {
        // deltas[0] is 0-ish (best). Others are >=0
        float x = -beta * deltas[i];
        // Avoid underflow a bit
        x = max(x, -80.0);
        w[i] = exp(x);
        wSum += w[i];
    }

    if (wSum <= 1e-8)
    return vec4(0.0);

    // Weighted sum in premultiplied space (helps a lot)
    vec4 acc = vec4(0.0);

    for (int i = 0; i < k; i++)
    {
        float wi = w[i] / wSum;

        vec4 c = shadeMaterial(mats[i], sd);

        // premul accumulate
        acc.rgb += c.rgb * c.a * wi;
        acc.a   += c.a * wi;
    }

    // unpremul
    if (acc.a > 1e-8) acc.rgb /= acc.a;
    return acc;
}

// Collect top-K union contributors near the final surface.
// We base proximity on delta = d_i - sdMin, where sdMin is the minimum distance among union participants.
// Note: This ignores subtractors; you can extend it, but start with unions.
void collectUnionContributors(vec2 pWorld,
out float sdMin,
out float deltas[MAX_BLEND_SDFS],
out int mats[MAX_BLEND_SDFS],
int k)
{
    // init
    sdMin = 1e30;
    for (int i = 0; i < MAX_BLEND_SDFS; i++)
    {
        deltas[i] = 1e30;
        mats[i]   = 0;
    }

    // First pass: compute sdMin of the scene as you already do? We'll do local min among union-like ops.
    // If you want exact match with your sceneEval (including smooth union), sdMin is still the result.
    // Here we use plain min(d_i) as a stable reference for deltas.
    // If you want tighter matching, you can set sdMin = sceneEval(pWorld).d (but then deltas can be negative for smoothMin).
    for (int i = 0; i < primCount; i++)
    {
        Prim pr = prims[i];

        // Ignore subtractors here (they're applied at end in your pipeline).
        if (pr.op == OP_SUBTRACT || pr.op == OP_SMOOTH_SUB) continue;

        vec2 pLocal = (pr.worldToLocal * vec4(pWorld, 0.0, 1.0)).xy;
        float d = evalPrimLocal(pr.type, pLocal, pr);

        // For intersection, the active surface uses max; mixing there is different.
        // Skip intersects for now, or handle separately.
        // For now: consider only union-ish ops:
        // - base prim (i==0) has no op meaning
        // - OP_UNION / OP_SMOOTH_UNION
        bool unionish = (i == 0) ||
        (pr.op == OP_UNION) ||
        (pr.op == OP_SMOOTH_UNION);

        if (!unionish) continue;

        sdMin = min(sdMin, d);
    }

    if (sdMin > 1e29)
    {
        sdMin = 1e30;
        return;
    }

    // Second pass: push top-K closest by delta
    for (int i = 0; i < primCount; i++)
    {
        Prim pr = prims[i];
        if (pr.op == OP_SUBTRACT || pr.op == OP_SMOOTH_SUB) continue;

        vec2 pLocal = (pr.worldToLocal * vec4(pWorld, 0.0, 1.0)).xy;
        float d = evalPrimLocal(pr.type, pLocal, pr);

        bool unionish = (i == 0) ||
        (pr.op == OP_UNION) ||
        (pr.op == OP_SMOOTH_UNION);
        if (!unionish) continue;

        float delta = max(d - sdMin, 0.0);
        pushCandidate(deltas, mats, k, delta, pr.matId);
    }
}

void main()
{
    // Keep your exact distance logic for geometry (including subtract)
    AccMat acc = sceneEval(vWorldPx);
    float sd   = acc.d;

    // If you want: only do multi-blend near surface for speed.
    // e.g. if abs(sd) > 100.0 -> just use dominant mat
    // but you said cost is fine.

    int k = clamp(uMaxBlendSdfs, 1, MAX_BLEND_SDFS);

    float sdMin;
    float deltas[MAX_BLEND_SDFS];
    int mats[MAX_BLEND_SDFS];

    collectUnionContributors(vWorldPx, sdMin, deltas, mats, k);

    // If collection failed, fall back to your old 2-mat blend
    vec4 outC;
    if (sdMin > 1e29)
    {
        vec4 c0 = shadeMaterial(acc.m0, sd);
        vec4 c1 = shadeMaterial(acc.m1, sd);
        outC = mix(c0, c1, clamp01(acc.w));
    }
    else
    {
        // Choose beta relative to your smooth union K scale.
        // Rough rule: beta ~ 1 / (effectiveBlendWidthPx)
        // If you tend to use pr.k ~ 10..30, beta around 0.15..0.05 is reasonable.
        float beta = 0.10;

        outC = blendMaterialsSoftmax(sd, deltas, mats, k, beta);
    }

    if (outC.a <= 0.0) discard;
    oColor = outC;
}