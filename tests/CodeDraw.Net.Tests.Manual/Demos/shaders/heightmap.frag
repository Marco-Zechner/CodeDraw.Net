#version 450 core

in vec2 vUv;
out vec4 oColor;

uniform vec2  uMapPos;
uniform vec2  uRes;          // pixels (W,H)
uniform float uHeightAmp = 1.0;

// --- island controls ---
uniform float uIslandRadius = 0.85;  // bigger = larger island (0.6..1.2)
uniform float uIslandEdge   = 0.35;  // softness of edge (0.15..0.6)

// --- height shaping ---
uniform float uSeaLevelBias = 0.00;  // shifts heights up/down (-0.2..0.2)
uniform float uPeakSoftness = 2.0;   // higher => less flat cap (1.2..4.0)
uniform float uDetail       = 0.35;  // adds small detail on top (0..0.7)

float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.13);
    p3 += dot(p3, p3.yzx + 3.333);
    return fract((p3.x + p3.y) * p3.z);
}

float valueNoise(vec2 x)
{
    vec2 i = floor(x);
    vec2 f = fract(x);

    float a = hash12(i);
    float b = hash12(i + vec2(1.0, 0.0));
    float c = hash12(i + vec2(0.0, 1.0));
    float d = hash12(i + vec2(1.0, 1.0));

    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float fbm(vec2 x)
{
    float v = 0.0;
    float a = 0.5;
    vec2  shift = vec2(100.0);
    mat2  rot = mat2(cos(0.5), sin(0.5), -sin(0.5), cos(0.5));
    for (int i = 0; i < 5; i++)
    {
        v += a * valueNoise(x);
        x = rot * x * 2.0 + shift;
        a *= 0.5;
    }
    return v;
}

// flipped pack: smallest lane in R, largest in A
vec4 packHeight(float h)
{
    h = clamp(h, 0.0, 1.0);
    float x = h * 4.0;
    return vec4(
    clamp(x,       0.0, 1.0),
    clamp(x - 1.0, 0.0, 1.0),
    clamp(x - 2.0, 0.0, 1.0),
    clamp(x - 3.0, 0.0, 1.0)
    );
}

// Soft clip (prevents hard plateau):
// maps [0..inf) into [0..1) smoothly.
float softClip01(float x, float k)
{
    // k ~ 1..4
    x = max(x, 0.0);
    return 1.0 - exp(-x * k);
}

void main()
{
    float aspect = (uRes.y > 1e-6) ? (uRes.x / uRes.y) : 1.0;
    vec2 resAspect = vec2(aspect, 1.0);

    vec2 p = (vUv * resAspect) + uMapPos;

    // Base landmass noise (0..~1)
    float n = fbm(p * 4.0);

    // Add some higher-frequency detail so peaks aren’t just “one blob”
    float nHi = fbm(p * 14.0);
    n = mix(n, 0.65 * n + 0.35 * nHi, clamp(uDetail, 0.0, 1.0));

    // Make mountains feel “mountainy”
    // (push mid-highs up while keeping lowlands)
    n = pow(max(n, 0.0), 1.35);

    // Island mask: bigger + smoother edge
    vec2 c = (vUv - vec2(0.5)) * vec2(aspect, 1.0);
    float d = length(c); // 0 at center

    // radius controls where it starts fading, edge controls softness
    float mask = 1.0 - smoothstep(uIslandRadius, uIslandRadius + uIslandEdge, d);

    // Height before clip
    float hRaw = (n * mask) * uHeightAmp + uSeaLevelBias;

    // Soft clip to avoid flat plateau at 1.0
    float h = softClip01(hRaw, max(uPeakSoftness, 1e-3));

    oColor = packHeight(h);
}