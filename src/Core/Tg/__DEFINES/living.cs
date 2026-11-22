// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/living.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only CAN_HAVE_BLOOD, QUEUE_BLOOD_UPDATE and GET_PHYSIOLOGY translated (M2 P1, the living core and blood), and
// MODIFY_PHYSIOLOGY with its two helper macros (M8 slice B: the coagulant's bleed modifier); the
// constants of this file are generated (Defines), and its other function-like macros come with their users.

public static partial class GlobalProcs
{
    /// Checks if the mob can have blood
    // tg: code/__DEFINES/living.dm:24  #define CAN_HAVE_BLOOD(mob) (mob.living_flags & LIVING_CAN_HAVE_BLOOD)
    public static object CAN_HAVE_BLOOD(object mob) => Dm.bitand(((mob_living)mob).living_flags, LIVING_CAN_HAVE_BLOOD);

    /// Queues a blood update for the next life tick for the mob
    // tg: code/__DEFINES/living.dm:26  #define QUEUE_BLOOD_UPDATE(mob) mob.living_flags |= BLOOD_UPDATE_QUEUED
    public static void QUEUE_BLOOD_UPDATE(object mob) => Dm.combine(ref ((mob_living)mob).living_flags, BLOOD_UPDATE_QUEUED);

    ///get the physiology coefficient for a specific key
    // tg: code/__DEFINES/living.dm:40  #define GET_PHYSIOLOGY(living, key) (LAZYACCESS(living.physiology, key) || DEFAULT_PHYSIOLOGY_VAL)
    public static object GET_PHYSIOLOGY(object living, object key) => (LAZYACCESS(((mob_living)living).physiology, key) is var or__1 && Dm.truthy(or__1) ? or__1 : DEFAULT_PHYSIOLOGY_VAL);

    // MODIFY_PHYSIOLOGY expands _INIT_PHYSIOLOGY_VAL and _CLEAR_PHYSIOLOGY_VAL (:42-51); a statement macro that assigns the
    // target's var, so a GlobalProcs member (rules 11.1; wound_effects.cs writes the same lines out inline)
    // tg: code/__DEFINES/living.dm:53  #define MODIFY_PHYSIOLOGY(living, key, mult)
    public static void MODIFY_PHYSIOLOGY(object living, object key, object mult)
    {
        var target = (mob_living)living;
        LAZYINITLIST(ref target.physiology);
        if (target.physiology[key] == null)
            target.physiology[key] = DEFAULT_PHYSIOLOGY_VAL;
        target.physiology[key] = Dm.mul(target.physiology[key], mult);
        if (round(Dm.num(target.physiology[key]), 0.001f) == DEFAULT_PHYSIOLOGY_VAL)
            LAZYREMOVE(ref target.physiology, key);
    }
}
