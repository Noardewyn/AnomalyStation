using Xunit.Abstractions;
using Xunit.Sdk;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>
/// The trait <c>Category=Meta</c> of every test that boots the converted MetaStation (<see cref="MetaFactAttribute"/> and the other Meta
/// facts): they hold most of the suite's time, so the quick run leaves them out (<c>dotnet test --filter Category!=Meta</c>,
/// docs/testing.md) and the full run at the end of a stage keeps them.
/// </summary>
public sealed class MetaTraitDiscoverer : ITraitDiscoverer
{
    public const string TypeName = "Anomaly.Core.Tests.Tg.M3Maps.P2.MetaTraitDiscoverer";
    public const string AssemblyName = "Anomaly.Core.Tests";

    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "Meta");
    }
}
