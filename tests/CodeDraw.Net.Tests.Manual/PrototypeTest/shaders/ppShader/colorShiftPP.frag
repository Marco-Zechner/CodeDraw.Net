#version 450

in vec2 vUV;
out vec4 FragColor;

// Engine-built-ins provided by PostProcess():
uniform sampler2D uTex;
uniform vec2 uRes;

uniform float uTime;

vec3 ToHSV(vec3 rgb){
    float cMax = max(rgb.r, max(rgb.g, rgb.b));
    float cMin = min(rgb.r, min(rgb.g, rgb.b));
    float delta = cMax - cMin;

    float h = 0.0;
    if (delta > 0.0) {
        if (cMax == rgb.r) {
            h = mod((rgb.g - rgb.b) / delta, 6.0);
        } else if (cMax == rgb.g) {
            h = ((rgb.b - rgb.r) / delta) + 2.0;
        } else {
            h = ((rgb.r - rgb.g) / delta) + 4.0;
        }
        h *= 60.0;
    }

    float s = (cMax == 0.0) ? 0.0 : (delta / cMax);
    float v = cMax;

    return vec3(h, s, v);
}

vec3 ToRGB(vec3 hsv){
    float c = hsv.z * hsv.y;
    float x = c * (1.0 - abs(mod(hsv.x / 60.0, 2.0) - 1.0));
    float m = hsv.z - c;

    vec3 rgbPrime;
    if (hsv.x < 60.0) {
        rgbPrime = vec3(c, x, 0.0);
    } else if (hsv.x < 120.0) {
        rgbPrime = vec3(x, c, 0.0);
    } else if (hsv.x < 180.0) {
        rgbPrime = vec3(0.0, c, x);
    } else if (hsv.x < 240.0) {
        rgbPrime = vec3(0.0, x, c);
    } else if (hsv.x < 300.0) {
        rgbPrime = vec3(x, 0.0, c);
    } else {
        rgbPrime = vec3(c, 0.0, x);
    }

    return rgbPrime + vec3(m);
}

void main(){
    vec4 color = texture(uTex, vUV);
    vec3 hsv = ToHSV(color.rgb);
    hsv.x = mod(hsv.x + uTime * 60.0, 360.0);
    vec3 rgb = ToRGB(hsv);
    FragColor = vec4(rgb, color.a);
}