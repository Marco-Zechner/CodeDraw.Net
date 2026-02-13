#version 330 core

in vec2 vUv;
in vec4 vColor;

uniform sampler2D uAtlas;

out vec4 outColor;

void main()
{
    float a = texture(uAtlas, vUv).r; // because GL_R8 / GL_RED
    outColor = vec4(vColor.rgb, vColor.a * a);
}
