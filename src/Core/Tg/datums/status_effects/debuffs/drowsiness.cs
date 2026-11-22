// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/drowsiness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice B: the whole file (morphine, tirimol and probital's overdose, a failing liver make a body drowsy; its eye blur is the
// view's blur on the client, protocol 29). The var block is generated.

// tg: code/datums/status_effects/debuffs/drowsiness.dm:1
public partial class datum_status_effect_drowsiness : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/drowsiness.dm:7
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/drowsiness.dm:11
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (HAS_TRAIT(owner, TRAIT_SLEEPIMMUNE) || !Dm.truthy(Dm.bitand(owner.status_flags, CANUNCONSCIOUS)))
            return FALSE;
        // Do robots dream of electric sheep?
        if (issilicon(owner))
            return FALSE;

        RegisterSignal(owner, COMSIG_COMPONENT_CLEAN_FACE_ACT, nameof(on_face_clean));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/drowsiness.dm:21
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_COMPONENT_CLEAN_FACE_ACT);
        return null;
    }

    /// Signal proc for [COMSIG_COMPONENT_CLEAN_FACE_ACT]. When we wash our face, reduce drowsiness by a bit.
    // tg: code/datums/status_effects/debuffs/drowsiness.dm:25
    [SignalHandler]
    public virtual void on_face_clean(datum source = null)
    {
        remove_duration(rand(4 * SECONDS, 6 * SECONDS));
    }

    // tg: code/datums/status_effects/debuffs/drowsiness.dm:30
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        // You do not feel drowsy while unconscious or in stasis
        if (IS_UNCONSCIOUS(owner) || HAS_TRAIT(owner, TRAIT_STASIS))
            return null;

        // Resting helps against drowsiness
        // While resting, we lose 4 seconds of duration (2 additional ticks) per tick
        if (Dm.truthy(owner.resting) && Dm.truthy(remove_duration(2 * Dm.num(seconds_between_ticks))))
            return null;

        await owner.set_eye_blur_if_lower(4 * SECONDS);
        if (prob(5))
            await owner.AdjustSleeping(10 * SECONDS);
        return null;
    }
}
