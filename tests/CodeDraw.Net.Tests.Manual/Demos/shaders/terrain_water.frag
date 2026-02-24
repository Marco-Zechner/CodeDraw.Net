#version 450 core

in vec2 vUv;
out vec4 oColor;

uniform sampler2D uHeightMap;

uniform float uTime;
uniform float uWaterLevel;

uniform vec3  uSunPos;            // (x,y,z) in 0..1 for x/y, z 0..1 height
uniform vec2  uPix;               // (1/W, 1/H)

uniform float uAmbientStrength;
uniform vec3  uAmbientColor;
uniform vec3  uLightColor;

uniform float uTerrainSpec  = 0.03; // basically off (0..0.08)
uniform float uWaterSpec    = 0.55; // shiny water
uniform float uTerrainRough = 1.0;  // 0.6..1.6 (higher = rougher)

// Water motion knobs
uniform vec2  uWaterDir = vec2(-0.7, 0.7); // base flow direction in UV
uniform float uWaterSpeed = -0.1;          // UV/sec base 
uniform float uCurrentTwist = 0.100;       // how much terrain bends the flow 
uniform float uCurrentCurl  = 0.005;       // small curl noise contribution   

// Foam knobs
uniform float uFoamStrength = 1.0;
uniform float uFoamShoreWidth = 0.020;      // in height units (depth)
uniform float uFoamCrestStart = 0.05;       // where foam starts on steepness (0..1)
uniform float uFoamCrestWidth = 0;        // softness

float saturate(float x) { return clamp(x, 0.0, 1.0); }

float unpackHeight(vec4 hPacked)
{
    return (hPacked.r + hPacked.g + hPacked.b + hPacked.a) * 0.25;
}

// ------------------------------------------------------------
// Seam-proof height sampling (no half-texel offset, no filtering)
// ------------------------------------------------------------
float heightAt(vec2 uv)
{
    ivec2 ts = textureSize(uHeightMap, 0);

    uv = clamp(uv, vec2(0.0), vec2(1.0));

    vec2 p = uv * (vec2(ts) - vec2(1.0));
    ivec2 i0 = ivec2(floor(p));
    vec2  f  = fract(p);

    ivec2 i1 = min(i0 + ivec2(1, 0), ts - ivec2(1));
    ivec2 i2 = min(i0 + ivec2(0, 1), ts - ivec2(1));
    ivec2 i3 = min(i0 + ivec2(1, 1), ts - ivec2(1));

    float h00 = unpackHeight(texelFetch(uHeightMap, i0, 0));
    float h10 = unpackHeight(texelFetch(uHeightMap, i1, 0));
    float h01 = unpackHeight(texelFetch(uHeightMap, i2, 0));
    float h11 = unpackHeight(texelFetch(uHeightMap, i3, 0));

    float hx0 = mix(h00, h10, f.x);
    float hx1 = mix(h01, h11, f.x);
    return mix(hx0, hx1, f.y);
}

vec3 terrainNormal(vec2 uv)
{
    float hScale = 140.0;

    float l = hScale * heightAt(uv - vec2(uPix.x, 0.0));
    float r = hScale * heightAt(uv + vec2(uPix.x, 0.0));
    float d = hScale * heightAt(uv - vec2(0.0, uPix.y));
    float u = hScale * heightAt(uv + vec2(0.0, uPix.y));

    return normalize(vec3(l - r, d - u, 1.0));
}

// -----------------------------------------------------------------------------
// Noise (for water detail / curl)
// -----------------------------------------------------------------------------
float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

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
    vec2  shift = vec2(37.0, 91.0);
    mat2  rot = mat2(cos(0.5), sin(0.5), -sin(0.5), cos(0.5));
    for (int i = 0; i < 5; i++)
    {
        v += a * valueNoise2(x);
        x = rot * x * 2.0 + shift;
        a *= 0.5;
    }
    return v;
}

// 2D curl from scalar noise (cheap-ish). Returns a small sideways velocity field.
vec2 curl2(vec2 p)
{
    float e = 1.25; // in "noise domain" units
    float n1 = fbm(p + vec2(0.0, e));
    float n2 = fbm(p - vec2(0.0, e));
    float n3 = fbm(p + vec2(e, 0.0));
    float n4 = fbm(p - vec2(e, 0.0));
    // d/dy and d/dx approximations
    float dy = (n1 - n2) / (2.0 * e);
    float dx = (n3 - n4) / (2.0 * e);
    return vec2(dy, -dx);
}

