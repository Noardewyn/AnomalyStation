// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/rust_g_overrides.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only url_encode translated (REF() needs it); url_decode comes with its first caller.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/rust_g_overrides.dm:2  #define url_encode(text) rustg_url_encode("[text]")
    // rustg_url_encode is rust-g's native url_encode, written in C# in Builtin/rust_g.builtin.cs.
    public static string url_encode(object text) => rustg_url_encode(Dm.text("[]", text));
}
