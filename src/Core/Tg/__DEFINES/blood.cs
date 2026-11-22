// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/blood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only IS_BLOOD_ALWAYS_SYNTHETIC translated (M8 slice A: the carbon's reagent_expose); the constants of this file
// are generated (Defines), its other function-like macros come with their users.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/blood.dm:39  #define IS_BLOOD_ALWAYS_SYNTHETIC(mob) (!ishuman(mob) || HAS_TRAIT(mob, TRAIT_BORN_MONKEY) || HAS_TRAIT(mob, TRAIT_SPAWNED_MOB))
    public static bool IS_BLOOD_ALWAYS_SYNTHETIC(object mob) => (!ishuman(mob) || HAS_TRAIT(mob, TRAIT_BORN_MONKEY) || HAS_TRAIT(mob, TRAIT_SPAWNED_MOB));
}
