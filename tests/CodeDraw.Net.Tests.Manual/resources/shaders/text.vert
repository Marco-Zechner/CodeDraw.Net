#version 330 core

layout(location=0) in vec2 aPosPx;   // position in pixels
layout(location=1) in vec2 aUv;      // uv
layout(location=2) in vec4 aColor;   // rgba

uniform vec2 uRes;

out vec2 vUv;
out vec4 vColor;

void main()
{
    vec2 ndc = (aPosPx / uRes) * 2.0 - 1.0;
    ndc.y = -ndc.y;
    gl_Position = vec4(ndc, 0.0, 1.0);

    vUv = aUv;
    vColor = aColor;
}
