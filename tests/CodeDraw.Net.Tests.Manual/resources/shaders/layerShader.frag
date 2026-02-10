#version 330 core
in vec2 vUV;
out vec4 FragColor;
uniform sampler2D uTex;
uniform int uForceOpaque; // 0/1
void main(){
    vec4 c = texture(uTex, vUV);
    if (uForceOpaque == 1) c.a = 1.0;
    FragColor = c;
}