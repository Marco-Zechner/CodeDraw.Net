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