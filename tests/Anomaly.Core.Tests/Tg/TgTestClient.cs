using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tg;

/// <summary>
/// A test fixture client (rules 15: a fixture type of the port's own, <c>/client/test_m1_client</c>): a <c>/client</c> made
/// with <c>Dm.@new</c> whose <c>New</c> does nothing. In BYOND only the engine makes clients, for a connecting player, and
/// then runs tg's <c>client/New</c> (the M1 client layer, <c>ClientSeam.Connect</c>); tests that only need a client object
/// for a mob's <c>client</c> var (sound tokens, progress bars, the do_after watchers) use this instead (M1 contracts step:
/// tg's <c>client/New</c> is a skeleton until M1 P1 lands, and would attach mobs and log in once it does).
/// </summary>
[DmPath("/client/test_m1_client")]
public class client_test_m1_client : client
{
    public override DmTask<object> New(object TopicData = null) => DmTask<object>.FromResult(null);
}
