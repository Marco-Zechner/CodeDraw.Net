img import
gif & video import

gif.DrawFrame(i);

layer.StartGifRender();
layer.StopGifRender();
layer.ExportGif("output.gif");

layer.Export(output.png);

draw simply shapes like line, rectangle, circle, ellipse, arc, triangle, polygon, bezier curve
via a shader


import Bitmap into layer and export layer as Bitmap
add a "SetPixel" method to layer to set a single pixel color
add a "GetPixel" method to layer to get a single pixel color


Welcome to CodeDraw.Net program.
A wall of lightgray characters randomly changing with a clock ticking sound. (darkgray background)
Slowly in the center character for character the text "CodeDraw.Net" appears. those colors become a white color.
We apply a postprocessing shader to give a glowing effect to the text-wall around the cursor position.
Once CodeDraw.Net is fully visible the other characters stop changing. only when the user moves the cursor then they change again while he moves it.

---

TODO:
add setting to layer to follow a window resize "layer.FollowWindowSize(window); //or null for none"
the "auto-layer" of a window will have this set by default to follow. if the layer is once manually resized or another window is also set to draw it, then this setting will reset to none.

win1 = new Window(); //auto-layer follows
win2 = new Window();
win2.SetPresentedLayer(win1.Layer); //auto-layer won't change anymore on resize.
win1.Layer.FollowWindowSize(win2); //auto-layer will now follow win2 size changes
win2.Layer.RequestLayerSize(100, 100); //auto-layer won't change anymore on resize. (win2.Layer is the same as win1.Layer, since both present the same layer)


TODO: add "Analogous, Complementary, Triadic, Tetradic" to the color lib
i have (RGB, HSV, CMYK)
check if i want to add more like:
- HSL (Hue, Saturation, Lightness)
- HWB (Hue, Whiteness, Blackness)
- RGBW (Red, Green, Blue, White)
- CMY (Cyan, Magenta, Yellow)
- YUV (Luma, Chroma U, Chroma V)
look into Matrix5x5 for colors? what use do these have.
look into RGB linear and sRGB conversions

BUG: files are constantly being read

---

