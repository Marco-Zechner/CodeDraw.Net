#version 330 core

layout(location=0) in vec2 aPosPx;   // pixel position, top-left origin
layout(location=1) in vec2 aUv;
layout(location=2) in vec4 aColor;

out vec2 vUv;
out vec4 vColor;

uniform vec2 uRes; // layer resolution in px

void main()
{
    vUv = aUv;
    vColor = aColor;

    // px -> NDC, top-left origin
    vec2 ndc;
    ndc.x = (aPosPx.x / uRes.x) * 2.0 - 1.0;
    ndc.y = 1.0 - (aPosPx.y / uRes.y) * 2.0;

    gl_Position = vec4(ndc, 0.0, 1.0);
}
