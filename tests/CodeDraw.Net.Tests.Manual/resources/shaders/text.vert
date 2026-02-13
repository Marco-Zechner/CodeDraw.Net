#version 330 core

layout(location=0) in vec2 aPos01;        // quad vertices in [0..1]
layout(location=1) in vec4 iPosSize;      // x y w h (pixels)
layout(location=2) in vec4 iUv;           // u0 v0 u1 v1
layout(location=3) in vec4 iColor;        // rgba

uniform vec2 uRes; // (width,height) in pixels

out vec2 vUv;
out vec4 vColor;

void main()
{
    // Compute pixel position of this vertex
    vec2 px = iPosSize.xy + aPos01 * iPosSize.zw;

    // Pixel -> NDC
    vec2 ndc = (px / uRes) * 2.0 - 1.0;

    // Flip Y because your layer coordinates are top-left origin
    ndc.y = -ndc.y;

    gl_Position = vec4(ndc, 0.0, 1.0);

    // Interpolate UV inside glyph rect
    vUv = mix(iUv.xy, iUv.zw, aPos01);
    vColor = iColor;
}
