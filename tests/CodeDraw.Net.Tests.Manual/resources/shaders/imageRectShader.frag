#version 450 core

in vec2 vUv;
out vec4 oColor;

uniform sampler2D uTex;

// destination rectangle in *pixels*: (x,y,w,h)
uniform vec4 uDstRectPx;
// destination resolution in *pixels*: (W,H)
uniform vec2 uDstResPx;

// source UV rect inside texture: (u0,v0,u1,v1) in 0..1
uniform vec4 uSrcUvRect;

void main()
{
    // Convert current fragment uv into pixel coords in destination.
    // vUv is 0..1 across the full target.
    vec2 px = vec2(vUv.x, 1.0 - vUv.y) * uDstResPx;

    // Check if we're inside dst rect
    vec2 p0 = uDstRectPx.xy;
    vec2 p1 = uDstRectPx.xy + uDstRectPx.zw;

    if (px.x < p0.x || px.y < p0.y || px.x >= p1.x || px.y >= p1.y)
    {
        // outside: transparent (lets layer blending decide)
        oColor = vec4(0.0);
        return;
    }

    // local uv in rect 0..1
    vec2 luv = (px - p0) / max(uDstRectPx.zw, vec2(1.0));

    // map into src UV rect
    vec2 suv = mix(uSrcUvRect.xy, uSrcUvRect.zw, luv);

    oColor = texture(uTex, suv);
}