#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUV;
uniform vec4 uPosSize; // x,y,w,h in pixels
uniform vec2 uRes;     // framebuffer size in pixels
void main(){
    vec2 p = uPosSize.xy + aUV * uPosSize.zw;
    vec2 ndc = vec2((p.x / uRes.x) * 2.0 - 1.0, 1.0 - (p.y / uRes.y) * 2.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
}