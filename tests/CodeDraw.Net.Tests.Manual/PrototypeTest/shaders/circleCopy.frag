#version 450 core

in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uTex;

uniform vec2  uGlowPos;   // pixels
uniform float uRadius;    // pixels
uniform float uEdgeSoftness; // pixels, e.g. 10-40
uniform vec2  uResolution;

void main()
{
    vec4 base = texture(uTex, vUV);

    vec2 fragPx = vUV * uResolution;
    float dist = length(fragPx - uGlowPos);

    // soft edge region
    float mask = 1.0 - smoothstep(uRadius - uEdgeSoftness, uRadius, dist);

    // shape the fade so it feels more like blur than linear alpha
    mask = pow(mask, 1.8);

    fragColor = vec4(base.rgb, base.a * mask);
}