// -----------------------------------------------------------------------------
// Water flow: coherent direction + terrain bending + small curl
// -----------------------------------------------------------------------------
vec2 flowDir(vec2 uv, float hT)
{
    vec2 d = uWaterDir;
    float l = length(d);
    d = (l > 1e-6) ? d / l : vec2(-0.7, 0.7);

    // Terrain gradient bends flow (currents follow “channels”).
    // Using height samples directly: grad points uphill; currents tend to be deflected around it.
    float hx = heightAt(uv + vec2(uPix.x, 0.0)) - heightAt(uv - vec2(uPix.x, 0.0));
    float hy = heightAt(uv + vec2(0.0, uPix.y)) - heightAt(uv - vec2(0.0, uPix.y));
    vec2 grad = vec2(hx, hy) / max(2.0 * min(uPix.x, uPix.y), 1e-6);

    // Only really affect water, and mostly near shore (where terrain matters).
    float shore = saturate(1.0 - (uWaterLevel - hT) / 0.06); // 1 near shore, 0 deeper
    vec2 bend = normalize(vec2(-grad.y, grad.x) + 1e-6) * uCurrentTwist * shore;

    // Curl noise adds interest everywhere, but small.
    vec2 c = curl2(uv * 24.0 + uTime * 0.25) * uCurrentCurl;

    vec2 outD = d + bend + c;
    float ll = length(outD);
    return (ll > 1e-6) ? outD / ll : d;
}

// Advect UV in the same direction but with different “detail levels” (different scales/speeds)
vec2 advectUv(vec2 uv, vec2 dir, float speed, float t)
{
    return uv + dir * (t * speed);
}

// -----------------------------------------------------------------------------
// Water surface: multi-band traveling waves (same direction, different speeds)
// -----------------------------------------------------------------------------
float waterHeight(vec2 uv, vec2 dir, float hT)
{
    // Normalize direction and build along/perp basis
    vec2 d = dir;
    vec2 p = vec2(-d.y, d.x);

    float t = uTime;

    // Base “swell” aligned with direction
    float s0 = dot(uv, d) * 18.0 + t * (1.2 * uWaterSpeed / max(length(d), 1e-6));
    float s1 = dot(uv, d) * 11.0 - t * (0.9 * uWaterSpeed / max(length(d), 1e-6));

    float swell =
    0.010 * sin(s0) +
    0.007 * sin(s1);

    // Mid detail: same direction, faster, smaller amplitude
    float m0 = dot(uv, d) * 42.0 + t * (2.8 * uWaterSpeed);
    float mid = 0.0045 * sin(m0);

    // Fine ripples: use fbm, also moving same direction, fastest
    vec2 uvFine = uv * 70.0 + d * (t * (4.5 * uWaterSpeed)) + vec2(t * 0.15, -t * 0.11);
    float fine = (fbm(uvFine) * 2.0 - 1.0) * 0.0028;

    // Shore sharpening: slightly higher and choppier near shallow water
    float depth = max(uWaterLevel - hT, 0.0);
    float shore = saturate(1.0 - depth / 0.05);

    float shoreBoost = 1.0 + 0.8 * shore;

    return uWaterLevel + (swell + mid + fine) * shoreBoost;
}

vec3 waterNormal(vec2 uv, vec2 dir, float hT)
{
    float epsx = uPix.x;
    float epsy = uPix.y;

    float h0 = waterHeight(uv, dir, hT);
    float hx = waterHeight(uv + vec2(epsx, 0.0), dir, hT);
    float hy = waterHeight(uv + vec2(0.0, epsy), dir, hT);

    float k = 220.0;
    return normalize(vec3((h0 - hx) * k, (h0 - hy) * k, 1.0));
}

// ---- shadows: simple raymarch in heightfield ----
float softShadow(vec3 p0, vec3 sunDir)
{
    vec3 p = p0 + sunDir * 0.002;

    const int MAX_STEPS = 170;
    float minStep = min(uPix.x, uPix.y) * 0.8;

    for (int i = 0; i < MAX_STEPS; i++)
    {
        if (p.x < 0.0 || p.x > 1.0 || p.y < 0.0 || p.y > 1.0) break;
        if (p.z > 1.05) break;

        float hT = heightAt(p.xy);
        if (hT > p.z) return 0.0;

        float dz = p.z - hT;
        float stepLen = max(minStep, dz * 0.06);
        p += sunDir * stepLen;
    }

    return 1.0;
}

vec3 terrainColor(float h, vec3 n)
{
    vec3 sand   = vec3(0.839, 0.714, 0.620);
    vec3 grass  = vec3(0.596, 0.678, 0.353);
    vec3 bush   = vec3(0.396, 0.522, 0.255);
    vec3 forest = vec3(0.278, 0.463, 0.271);
    vec3 stone  = vec3(0.427, 0.463, 0.529);
    vec3 slate  = vec3(0.518, 0.553, 0.604);
    vec3 snow   = vec3(0.824, 0.878, 0.871);

    vec3 col = snow;
    if (h < 0.42) col = sand;
    else if (h < 0.54) col = grass;
    else if (h < 0.64) col = bush;
    else if (h < 0.74) col = forest;
    else if (h < 0.86) col = stone;
    else if (h < 0.93) col = slate;

    float flatness = dot(n, vec3(0.0, 0.0, 1.0));
    float steep = 1.0 - smoothstep(0.58, 0.78, flatness);
    float high  = smoothstep(0.55, 0.78, h);
    float forceStone = steep * high;

    return mix(col, stone, forceStone);
}

