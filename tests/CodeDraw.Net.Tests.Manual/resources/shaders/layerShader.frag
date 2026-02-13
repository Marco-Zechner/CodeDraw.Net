#version 330 core

in vec2 vUV;
out vec4 out_color;

uniform sampler2D uTex;

uniform int  uPresentMode;
uniform vec2 uWindowSizePx;
uniform vec2 uLayerSizePx;
uniform mat3 uWindowToLayer;
uniform vec4 uBackground;
uniform int  uForceOpaque;

vec4 sample_layer_uv(vec2 uv)
{
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
    return vec4(0.0);
    return texture(uTex, uv);
}

void main()
{
    vec4 bg = uBackground;

    vec2 winPx = vec2(gl_FragCoord.x, uWindowSizePx.y - gl_FragCoord.y);

    vec4 src;
    if (uPresentMode == 0)
    {
        src = texture(uTex, vUV);
    }
    else if (uPresentMode == 2)
    {
        vec2 uv = fract(winPx / uLayerSizePx);
        src = texture(uTex, uv);
    }
    else
    {
        vec3 lp = uWindowToLayer * vec3(winPx, 1.0);
        vec2 uv = lp.xy / uLayerSizePx;
        src = sample_layer_uv(uv);
    }

    vec4 outc = vec4(
    src.rgb * src.a + bg.rgb * (1.0 - src.a),
    src.a + bg.a * (1.0 - src.a)
    );

    if (uForceOpaque != 0)
    outc.a = 1.0;

    out_color = outc;
}