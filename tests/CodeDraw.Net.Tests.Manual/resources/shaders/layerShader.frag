#version 450 core

in vec2 vUV;
out vec4 out_color;

uniform sampler2D uTex;

uniform int  uPresentMode;
uniform vec2 uWindowSizePx;
uniform vec2 uLayerSizePx;
uniform mat3 uWindowToLayer;
uniform int  uForceOpaque;

vec4 sample_layer_uv(vec2 uv)
{
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
    return vec4(0.0);
    return texture(uTex, vec2(uv.x, 1-uv.y)); //TODO: find is the issue with flipped why is somewhere else, aka if this is a real fix, or just a hack
}

void main()
{
    vec2 winPx = vec2(gl_FragCoord.x, uWindowSizePx.y - gl_FragCoord.y);

    vec4 src;
    if (uPresentMode == 0) // stretch to fill
    {
        src = texture(uTex, vUV);
    }
    else if (uPresentMode == 2) // tile
    {
        vec2 uv = fract(winPx / uLayerSizePx);
        src = texture(uTex, uv);
    }
    else // camera
    {
        vec3 lp = uWindowToLayer * vec3(winPx, 1.0);
        vec2 uv = lp.xy / uLayerSizePx;
        src = sample_layer_uv(uv);
    }

    if (uForceOpaque != 0)
        src.a = 1.0;

    out_color = src;
}