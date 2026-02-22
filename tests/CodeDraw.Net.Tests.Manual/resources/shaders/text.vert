#version 450 core

layout(location=0) in vec2 aPosPx;   // LOCAL pixel position (text-space)
layout(location=1) in vec2 aUv;
layout(location=2) in vec4 aColor;

out vec2 vUv;
out vec4 vColor;

uniform vec2 uRes; // layer resolution in px
uniform mat3 uXf;  

void main()
{
    vUv = aUv;
    vColor = aColor;

    // local px -> world/layer px
    vec2 p = (uXf * vec3(aPosPx, 1.0)).xy;

    // world/layer px -> NDC (top-left origin)
    vec2 ndc;
    ndc.x = (p.x / uRes.x) * 2.0 - 1.0;
    ndc.y = 1.0 - (p.y / uRes.y) * 2.0;

    gl_Position = vec4(ndc, 0.0, 1.0);
}