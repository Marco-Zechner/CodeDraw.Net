#version 450 core
layout(location=0) in vec2 aPos; // can ignore, keep for quad
layout(location=1) in vec2 aUV;

uniform vec4 uPosSize; // x,y,w,h in local pixels
uniform vec2 uRes;     // framebuffer size in pixels
uniform mat3 uXf;

void main()
{
    vec2 pLocal = uPosSize.xy + aUV * uPosSize.zw;  // local rect corner
    vec3 pW = uXf * vec3(pLocal, 1.0);              // world/layer position
    vec2 p = pW.xy;

    vec2 ndc = vec2((p.x / uRes.x) * 2.0 - 1.0,
    1.0 - (p.y / uRes.y) * 2.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
}