#version 450 core

in vec2 vUv;
in vec2 vWorldPx;

out vec4 oColor;

// --- Style (per draw call) ---
uniform vec4 uFillColor;      // rgba
uniform vec4 uStrokeColor;    // rgba
uniform float uStrokeThickness; // px
uniform float uFeatherPx;       // px
uniform int uHasFill;           // 0/1
uniform int uHasStroke;         // 0/1

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
const int OP_SMOOTH_SUB = 6;

struct Prim
{
    int type;
    int op;
    int _pad0;
    int _pad1;

    mat4 worldToLocal;

    vec4 p0;
    vec4 p1;

    float k;
    float _pad2, _pad3, _pad4; // align to 16 bytes
};

// std430 SSBO
layout(std430, binding=0) readonly buffer PrimBuffer
{
    int primCount;
    int _padA, _padB, _padC; // align to 16 bytes
    Prim prims[];
};

float clamp01(float x) { return clamp(x, 0.0, 1.0); }

float smoothMin(float a, float b, float k)
{
    // polynomial smooth-min
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

// IQ box SDF (axis-aligned), local space
float sdfBox(vec2 p, vec2 c, vec2 eHalf)
{
    vec2 d = abs(p - c) - eHalf;
    float outside = length(max(d, vec2(0.0)));
    float inside = min(max(d.x, d.y), 0.0);
    return outside + inside;
}

float sdfRectMinMax(vec2 p, vec4 minMax)
{
    // minMax = (minX, minY, maxX, maxY)
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
    if (type == SDF_CIRCLE)
    {
        // p0.xy=center, p0.z=radius
        return sdfCircle(pLocal, pr.p0.xy, pr.p0.z);
    }
    if (type == SDF_RECT)
    {
        // p0 = (minX,minY,maxX,maxY)
        return sdfRectMinMax(pLocal, pr.p0);
    }
    if (type == SDF_ROUNDEDRECT)
    {
        // p0 = minMax, p1.x=radius
        return sdfRoundedRectMinMax(pLocal, pr.p0, pr.p1.x);
    }
    if (type == SDF_SEGMENT)
    {
        // p0.xy=a, p0.zw=b, p1.x=radius
        return sdfSegment(pLocal, pr.p0.xy, pr.p0.zw, pr.p1.x);
    }
    if (type == SDF_TRIANGLE)
    {
        // p0.xy=a, p0.zw=b, p1.xy=c
        return sdfTriangle(pLocal, pr.p0.xy, pr.p0.zw, pr.p1.xy);
    }
    if (type == SDF_ELLIPSE)
    {
        // p0.xy=center, p1.xy=radius
        return sdfEllipseApprox(pLocal, pr.p0.xy, pr.p1.xy);
    }

    // unknown => empty
    return 1e30;
}

float combine(float acc, float d, int op, float k)
{
    if (op == OP_UNION)     return min(acc,  d);
    if (op == OP_INTERSECT) return max(acc,  d);
    if (op == OP_SUBTRACT)  return max(acc, -d);

    float kk = max(k, 0.0);
    if (kk <= 0.0)
    {
        // fallback to hard ops if k==0
        if (op == OP_SMOOTH_UNION) return min(acc,  d);
        if (op == OP_SMOOTH_INTER) return max(acc,  d);
        if (op == OP_SMOOTH_SUB) return max(acc, -d);
    }
    else
    {
        if (op == OP_SMOOTH_UNION) return smoothMin(acc,  d, kk);
        if (op == OP_SMOOTH_INTER) return smoothMax(acc,  d, kk);
        if (op == OP_SMOOTH_SUB) return smoothMax(acc, -d, kk); //TODO: only fine for single subtraction. rework to handle multiple subtractions properly
    }

    return min(acc, d);
}

float sceneSdf(vec2 pWorld)
{
    float acc = 1e30;

    // Subtraction block accumulation: A - union(Bs)
    bool hasSub = false;
    bool smoothSub = false;
    float kSub = 0.0;
    float dbUnion = 1e30; // union distance of Bs

    int n = primCount;
    for (int i = 0; i < n; i++)
    {
        Prim pr = prims[i];

        vec2 pLocal = (pr.worldToLocal * vec4(pWorld, 0.0, 1.0)).xy;
        float d = evalPrimLocal(pr.type, pLocal, pr);

        if (i == 0)
        {
            acc = d; // first prim is always the "A/base" distance in your encoding
            continue;
        }

        // --- subtraction block: collect union(Bs) ---
        if (pr.op == OP_SUBTRACT || pr.op == OP_SMOOTH_SUB)
        {
            hasSub = true;

            float kk = max(pr.k, 0.0);
            if (pr.op == OP_SMOOTH_SUB && kk > 0.0)
            {
                smoothSub = true;
                // pick a single kSub; simplest is max over all subtract prims
                kSub = max(kSub, kk);

                // smooth union of Bs
                if (dbUnion > 1e29) dbUnion = d;           // first subtract
                else dbUnion = smoothMin(dbUnion, d, kk);
            }
            else
            {
                // hard union of Bs
                dbUnion = min(dbUnion, d);
            }

            continue;
        }

        // --- normal combine for non-subtract ops ---
        acc = combine(acc, d, pr.op, pr.k);
    }

    // Apply subtraction ONCE at the end: A - union(Bs)
    if (hasSub)
    {
        if (smoothSub && kSub > 0.0)
        acc = smoothMax(acc, -dbUnion, kSub);
        else
        acc = max(acc, -dbUnion);
    }

    return acc;
}

void main()
{
    float sd = sceneSdf(vWorldPx);

    float aFill = 0.0;
    if (uHasFill != 0)
    aFill = fillAlpha(sd, uFeatherPx) * uFillColor.a;

    float aStroke = 0.0;
    if (uHasStroke != 0 && uStrokeColor.a > 0.0 && uStrokeThickness > 0.0)
    aStroke = strokeAlpha(sd, 0.5 * uStrokeThickness, uFeatherPx) * uStrokeColor.a;

    // simple "stroke over fill"
    vec3 col = vec3(0.0);
    float a = 0.0;

    if (aFill > 0.0)
    {
        col = uFillColor.rgb;
        a = aFill;
    }

    if (aStroke > 0.0)
    {
        // source-over: stroke over fill
        float outA = aStroke + a * (1.0 - aStroke);
        vec3 outC = (uStrokeColor.rgb * aStroke + col * a * (1.0 - aStroke)) / max(outA, 1e-8);
        col = outC;
        a = outA;
    }

    oColor = vec4(col, a);
}