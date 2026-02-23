#version 450 core

layout(location=0) in vec2 aPos; // ignored (quad)
layout(location=1) in vec2 aUV;  // 0..1

out vec2 vUv;
out vec2 vWorldPx;

uniform vec4 uPosSize; // x,y,w,h in LOCAL px for the quad (coverage bounds in layer space)
uniform vec2 uRes;     // framebuffer size in px
uniform mat3 uXf;      // current layer transform: local->world/layer px

void main()
{
    vUv = aUV;

    // local quad corner in layer-local space
    vec2 pLocal = uPosSize.xy + aUV * uPosSize.zw;

    // layer transform to world/layer px
    vec2 pWorld = (uXf * vec3(pLocal, 1.0)).xy;
    vWorldPx = pWorld;

    // world/layer px -> NDC (top-left origin)
    vec2 ndc;
    ndc.x = (pWorld.x / uRes.x) * 2.0 - 1.0;
    ndc.y = 1.0 - (pWorld.y / uRes.y) * 2.0;

    gl_Position = vec4(ndc, 0.0, 1.0);
}