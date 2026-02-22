#version 450 core

in vec2 vUV;
out vec4 FragColor;

uniform sampler2D uTex;
uniform vec2 uRes;

// One knob:
//  0..5   subtle
//  5..20  obvious halo
//  20..50 very glowy / dreamy
uniform float uGlow;

float luminance(vec3 c)
{
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

// Soft bright-pass: returns color that "emits" light.
// Threshold is derived from uGlow so you still have 1 uniform.
vec3 emit(vec3 c, float glow)
{
    float L = luminance(c);

    // More glow => lower threshold => more pixels emit.
    float thr  = mix(1.05, 0.55, clamp(glow * 0.06, 0.0, 1.0)); // ~1.05 .. 0.55
    float knee = 0.35;                                          // softness band

    float t = smoothstep(thr - knee, thr + knee, L);

    // Make emission grow non-linearly with luminance (feels more like bloom)
    // (Keeps mids from glowing too much while highlights blow out nicely.)
    float power = 1.2;
    float e = pow(max(L - (thr - knee), 0.0), power);

    return c * t * (0.6 + 1.4 * e);
}

//TODO: red doesn't glow?
void main()
{
    vec4 base = texture(uTex, vUV);

    vec2 px = 1.0 / max(uRes, vec2(1.0));

    // Radius grows with uGlow (still one knob).
    // This is where halo lives: you need bigger radii than your old 2..4 taps.
    float radiusPx = mix(2.0, 28.0, clamp(uGlow / 30.0, 0.0, 1.0)); // ~2..28 px

    // Step size between taps: smaller -> smoother halo but more expensive look.
    // Kept constant-ish; radius controls spread.
    float stepPx = 2.0;

    // How strong the added halo is.
    float intensity = clamp(uGlow, 0.0, 80.0) * 0.045; // tune here

    // Accumulate wide scattering halo from many samples.
    // 1) sample multiple directions (8-way)
    // 2) step outwards with exponential falloff
    vec3 halo = vec3(0.0);
    float wsum = 0.0;

    // 8 directions (normalized-ish)
    vec2 dirs[8];
    dirs[0] = vec2( 1, 0);
    dirs[1] = vec2(-1, 0);
    dirs[2] = vec2( 0, 1);
    dirs[3] = vec2( 0,-1);
    dirs[4] = normalize(vec2( 1, 1));
    dirs[5] = normalize(vec2(-1, 1));
    dirs[6] = normalize(vec2( 1,-1));
    dirs[7] = normalize(vec2(-1,-1));

    // Convert step to UV
    vec2 stepUV = px * stepPx;

    // Number of steps derived from radius (keeps behavior stable across uRes)
    int steps = int(clamp(radiusPx / stepPx, 1.0, 18.0)); // up to 18*8 = 144 taps

    // Exponential falloff controls foggy feel.
    // Larger k => tighter halo; smaller k => broader haze.
    float k = 0.28;

    // Center emission also contributes (prevents needing tons of taps for obviousness)
    vec3 centerEmit = emit(base.rgb, uGlow);
    halo += centerEmit * 0.22;
    wsum += 0.22;

    for (int d = 0; d < 8; d++)
    {
        for (int i = 1; i <= steps; i++)
        {
            float dist = float(i) * stepPx; // in pixels
            float w = exp(-k * dist);       // haze-like falloff

            vec2 uv = vUV + dirs[d] * stepUV * float(i);
            vec3 c = texture(uTex, uv).rgb;

            halo += emit(c, uGlow) * w;
            wsum += w;
        }
    }

    halo /= max(wsum, 1e-5);

    // Combine:
    // Additive makes it feel like light.
    // A tiny "screen-ish" lift helps it look like it bounces in air.
    vec3 add = halo * intensity;
    vec3 outRgb = base.rgb + add;

    // Optional: slight screen component (still one knob; tied to intensity)
    // Screen: 1 - (1-a)(1-b) -> gives that "bloom lifts the mids" feeling.
    float screenAmt = clamp(uGlow * 0.01, 0.0, 0.25);
    vec3 screenCol = 1.0 - (1.0 - base.rgb) * (1.0 - add);
    outRgb = mix(outRgb, screenCol, screenAmt);

    FragColor = vec4(outRgb, base.a);
}
