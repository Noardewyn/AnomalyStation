// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/explosions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 1 (explosions, D149): the file's one function-like macro, EX_ACT (a GlobalProcs member, rules 11.1); its constants
// (EXPLODE_*, the gibtonite states, the EXARG_KEY_* keys, the explodable component's and the grenade's flags) are generated
// into Defines.

public static partial class GlobalProcs
{
    /// A wrapper for [/atom/proc/ex_act] to ensure that the explosion propagation and attendant signal are always handled.
    // tg: code/__DEFINES/explosions.dm:26  #define EX_ACT(target, args...)
    //     (a statement; target is evaluated once, which is what tg intends, rules 11.1; args... are ex_act's own: severity,
    //     target and, from a spacevine, origin)
    public static async DmTask EX_ACT(atom target, params object[] args)
    {
        if (!Dm.truthy(Dm.bitand(target.flags_1, PREVENT_CONTENTS_EXPLOSION_1)))
        {
            // DEVIATION(DV27): the interaction guard (D132) on the type's own contents_explosion: a type whose tg
            // contents_explosion is not translated (an undeclared override) explodes none of its contents, where the
            // nearest translated ancestor's would explode another type's.
            if (!InertSeam.MissingOverride(target, "contents_explosion"))
                await target.contents_explosion(Dm.arg(args, 0), Dm.arg(args, 1));
        }
        // SEND_SIGNAL(target, sigtype, ##args): list(target, ##args)
        DmList signal_args = Dm.list(target);
        foreach (object arg in args)
            signal_args.Add(arg);
        if (!(HAS_TRAIT(target, TRAIT_BOMBIMMUNE)) && !Dm.truthy(Dm.bitand((!Dm.truthy(target._listen_lookup?[COMSIG_ATOM_PRE_EX_ACT]) ? NONE : target._SendSignal(COMSIG_ATOM_PRE_EX_ACT, signal_args.Copy())), COMPONENT_CANCEL_EX_ACT)))
        {
            if (Dm.truthy(target._listen_lookup?[COMSIG_ATOM_EX_ACT]))
                target._SendSignal(COMSIG_ATOM_EX_ACT, signal_args.Copy());
            // DEVIATION(DV27): the interaction guard (D132) on the type's own ex_act: a type whose tg ex_act is not
            // translated (an undeclared override: a mob's, an inert machine's) is left as it is, where the nearest
            // translated ancestor's (/obj/ex_act's damage) would give it another type's behaviour.
            if (!InertSeam.MissingOverride(target, "ex_act"))
                target.ex_act(Dm.arg(args, 0), Dm.arg(args, 1), args.Length > 2 ? args[2..] : Array.Empty<object>());
        }
    }
}
