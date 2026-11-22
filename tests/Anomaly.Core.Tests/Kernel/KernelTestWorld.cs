using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>Worlds for kernel tests: the runtime error sink records "Type: message" lines.</summary>
internal static class KernelTestWorld
{
    public static World New(out List<string> errors, ulong seed = 42)
    {
        var errs = new List<string>();
        errors = errs;
        return new World(seed) { RuntimeErrorSink = e => errs.Add(e.GetType().Name + ": " + e.Message) };
    }
}
