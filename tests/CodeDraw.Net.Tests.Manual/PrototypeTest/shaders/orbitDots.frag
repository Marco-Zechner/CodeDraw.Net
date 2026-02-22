#version 450 core

in vec2 vUv;
out vec4 FragColor;

uniform vec4 uPosSize;  // TODO: throw error if vec2 does not match c# equiavlent type. eg. if i pass in a vec3
uniform float uTime;     // seconds 
uniform vec4  uColor;    // r,g,b,a 
uniform float uRadius1;  // dot radius (px)
uniform float uRadius2;  // orbit radius (px)
uniform float uPeriod;   // seconds per full rotation
uniform float uOffset;   // time offset (seconds)

void main()
{
//    FragColor = vec4(vUv,0.0,1.0);
//    return;
    
    // Pixel position inside rect (top-left origin)
    vec2 rectPx = uPosSize.xy + vUv * uPosSize.zw;

    // Rect center in pixels
    vec2 c = uPosSize.xy + 0.5 * uPosSize.zw;

    // Angular speed (period can be negative to reverse)
    float w = 6.28318530718 / max(abs(uPeriod), 0.0001);
    float ang = (uTime + uOffset) * w * sign(uPeriod);

    vec2 offset = vec2(cos(ang), sin(ang)) * uRadius2;

    vec2 p1 = c + offset;
    vec2 p2 = c - offset;

    float d1 = length(rectPx - p1);
    float d2 = length(rectPx - p2);

    if (d1 > uRadius1 && d2 > uRadius1) discard;

    FragColor = uColor;
}
