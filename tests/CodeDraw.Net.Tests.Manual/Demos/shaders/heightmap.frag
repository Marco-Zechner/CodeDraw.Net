#version 450 core

in vec2 vUv;
out vec4 oColor;

uniform vec2  uMapPos;     // world offset in "map uv space"
uniform vec2  uRes;        // (W/H, 1)  (CodeDraw provides this in your pipeline)
uniform float uHeightAmp;  // 0..1+

// ----------------------------------------------------------------------------
// Noise (fast value noise + fbm)
// ----------------------------------------------------------------------------
float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.13);
    p3 += dot(p3, p3.yzx + 3.333);
    return fract((p3.x + p3.y) * p3.z);
}

// IMPORTANT: do NOT name this "noise2" (GLSL has a built-in noise2 returning vec2)
float valueNoise2(vec2 x)
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
        v += a * valueNoise2(x);
        x = rot * x * 2.0 + shift;
        a *= 0.5;
    }
    return v;
}

// Pack height into RGBA (4 lanes) like your original
vec4 packHeight(float h)
{
    h = clamp(h, 0.0, 1.0);

    vec4 outv = vec4(0.0);
    h *= 4.0;

    outv.r = clamp(h - 3.0, 0.0, 1.0);
    outv.g = clamp(h - 2.0, 0.0, 1.0);
    outv.b = clamp(h - 1.0, 0.0, 1.0);
    outv.a = clamp(h,       0.0, 1.0);

    return outv;
}

void main()
{
    vec2 p = (vUv * uRes) + uMapPos;

    float n = 1.1 * fbm(p * 5.0);
    n = pow(max(n, 0.0), 1.5);

    // island falloff
    vec2 c = (vUv - vec2(0.5)) * vec2(uRes.x, 1.0);
    float d = length(c) * 2.0;
    float island = clamp(1.3 - d, 0.0, 1.0);

    float h = clamp(n * island * uHeightAmp, 0.0, 1.0);

    oColor = packHeight(h);
}