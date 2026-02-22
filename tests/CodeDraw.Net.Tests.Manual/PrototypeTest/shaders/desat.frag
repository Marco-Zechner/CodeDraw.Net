#version 450 core
in vec2 vUV;
out vec4 FragColor;

uniform sampler2D uTex;
uniform float uAmount = 0.85; // 0..1

void main(){
    vec4 c = texture(uTex, vUV);
    float lum = dot(c.rgb, vec3(0.2126, 0.7152, 0.0722));
    vec3 gray = vec3(lum);
    vec3 rgb = mix(c.rgb, gray, uAmount);
    FragColor = vec4(rgb, c.a);
}