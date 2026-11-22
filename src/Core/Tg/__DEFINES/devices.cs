// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/devices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice E: the PDA messenger): STRINGIFY_PDA_TARGET; the constants of devices.dm are generated into Defines.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/devices.dm:2  #define STRINGIFY_PDA_TARGET(name, job) "[name] ([job])"
    public static string STRINGIFY_PDA_TARGET(object name, object job) => Dm.text("[] ([])", name, job);
}
