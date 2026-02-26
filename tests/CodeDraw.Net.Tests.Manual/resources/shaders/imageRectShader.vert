#version 450 core

layout(location = 0) in vec2 aPos; // EXPECTED: clip space [-1..1]
layout(location = 1) in vec2 aUv;  // 0..1

out vec2 vUv;

void main()
{
    vUv = aUv;
    gl_Position = vec4(aPos, 0.0, 1.0);
}