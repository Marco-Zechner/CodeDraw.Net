img import
gif & video import

gif.DrawFrame(i);

layer.StartGifRender();
layer.StopGifRender();
layer.ExportGif("output.gif");

layer.Export(output.png);

draw simply shapes like line, rectangle, circle, ellipse, arc, triangle, polygon, bezier curve
via a shader

draw text via a shader (with custom font support)

rerender current frame with a custom shader (ie. render current frame up to that point, pass it into the shader
so the user can do something with it, if he wants, and then draw it over the current frame)