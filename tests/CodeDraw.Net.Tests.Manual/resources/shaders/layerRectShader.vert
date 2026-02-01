#version 330 core
layout(location=0) in vec2 aPos; // full-screen quad: [-1..1]
layout(location=1) in vec2 aUv;  // [0..1]

uniform vec4 uDstRectPx;   // x,y,w,h in pixels (dest)
uniform vec2 uDstResPx;    // dest canvas size in pixels
uniform vec4 uSrcUvRect;   // x,y,w,h in UV space (source)

out vec2 vUv;

void main()
{
    // Convert pixel rect to NDC
    vec2 dstMinPx = uDstRectPx.xy;
    vec2 dstMaxPx = uDstRectPx.xy + uDstRectPx.zw;

    vec2 ndcMin = vec2(
    (dstMinPx.x / uDstResPx.x) * 2.0 - 1.0,
    1.0 - (dstMinPx.y / uDstResPx.y) * 2.0
    );
    vec2 ndcMax = vec2(
    (dstMaxPx.x / uDstResPx.x) * 2.0 - 1.0,
    1.0 - (dstMaxPx.y / uDstResPx.y) * 2.0
    );

    // Map the full-screen quad pos (-1..1) into our dst rect in NDC.
    // aPos.x=-1 => ndcMin.x, aPos.x=+1 => ndcMax.x (same for y)
    vec2 t = vec2(aUv.x, 1.0 - aUv.y); // make t.y = 0 mean "top"
    vec2 ndc = mix(ndcMin, ndcMax, t);

    gl_Position = vec4(ndc, 0.0, 1.0);

    // UV: map quad uv into source uv rect
    vUv = uSrcUvRect.xy + aUv * uSrcUvRect.zw;
}