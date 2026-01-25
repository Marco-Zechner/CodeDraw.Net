using MarcoZechner.CodeDrawDotNet.Api;
using MarcoZechner.MathDotNet;

namespace CodeDraw.Net.Tests.Unit;

public class WindowBasicTests
{
    [Test]
    public void CodeDrawWindow_whenConstructedNewWithSize_shouldExistWithSize()
    {
        var win = new CodeDrawWindow("TestWindow")
        {
            Size = new Vector2<int>(200, 300)
        };

        Assert.That(win, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(win.Size, Is.EqualTo(new Vector2<int>(200, 300)));
            Assert.That(win.IsOpen, Is.True);
            Assert.That(win.IsClosed, Is.False);
        });
    }

    [Test]
    public void CodeDrawWindow_whenConstructedNew_shouldExistWithDefaultSize()
    {
        var win = new CodeDrawWindow("TestWindow");

        Assert.That(win, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(win.Size, Is.EqualTo(new Vector2<int>(0, 0)));
            Assert.That(win.IsOpen, Is.True);
            Assert.That(win.IsClosed, Is.False);
        });
    }

    [Test]
    public void CodeDrawWindow_whenResizable_shouldTriggerOnResize()
    {
        var win = new CodeDrawWindow("TestWindow")
        {
            Size = new Vector2<int>(200, 200),
        };
        bool windowSizeChanged = false;
        bool framebufferSizeChanged = false;
        win.WindowSizeChanged += (_, _) => windowSizeChanged = true;
        win.FramebufferSizeChanged += (_, _) => framebufferSizeChanged = true;

        win.Size = new Vector2<int>(400, 400);

        Assert.That(win, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(windowSizeChanged, Is.True);;
            Assert.That(framebufferSizeChanged, Is.True);;
            Assert.That(win.IsOpen, Is.True);
            Assert.That(win.IsClosed, Is.False);
        });
    }
}
