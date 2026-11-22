// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/configuration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The constants of configuration.dm are generated into Defines. Its two function-like macros paste their argument into a type
// path (rules 11.1 (b)), so they have no member and are expanded inline at every use:

//config files
// tg: code/__DEFINES/configuration.dm:2  #define CONFIG_GET(X) global.config.Get(/datum/config_entry/##X)
//     CONFIG_GET(number/fps) is config.Get(typeof(datum_config_entry_number_fps)), an object (Get's slot returns object).
// tg: code/__DEFINES/configuration.dm:3  #define CONFIG_SET(X, Y) global.config.Set(/datum/config_entry/##X, ##Y)
//     CONFIG_SET(number/fps, 30) is config.Set(typeof(datum_config_entry_number_fps), 30), a DmTask<object> (Set's slot
//     suspends): awaited in an async proc, Waiting.NoSleep(...) in a synchronous one (rules 8).
// global.config is the accessor GlobalProcs.config (rules 4.8, 10.6), written bare unless a member named config hides it.