// -----------------------------------------------------------------------------
// Foam: (A) shoreline foam based on depth, (B) crest foam based on steep normals
// -----------------------------------------------------------------------------
float shorelineFoam(float depth)
{
    // depth = water - terrain, so 0 at shoreline and positive in water
    // Strongest at shoreline, fades out into water over uFoamShoreWidth.
    float w = max(uFoamShoreWidth, 1e-5);
    float f = 1.0 - smoothstep(0.0, w, depth);
    return f;
}

float crestFoam(vec3 nW)
{
    // Use how much the normal tilts away from up as “choppiness”.
    // 1 - n.z is 0 on flat, larger on crests.
    float steep = 1.0 - clamp(nW.z, 0.0, 1.0);
    float f = smoothstep(uFoamCrestStart, uFoamCrestStart + uFoamCrestWidth, steep);
    return f;
}

float foamMask(vec2 uv, vec2 dir, float hT, float hW, vec3 nW)
{
    float depth = max(hW - hT, 0.0);

    float shore = shorelineFoam(depth);
    float crest = crestFoam(nW);

    // Add animated breakup so it’s not a perfect band.
    float t = uTime * 0.45;
    float n = fbm(uv * 55.0 + dir * (t * 0.7));
    n = saturate((n - 0.45) * 2.2); // threshold-ish

    // Shore foam: strong + a bit of noisy breakup
    float shoreFoam = shore * (0.65 + 0.35 * n);

    // Crest foam: mostly in water, and reduced at deep water
    float deep = saturate(depth / 0.12);
    float crestFoam = crest * (1.0 - 0.75 * deep) * (0.55 + 0.45 * n);

    float f = saturate(shoreFoam + crestFoam);
    return f * uFoamStrength;
}

void main()
{
    vec2 uv = vUv;

    float hT = heightAt(uv);

    // Terrain-aware flow direction (gives you “more interesting than top-left”)
    vec2 dir = flowDir(uv, hT);

    float hW = waterHeight(uv, dir, hT);

    float isWater = step(hT, hW);
    float depth = max(hW - hT, 0.0);

    vec3 nT = terrainNormal(uv);
    vec3 nW = waterNormal(uv, dir, hT);
    vec3 n  = normalize(mix(nT, nW, isWater));

    vec3 sunDir = normalize(uSunPos - vec3(0.5, 0.5, 0.0));

    float hSurf = mix(hT, hW, isWater);
    vec3 pSurf = vec3(uv, hSurf);

    float lit = softShadow(pSurf, sunDir);

    // --- Diffuse ---
    float ndlTerrain = pow(max(dot(nT, sunDir), 0.0), uTerrainRough);
    float ndlWater   = max(dot(nW, sunDir), 0.0);
    float ndlMix = mix(ndlTerrain, ndlWater, isWater);

    vec3 ambient = clamp(uAmbientColor * uAmbientStrength, 0.0, 1.0);
    vec3 direct  = clamp(uLightColor * (0.10 + 0.90 * ndlMix) * lit, 0.0, 2.0);

    // --- View / half vector ---
    vec3 viewPos = vec3(0.5, 0.5, 2.0);
    vec3 V = normalize(viewPos - pSurf);
    vec3 H = normalize(sunDir + V);

    // --- Base colors ---
    vec3 colTerrain = terrainColor(hT, nT);

    vec3 deepWater = vec3(0.00, 0.15, 0.28);
    vec3 shallow   = vec3(0.10, 0.35, 0.45);

    float waterTint = saturate(depth / 0.08);
    vec3 colWater = mix(shallow, deepWater, waterTint);

    // Foam color (slightly warm white so it sits in the scene)
    float foam = foamMask(uv, dir, hT, hW, nW) * isWater;
    vec3 foamCol = vec3(0.92, 0.95, 0.98);

    // Mix foam into water base before lighting (so it gets shaded too)
    colWater = mix(colWater, foamCol, foam);

    vec3 base = mix(colTerrain, colWater, isWater);
    vec3 litCol = (ambient + direct) * base;

    // --- Fresnel (water only) ---
    float fres = pow(1.0 - max(dot(nW, V), 0.0), 5.0);
    fres = clamp(fres, 0.0, 1.0);

    // --- Specular ---
    // Reduce water spec in shallow water + in foam (foam is diffuse/matte).
    float shallowKill = 1.0 - smoothstep(0.0, 0.02, depth);
    float foamKill = 1.0 - foam;
    float specMask = (1.0 - 0.65 * shallowKill) * foamKill;

    float waterSpec = pow(max(dot(nW, H), 0.0), 96.0) * uWaterSpec * lit * specMask;
    vec3  waterSpecCol = uLightColor * waterSpec;

    float terrainSpec = pow(max(dot(nT, H), 0.0), 16.0) * uTerrainSpec * lit;
    vec3  terrainSpecCol = uLightColor * terrainSpec;

    // Extra terms
    vec3 waterExtra =
    (waterSpecCol + fres * vec3(0.06, 0.10, 0.12)) * isWater;

    vec3 terrainExtra = terrainSpecCol * (1.0 - isWater);

    oColor = vec4(litCol + waterExtra + terrainExtra, 1.0);
}