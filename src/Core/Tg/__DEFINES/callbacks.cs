// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/callbacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Every macro of this file is expanded inline or generated; nothing is declared here.
//
// tg: code/__DEFINES/callbacks.dm:1  #define GLOBAL_PROC "some_magic_bullshit": generated into Defines.
// tg: code/__DEFINES/callbacks.dm:3  CALLBACK is new /datum/callback: Dm.@new<datum_callback>(object, proc, args...), named
//     arguments as Dm.named(...) (rules 7.7, 11.3).
// tg: code/__DEFINES/callbacks.dm:6  #define MAKE_SPAWN_ACT_LIKE_WAITFOR -1: generated into Defines.
// tg: code/__DEFINES/callbacks.dm:8  ASYNC is spawn(MAKE_SPAWN_ACT_LIKE_WAITFOR): spawn(-1, async () => { ... }), or
//     Waiting.Detach(Foo(...)) / Waiting.DetachSync(() => Foo(...)) around a single call (rules 8).
// tg: code/__DEFINES/callbacks.dm:10 INVOKE_ASYNC(proc_owner, proc_path, proc_arguments...): Waiting.Detach(owner.x(args))
//     for a DmTask slot, Waiting.DetachSync(() => owner.x(args)) for a synchronous one, Waiting.Detach(Dm.Call(owner, name,
//     args)) for a proc name held in a var; with GLOBAL_PROC as owner, the global proc itself (rules 7.7, 8;
//     waiting-model.md section 6).
// tg: code/__DEFINES/callbacks.dm:24 VERB_CALLBACK is new /datum/callback/verb_callback: Dm.@new<datum_callback_verb_callback>(...)
//     (verbs are translated with the command layer, rules 7.10).
