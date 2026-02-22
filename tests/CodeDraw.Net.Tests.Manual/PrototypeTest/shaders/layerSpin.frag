#version 450 core

in vec2 v_uv;
out vec4 out_color;

uniform sampler2D uTex;      // source layer texture
uniform vec2      uRes;      // destination resolution in px (same as layer size)
uniform float     uTime;     // seconds since start (or any time)
uniform float     uSpeed;    // radians per second (uniform speed)

void main()
{
    // Rotate around center in UV space
    vec2 center = vec2(0.5, 0.5);
    vec2 p = v_uv - center;

    float a = uTime * uSpeed;
    float c = cos(a);
    float s = sin(a);

    vec2 pr = vec2(
    c * p.x - s * p.y,
    s * p.x + c * p.y
    );

    vec2 uv = pr + center;

    // Optional: discard outside (black). For clamp-to-edge sampling, this isn't needed,
    // but discarding avoids smearing at corners if you don't want that.
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
    {
        out_color = vec4(0.0, 0.0, 0.0, 0.0);
        return;
    }

    out_color = texture(uTex, uv);
}