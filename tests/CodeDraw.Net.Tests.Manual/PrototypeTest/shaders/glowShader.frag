#version 330 core

in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uTex;

uniform vec2  uGlowPos;      // NOW: pixel coords (same space as uResolution)
uniform float uRadius;       // NOW: pixels
uniform float uIntensity;
uniform vec3  uGlowColor;

uniform vec2 uResolution;    // pixels

void main()
{
    vec4 base = texture(uTex, vUV);

    // Convert current fragment UV -> pixel position
    vec2 fragPx = vUV * uResolution;

    // Distance in pixels
    float dist = length(fragPx - uGlowPos);

    // Smooth falloff in pixels
    float inner = uRadius * 0.6;
    float glow  = 1.0 - smoothstep(inner, uRadius, dist);

    // Shape it a bit
    glow = pow(glow, 2.0) * uIntensity;

    fragColor = vec4(base.rgb + uGlowColor * glow, base.a);
}
