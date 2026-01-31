namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

[Prototype(1)]
public class Prototype1 : ITestable
{
    public void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        using var session = new Prototype1Session(host);

        Console.WriteLine("Prototype1 running. Press ENTER to stop.");
        Console.ReadLine();

        host.Stop();
    }
}
