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

        win.Open();

        Assert.Multiple(() =>
        {
            Assert.That(win.Size, Is.EqualTo(new Vector2<int>(200, 300)));
            Assert.That(win.IsOpen, Is.True);
            Assert.That(win.IsClosed, Is.False);
        });

        win.Close();
        win.WaitForClose();
        Assert.Multiple(() =>
        {
            Assert.That(win.IsOpen, Is.False);
            Assert.That(win.IsClosed, Is.True);
        });
    }

    [Test]
    public void CodeDrawWindow_whenConstructedNew_shouldExistWithDefaultSize()
    {
        var win = new CodeDrawWindow("TestWindow");

        Assert.That(win, Is.Not.Null);

        win.Open();

        Assert.Multiple(() =>
        {
            Assert.That(win.Size, Is.EqualTo(new Vector2<int>(1280, 720)));
            Assert.That(win.IsOpen, Is.True);
            Assert.That(win.IsClosed, Is.False);
        });

        win.Close();
        win.WaitForClose();
        Assert.Multiple(() =>
        {
            Assert.That(win.IsOpen, Is.False);
            Assert.That(win.IsClosed, Is.True);
        });
    }
}
