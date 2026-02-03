#version 450
layout(location=0) in vec2 vUv;
layout(location=0) out vec4 outColor;

uniform vec4  uPosSize;  // x,y,w,h in pixels (built-in)
uniform vec2  uRes;      // target res in pixels (built-in)
uniform float uTime;     // seconds (built-in)
uniform vec4  uColor;    // r,g,b,a (built-in)

uniform float uRadius1;  // dot radius (px)
uniform float uRadius2;  // orbit radius (px)
uniform float uPeriod;   // seconds per full rotation
uniform float uOffset;   // time offset (seconds)

void main()
{
    // Pixel position of this fragment inside the *rect* (not full screen)
    vec2 rectPx = uPosSize.xy + vUv * uPosSize.zw;

    // Rect center in pixels
    vec2 c = uPosSize.xy + 0.5 * uPosSize.zw;

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
