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

// -----------------------------------------------------------------------------
// Utils
// -----------------------------------------------------------------------------
float saturate(float x) { return clamp(x, 0.0, 1.0); }

float unpackHeight(vec4 hPacked)
{
    // matches your pack: average of lanes
    return (hPacked.r + hPacked.g + hPacked.b + hPacked.a) * 0.25;
}

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

// bilinear-ish height sample using uPix
float heightAt(vec2 uv)
{
    // clamp to avoid sampling outside
    uv = clamp(uv, vec2(0.0), vec2(1.0));

    // sample 4 neighbors in pixel space
    vec2 invPix = vec2(1.0 / max(uPix.x, 1e-9), 1.0 / max(uPix.y, 1e-9));
    vec2 p = uv * invPix;
    vec2 f = fract(p);
    vec2 ip = floor(p) * uPix;

    float tl = unpackHeight(texture(uHeightMap, ip));
    float tr = unpackHeight(texture(uHeightMap, ip + vec2(uPix.x, 0.0)));
    float bl = unpackHeight(texture(uHeightMap, ip + vec2(0.0, uPix.y)));
    float br = unpackHeight(texture(uHeightMap, ip + vec2(uPix.x, uPix.y)));

    float t = mix(tl, tr, f.x);
    float b = mix(bl, br, f.x);
    return mix(t, b, f.y);
}

vec3 terrainNormal(vec2 uv)
{
    // “dry” look: smaller height scale in the normal so it’s less glossy/smooth
    float hScale = 140.0;

    float l = hScale * heightAt(uv - vec2(uPix.x, 0.0));
    float r = hScale * heightAt(uv + vec2(uPix.x, 0.0));
    float d = hScale * heightAt(uv - vec2(0.0, uPix.y));
    float u = hScale * heightAt(uv + vec2(0.0, uPix.y));

    return normalize(vec3(l - r, d - u, 1.0));
}

float waterHeight(vec2 uv)
{
    // More "water-y" than just voronoi jitter:
    // a couple of traveling sine waves + low amp fbm ripples
    float t = uTime * 0.12;

    // big swell
    float swell =
    0.008 * sin((uv.x * 14.0 + uv.y * 6.0) + t * 2.0) +
    0.006 * sin((uv.x * 8.0  - uv.y * 11.0) - t * 1.7);

    // small ripples
    float rip = (fbm(uv * 24.0 + vec2(t, -t * 0.7)) * 2.0 - 1.0) * 0.004;

    return uWaterLevel + swell + rip;
}

vec3 waterNormal(vec2 uv)
{
    float epsx = uPix.x;
    float epsy = uPix.y;

    float h0 = waterHeight(uv);
    float hx = waterHeight(uv + vec2(epsx, 0.0));
    float hy = waterHeight(uv + vec2(0.0, epsy));

    // scale up so waves read as waves, but keep it sane
    float k = 220.0;
    vec3 n = normalize(vec3((h0 - hx) * k, (h0 - hy) * k, 1.0));
    return n;
}

float softShadow(vec3 p0, vec3 sunDir)
{
    // Terrain-only shadowing (water doesn’t cast meaningful shadow here)
    // Start slightly above surface to avoid self-hit
    vec3 p = p0 + sunDir * 0.002;

    const int MAX_STEPS = 170;
    float minStep = min(uPix.x, uPix.y) * 0.8;

    for (int i = 0; i < MAX_STEPS; i++)
    {
        if (p.x < 0.0 || p.x > 1.0 || p.y < 0.0 || p.y > 1.0) break;
        if (p.z > 1.05) break;

        float hT = heightAt(p.xy);
        if (hT > p.z)
        return 0.0; // in shadow

        float dz = p.z - hT;
        float stepLen = max(minStep, dz * 0.06);
        p += sunDir * stepLen;
    }

    return 1.0; // lit
}

vec3 terrainColor(float h, vec3 n)
{
    // less “wet”: don’t overdrive brightness, keep bands, add subtle variation
    float v = (hash12(floor(vUv / max(uPix, vec2(1e-6))) ) * 2.0 - 1.0) * 0.02;
    h = clamp(h + v, 0.0, 1.0);

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

    // steep rock forcing
    float flatness = dot(n, vec3(0.0, 0.0, 1.0));
    float steep = 1.0 - smoothstep(0.58, 0.78, flatness);
    float high  = smoothstep(0.55, 0.78, h);
    float forceStone = steep * high;

    col = mix(col, stone, forceStone);

    return col;
}

void main()
{
    vec2 uv = vUv;

    float hT = heightAt(uv);
    float hW = waterHeight(uv);

    float isWater = step(hT, hW); // 1 if water covers terrain here
    float depth = max(hW - hT, 0.0);

    vec3 nT = terrainNormal(uv);
    vec3 nW = waterNormal(uv);

    vec3 n = normalize(mix(nT, nW, isWater));

    // Sun direction (from surface towards sun)
    vec3 sunDir = normalize(uSunPos - vec3(0.5, 0.5, 0.0));

    // Surface point at visible surface height
    float hSurf = mix(hT, hW, isWater);
    vec3 pSurf = vec3(uv, hSurf);

    // Shadow factor (terrain occlusion)
    float lit = softShadow(pSurf, sunDir);

    // Lighting (less “wet ground”)
    float ndl = max(dot(n, sunDir), 0.0);

    // ambient + directional
    vec3 ambient = clamp(uAmbientColor * uAmbientStrength, 0.0, 1.0);
    vec3 direct  = clamp(uLightColor * (0.18 + 0.82 * ndl) * lit, 0.0, 2.0);

    // View
    vec3 viewPos = vec3(0.5, 0.5, 2.0);
    vec3 V = normalize(viewPos - pSurf);
    vec3 H = normalize(sunDir + V);

    // Terrain shading
    vec3 colTerrain = terrainColor(hT, nT);

    // Water shading: depth tint + fresnel + spec
    vec3 deepWater = vec3(0.00, 0.15, 0.28);
    vec3 shallow   = vec3(0.10, 0.35, 0.45);

    float waterTint = saturate(depth / 0.08);
    vec3 colWater = mix(shallow, deepWater, waterTint);

    // Fresnel (stronger at grazing angles)
    float fres = pow(1.0 - max(dot(nW, V), 0.0), 5.0);
    fres = clamp(fres, 0.0, 1.0);

    // Specular: only really on water, and only if lit
    float spec = pow(max(dot(nW, H), 0.0), 64.0) * 0.8;
    vec3 specCol = uLightColor * spec * lit;

    // Reduce “wet” ground spec almost to zero
    float terrainSpec = pow(max(dot(nT, H), 0.0), 32.0) * 0.06 * lit;
    vec3 terrainSpecCol = uLightColor * terrainSpec;

    // Combine base material
    vec3 base = mix(colTerrain, colWater, isWater);

    // Apply lighting to base (water gets a little fresnel brighten)
    vec3 litCol = (ambient + direct) * base;

    // Add spec/fresnel (water)
    vec3 waterExtra = (specCol + fres * vec3(0.10, 0.18, 0.22)) * isWater;

    // Add tiny terrain spec
    vec3 terrainExtra = terrainSpecCol * (1.0 - isWater);

    oColor = vec4(litCol + waterExtra + terrainExtra, 1.0);
}