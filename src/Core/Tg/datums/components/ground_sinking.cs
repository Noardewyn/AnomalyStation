// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ground_sinking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// A basic mob with this component will sink into the ground, once sinked it will regenerate and might gain damage resistance.
// tg: code/datums/components/ground_sinking.dm:9
public partial class datum_component_ground_sinking : datum_component
{
    // tg: code/datums/components/ground_sinking.dm:1
    private const string REGENERATION_FILTER = "healing_glow";

    // tg: code/datums/components/ground_sinking.dm:32
    [DmParams("target_icon_state", "ground_sinking_delay", "sink_speed", "heal_when_sinked", "health_per_second", "outline_colour", "sink_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isbasicmob(parent))
            return COMPONENT_INCOMPATIBLE;

        this.target_icon_state = Dm.arg(__extra, 0);
        this.ground_sinking_delay = Dm.arg(__extra, 1) ?? 8 * SECONDS;
        this.sink_speed = Dm.arg(__extra, 2) ?? 1 * SECONDS;
        this.heal_when_sinked = Dm.arg(__extra, 3) ?? TRUE;
        this.health_per_second = Dm.arg(__extra, 4) ?? 1f;
        this.outline_colour = Dm.arg(__extra, 5) ?? COLOR_PALE_GREEN;
        this.sink_callback = Dm.arg(__extra, 6) as datum_callback;
        return null;
    }

    // tg: code/datums/components/ground_sinking.dm:52
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_moved));
    }

    // tg: code/datums/components/ground_sinking.dm:55
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (Dm.truthy(sinked) || Dm.truthy(is_sinking))
            unsink();
        if (ground_sinking_start_timer != null)
            deltimer(ground_sinking_start_timer);
        UnregisterSignal(parent, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/datums/components/ground_sinking.dm:62
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(sinked) || Dm.truthy(is_sinking))
            unsink();
        object dot = base.Destroy(force);
        if (ground_sinking_start_timer != null)
            deltimer(ground_sinking_start_timer);
        return dot;
    }

    /// When you move, reset the cooldown and start processing
    // tg: code/datums/components/ground_sinking.dm:70
    [SignalHandler]
    public virtual void on_moved(mob_living_basic living_target = null, atom OldLoc = null, object Dir = null, object Forced = null)
    {
        if (Dm.truthy(sinked) || Dm.truthy(is_sinking))
            unsink();
        if (ground_sinking_start_timer != null)
            deltimer(ground_sinking_start_timer);
        ground_sinking_start_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(start_sinking), living_target), Dm.num(ground_sinking_delay), TIMER_STOPPABLE, file: "code/datums/components/ground_sinking.dm", line: 76);
    }

    /// Start processing health regeneration, and show animation if provided
    // tg: code/datums/components/ground_sinking.dm:79
    public virtual void start_sinking(mob_living_basic living_target = null)
    {
        if (!Dm.truthy(sinked) || Dm.truthy(is_sinking))
        {
            is_sinking = TRUE;
            Waiting.Detach(sinking_progress(living_target)); // INVOKE_ASYNC
        }
    }

    /// Makes the mob try to sink three times. Unsinks if interrupted.
    // tg: code/datums/components/ground_sinking.dm:85
    public virtual async DmTask sinking_progress(mob_living_basic living_target = null)
    {
        living_target.visible_message(span_notice(Dm.text("[] starts sinking into the ground!", living_target)));
        for (int i = 1; i <= 3; i++)
        {
            if (QDELETED(living_target))
                return;
            if (!Dm.truthy(await do_after(living_target, Dm.num(sink_speed), living_target)))
            {
                unsink();
                return;
            }
            sink_count += 1;
            living_target.icon_state = Dm.text("[]_burried_[]", target_icon_state, sink_count);
        }
        sink_count = 0;
        finish_sinking(living_target);
    }

    /// The mob has fully sunk, updates its regeneration, damage resistance and density
    // tg: code/datums/components/ground_sinking.dm:99
    public virtual void finish_sinking(mob_living_basic living_target = null)
    {
        sinked = TRUE;
        is_sinking = FALSE;
        living_target.set_density(FALSE);
        if (Dm.truthy(heal_when_sinked))
            start_regenerating();
        if (sink_callback != null)
            Waiting.NoSleep(sink_callback.Invoke(sinked));
    }

    /// The mob pops out of the ground
    // tg: code/datums/components/ground_sinking.dm:108
    public virtual void unsink()
    {
        mob_living_basic living_target = parent as mob_living_basic;
        if (QDELETED(parent))
            return;
        if (Dm.truthy(sinked) && Dm.truthy(heal_when_sinked))
            stop_regenerating();
        living_target.icon_state = target_icon_state as string;
        living_target.set_density(TRUE);
        sinked = FALSE;
        if (sink_callback != null)
            Waiting.NoSleep(sink_callback.Invoke(sinked));
    }

    /// The mop starts regaining health
    // tg: code/datums/components/ground_sinking.dm:120
    public virtual void start_regenerating()
    {
        mob_living_basic living_parent = parent as mob_living_basic;
        if (living_parent.stat == DEAD)
            return;
        if (Dm.eq(living_parent.health, living_parent.maxHealth))
            return;
        living_parent.visible_message(span_notice(Dm.text("[]'s wounds begin to knit closed!", living_parent)));
        START_PROCESSING(SSobj, this);
        if (!Dm.truthy(outline_colour))
            return;
        living_parent.add_filter(REGENERATION_FILTER, 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", outline_colour), Dm.kv("alpha", 0f), Dm.kv("size", 1f)));
        object filter = living_parent.get_filter(REGENERATION_FILTER);
        Dm.animate(filter, Dm.named("alpha", 200f), Dm.named("time", 0.5f * SECONDS), Dm.named("loop", -1f));
        Dm.animate(Dm.named("alpha", 0f), Dm.named("time", 0.5f * SECONDS));
    }

    /// Stops regaining health
    // tg: code/datums/components/ground_sinking.dm:136
    public virtual void stop_regenerating()
    {
        STOP_PROCESSING(SSobj, this);
        mob_living_basic living_parent = parent as mob_living_basic;
        object filter = living_parent.get_filter(REGENERATION_FILTER);
        Dm.animate(filter);
        living_parent.remove_filter(REGENERATION_FILTER);
    }

    // tg: code/datums/components/ground_sinking.dm:143
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float spt = Dm.num(seconds_per_tick ?? SSMOBS_DT());
        mob_living_basic living_parent = parent as mob_living_basic;
        if (living_parent.stat == DEAD)
        {
            stop_regenerating();
            return null;
        }
        if (Dm.eq(living_parent.health, living_parent.maxHealth))
        {
            stop_regenerating();
            return null;
        }
        Waiting.NoSleep(living_parent.heal_overall_damage(Dm.num(health_per_second) * spt));
        return null;
    }
}
