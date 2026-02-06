#version 450
layout(location=0) in vec2 vUv;
layout(location=0) out vec4 outColor;

uniform vec4  uPosSize;  // x,y,w,h in pixels (built-in)

uniform vec2 uPos;  // TODO: throw error if vec2 does not match c# equiavlent type. eg. if i pass in a vec3
uniform vec2 uSize;
uniform float uTime;     // seconds
uniform vec4  uColor;    // r,g,b,a 
uniform float uRadius1;  // dot radius (px)
uniform float uRadius2;  // orbit radius (px)
uniform float uPeriod;   // seconds per full rotation
uniform float uOffset;   // time offset (seconds)

void main()
{
    // Pixel position of this fragment inside the *rect* (not full screen)
    vec2 rectPx = uPos.xy + vUv * uSize.xy;

    // Rect center in pixels
    vec2 c = uPos.xy + 0.5 * uSize.xy;

    // Angle over time (period)
    float w = 6.28318530718 / max(uPeriod, 0.0001);
    float ang = (uTime + uOffset) * w;

    vec2 offset = vec2(cos(ang), sin(ang)) * uRadius2;

    // Two dots opposite each other
    vec2 p1 = c + offset;
    vec2 p2 = c - offset;

    float d1 = length(rectPx - p1);
    float d2 = length(rectPx - p2);

    // Hard circles; for smoother edges you can use smoothstep around uRadius1
    float inside = (d1 <= uRadius1 || d2 <= uRadius1) ? 1.0 : 0.0;
    if (inside < 0.5) discard;

    outColor = uColor;
}
