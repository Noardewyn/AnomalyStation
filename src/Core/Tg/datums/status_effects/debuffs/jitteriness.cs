// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/jitteriness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice E: the whole file (a defibrillator's shock leaves the body jittering; the M7 shocks slice translated
// do_jitter_animation). The var block of /datum/status_effect/jitter is generated; its mood event is live since gaps MS1.

// tg: code/datums/status_effects/debuffs/jitteriness.dm:1
public partial class datum_status_effect_jitter : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/jitteriness.dm:7
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/jitteriness.dm:11
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        // If we're being applied to a dead person, don't make the status effect.
        // Just do a bit of jitter animation and be done.
        if (owner.stat == DEAD)
        {
            owner.do_jitter_animation(Dm.num(duration) / 10);
            return FALSE;
        }

        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(remove_jitter));
        await owner.add_mood_event(id, typeof(datum_mood_event_jittery));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/jitteriness.dm:22
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);
        owner.clear_mood_event(id);
        owner.update_offsets();
        return null;
    }

    // tg: code/datums/status_effects/debuffs/jitteriness.dm:27
    public override object get_examine_text(mob examiner = null)
    {
        float d = Dm.num(duration);
        if (d >= 5 * MINUTES)
            return span_boldwarning(I18n.T("[] [] convulsing violently!", owner.p_They(), owner.p_are()));
        if (d >= 3 * MINUTES)
            return span_warning(I18n.T("[] [] extremely jittery.", owner.p_They(), owner.p_are()));
        if (d >= 1 * MINUTES)
            return span_warning(I18n.T("[] [] twitching ever so slightly.", owner.p_They(), owner.p_are()));

        return null;
    }

    /// Removes all of our jitteriness on a signal
    // tg: code/datums/status_effects/debuffs/jitteriness.dm:39
    [SignalHandler]
    public virtual void remove_jitter(datum source = null)
    {
        qdel(this);
    }

    // tg: code/datums/status_effects/debuffs/jitteriness.dm:44
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        // Resting helps against jitter
        // While resting, we lose 8 seconds of duration (4 additional ticks) per tick
        if (Dm.truthy(owner.resting) && Dm.truthy(remove_duration(4 * Dm.num(seconds_between_ticks))))
            return null;

        float time_left_in_seconds = Dm.num(duration) / 10;
        owner.do_jitter_animation(time_left_in_seconds);
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/jitteriness.dm:55  (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    /// Helper proc that causes the mob to do a jittering animation by jitter_amount.
    /// jitter_amount will only apply up to 300 (maximum jitter effect).
    // tg: code/datums/status_effects/debuffs/jitteriness.dm:55
    public virtual void do_jitter_animation(float? jitter_amount = null)
    {
        jitter_amount ??= 100;
        float amplitude = min(4, (Dm.num(jitter_amount) / 100) + 1);
        float pixel_w_diff = rand(-amplitude, amplitude);
        float pixel_z_diff = rand(-amplitude / 3, amplitude / 3);
        animate(this, Dm.named("pixel_w", pixel_w_diff), Dm.named("pixel_z", pixel_z_diff), Dm.named("time", 0.2f * SECONDS), Dm.named("loop", 6), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        animate(Dm.named("pixel_w", -pixel_w_diff), Dm.named("pixel_z", -pixel_z_diff), Dm.named("time", 0.2f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
    }
}
