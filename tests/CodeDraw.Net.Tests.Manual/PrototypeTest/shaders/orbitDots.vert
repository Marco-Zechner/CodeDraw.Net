#version 450
layout(location=0) in vec2 aPos; // fullscreen quad pos (-1..1)
layout(location=1) in vec2 aUv;  // uv (0..1)

layout(location=0) out vec2 vUv;

void main() {
    vUv = aUv;
    gl_Position = vec4(aPos, 0.0, 1.0);
}
