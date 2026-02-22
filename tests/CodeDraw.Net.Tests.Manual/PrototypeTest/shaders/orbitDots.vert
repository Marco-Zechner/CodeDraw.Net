#version 450 core
layout(location=0) in vec2 aPos; // quad positions: -1..1
layout(location=1) in vec2 aUv;  // uv: 0..1

out vec2 vUv;

uniform vec4 uPosSize; // x,y,w,h in pixels (top-left origin)
uniform vec2 uRes;     // framebuffer size in pixels (W,H)

void main()
{
    vUv = aUv; // 0..1 inside the rect

    // Convert rect in pixels to NDC corners.
    vec2 minPx = uPosSize.xy;
    vec2 maxPx = uPosSize.xy + uPosSize.zw;

    vec2 ndcMin = vec2(
        (minPx.x / uRes.x) * 2.0 - 1.0,
        1.0 - (minPx.y / uRes.y) * 2.0
    );
    vec2 ndcMax = vec2(
        (maxPx.x / uRes.x) * 2.0 - 1.0,
        1.0 - (maxPx.y / uRes.y) * 2.0
    );

    // Map quad aPos (-1..1) -> t (0..1)
    vec2 t = aPos * 0.5 + 0.5;

    // Interpolate between rect corners in NDC
    vec2 ndc = mix(ndcMin, ndcMax, t);

    gl_Position = vec4(ndc, 0.0, 1.0);
}
