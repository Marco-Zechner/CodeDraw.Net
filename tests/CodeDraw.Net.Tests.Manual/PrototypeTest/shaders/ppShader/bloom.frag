#version 330 core

in vec2 vUV;
out vec4 FragColor;

uniform sampler2D uTex;
uniform vec2 uRes;

// One knob: 0 = off, higher = stronger glow
uniform float uGlow;

float luminance(vec3 c)
{
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void main()
{
    vec4 base = texture(uTex, vUV);

    // ---- 1) Bright-pass (soft threshold) -----------------------
    // We derive threshold from uGlow so you only need one uniform.
    // More glow -> lower threshold -> more pixels contribute.
    float thr = mix(1.0, 0.55, clamp(uGlow * 0.12, 0.0, 1.0)); // ~1.0 .. 0.55
    float knee = 0.35; // softness of the threshold (constant)

    float L = luminance(base.rgb);

    // "soft threshold": 0 below thr-knee, ramps up around threshold, 1-ish above
    float t = smoothstep(thr - knee, thr + knee, L);

    // Only bright-ish parts contribute (and keep their color)
    vec3 bright = base.rgb * t;

    // ---- 2) Blur bright-pass (small gaussian-ish) ---------------
    vec2 px = 1.0 / uRes;

    // Radius grows with uGlow (still one knob)
    float radius = mix(1.0, 4.0, clamp(uGlow * 0.08, 0.0, 1.0));
    vec2 r = px * radius;

    // 9-tap tent/gaussian hybrid (cheap, decent)
    vec3 blur =
    bright * 0.28 +
    texture(uTex, vUV + vec2( r.x, 0.0)).rgb * 0.12 * t +
    texture(uTex, vUV + vec2(-r.x, 0.0)).rgb * 0.12 * t +
    texture(uTex, vUV + vec2(0.0,  r.y)).rgb * 0.12 * t +
    texture(uTex, vUV + vec2(0.0, -r.y)).rgb * 0.12 * t +
    texture(uTex, vUV + vec2( r.x,  r.y)).rgb * 0.06 * t +
    texture(uTex, vUV + vec2(-r.x,  r.y)).rgb * 0.06 * t +
    texture(uTex, vUV + vec2( r.x, -r.y)).rgb * 0.06 * t +
    texture(uTex, vUV + vec2(-r.x, -r.y)).rgb * 0.06 * t;

    // NOTE: The taps above sample uTex directly; to avoid glowing dark pixels,
    // we multiply by 't' from the center sample. Not perfect, but stable + cheap.
    // If you want higher quality: compute t per tap (costlier).

    // ---- 3) Additive combine -----------------------------------
    // Scale: roughly linear for small values, won’t explode
    float strength = clamp(uGlow, 0.0, 50.0) * 0.06;
    vec3 outRgb = base.rgb + blur * strength;

    FragColor = vec4(outRgb, base.a);
}
