using System.Text;

namespace Anomaly.Tools.ToyWorld;

/// <summary>
/// Entry point of the toy world runner: <c>dotnet run --project tools/Anomaly.Tools.ToyWorld -- [--seed N] [--twice]</c>.
/// The program itself is <see cref="ToyWorldRunner"/>.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false); // the frames' arrows
        return ToyWorldRunner.Run(args, Console.Out, Console.Error);
    }
}
