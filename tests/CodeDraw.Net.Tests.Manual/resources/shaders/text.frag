#version 450 core

in vec2 vUv;
in vec4 vColor;

out vec4 outColor;

uniform sampler2D uAtlas;

void main()
{
    float cov = texture(uAtlas, vUv).r;
    outColor = vec4(vColor.rgb, vColor.a * cov);
}
