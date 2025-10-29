namespace MarcoZechner.CodeDrawDotNet.Engine;

internal unsafe sealed class DefaultWindowRenderer : AbstractWindowRenderer
{
    public DefaultWindowRenderer(Silk.NET.GLFW.WindowHandle* window, string title)
        : base(window, title) { }

    protected override void RunLoop()
    {
        double last = 0;
        var warnThresholdMs = PublicWindow!.LongActionWarnMs;

        while (Running && !Glfw.WindowShouldClose(Window))
        {
            double now = (DateTime.UtcNow - StartUtc).TotalSeconds;
            double dt = now - last; last = now;

            // Dequeue at most one sealed frame
            var executedToken = 0L;
            var hadFrame = TryDequeueFrame(out executedToken, out var batch);

            if (hadFrame)
            {
                // Pass 1
                Glfw.GetFramebufferSize(Window, out var fbW, out var fbH);
                if (fbW > 0 && fbH > 0)
                {
                    foreach (var act in batch!)
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        act.Execute(GL!, Glfw, Window, fbW, fbH);
                        sw.Stop();
                        if (warnThresholdMs > 0 && sw.ElapsedMilliseconds > warnThresholdMs)
                            Console.WriteLine($"[Render Watchdog] {act.GetType().Name} took {sw.ElapsedMilliseconds} ms");
                    }
                }
                Glfw.SwapBuffers(Window);
                Frames++;

                // Pass 2 (prime other buffer)
                Glfw.GetFramebufferSize(Window, out fbW, out fbH);
                if (fbW > 0 && fbH > 0)
                {
                    foreach (var act in batch!)
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        act.Execute(GL!, Glfw, Window, fbW, fbH);
                        sw.Stop();
                        if (warnThresholdMs > 0 && sw.ElapsedMilliseconds > warnThresholdMs)
                            Console.WriteLine($"[Render Watchdog] {act.GetType().Name} took {sw.ElapsedMilliseconds} ms");
                    }
                }
                Glfw.SwapBuffers(Window);
                Frames++;

                SignalPresented(executedToken);
            }
            else
            {
                // No new frame → keep window responsive
                Glfw.SwapBuffers(Window);
                Frames++;
            }

            // Pace
            var target = PublicWindow!.TargetFPS;
            if (target > 0)
            {
                int ms = (int)MathF.Max(0, (int)MathF.Round((float)(1000.0 / target)));
                if (ms > 0) Thread.Sleep(ms);
            }
        }
    }

}
