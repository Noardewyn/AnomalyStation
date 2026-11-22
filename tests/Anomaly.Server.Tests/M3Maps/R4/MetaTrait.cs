using Xunit.Abstractions;
using Xunit.Sdk;

namespace Anomaly.Server.Tests.M3Maps.R4;

/// <summary>
/// The trait <c>Category=Meta</c> of every test that boots the converted MetaStation (<see cref="MetaServerFactAttribute"/>,
/// <see cref="MetaServerTheoryAttribute"/>): they hold most of the suite's time, so the quick run leaves them out
/// (<c>dotnet test --filter Category!=Meta</c>, docs/testing.md) and the full run at the end of a stage keeps them.
/// </summary>
public sealed class MetaTraitDiscoverer : ITraitDiscoverer
{
    public const string TypeName = "Anomaly.Server.Tests.M3Maps.R4.MetaTraitDiscoverer";
    public const string AssemblyName = "Anomaly.Server.Tests";

    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "Meta");
    }
}
