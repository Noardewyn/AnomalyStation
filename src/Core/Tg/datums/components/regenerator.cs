// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/regenerator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * # Regenerator component
 *
 * A mob with this component will regenerate its health over time, as long as it has not received damage
 * in the last X seconds. Taking any damage will reset this cooldown.
 */
// tg: code/datums/components/regenerator.dm:9
public partial class datum_component_regenerator : datum_component
{
    private const string REGENERATION_FILTER = "healing_glow"; // tg: code/datums/components/regenerator.dm:1

    // tg: code/datums/components/regenerator.dm:32
    [DmParams("regeneration_delay", "brute_per_second", "burn_per_second", "tox_per_second", "oxy_per_second", "heals_wounds", "ignore_damage_types", "outline_colour", "regen_check")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.regeneration_delay = Dm.arg(__extra, 0) ?? (6 * SECONDS);
        this.brute_per_second = Dm.arg(__extra, 1) ?? 2f;
        this.burn_per_second = Dm.arg(__extra, 2) ?? 0f;
        this.tox_per_second = Dm.arg(__extra, 3) ?? 0f;
        this.oxy_per_second = Dm.arg(__extra, 4) ?? 0f;
        this.heals_wounds = Dm.num(Dm.arg(__extra, 5) ?? FALSE);
        this.ignore_damage_types = Dm.arg(__extra, 6) as DmList ?? Dm.list(STAMINA);
        this.outline_colour = Dm.arg(__extra, 7) ?? COLOR_PALE_GREEN;
        this.regen_check = Dm.arg(__extra, 8) as datum_callback;
        return null;
    }

    // tg: code/datums/components/regenerator.dm:56
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_MOB_APPLY_DAMAGE, nameof(on_take_damage));
    }

    // tg: code/datums/components/regenerator.dm:60
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        if (Dm.truthy(regeneration_start_timer))
            deltimer(regeneration_start_timer);
        UnregisterSignal(parent, COMSIG_MOB_APPLY_DAMAGE);
        stop_regenerating();
    }

    // tg: code/datums/components/regenerator.dm:67
    public override object Destroy(float? force = null)
    {
        stop_regenerating();
        object dot = base.Destroy(force);
        if (Dm.truthy(regeneration_start_timer))
            deltimer(regeneration_start_timer);
        return dot;
    }

    /// When you take damage, reset the cooldown and start processing
    // tg: code/datums/components/regenerator.dm:74
    [SignalHandler]
    public virtual void on_take_damage(datum source = null, object damage = null, object damagetype = null, params object[] __extra)
    {
        if (Dm.@in(damagetype, ignore_damage_types))
            return;

        reset_regeneration_timer();
    }

    /// Starts or restarts the regeneration timer
    // tg: code/datums/components/regenerator.dm:81
    public virtual void reset_regeneration_timer()
    {
        stop_regenerating();
        regeneration_start_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(start_regenerating)), Dm.num(regeneration_delay), Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), file: "code/datums/components/regenerator.dm", line: 83);
    }

    /// Start processing health regeneration, and show animation if provided
    // tg: code/datums/components/regenerator.dm:86
    public virtual void start_regenerating()
    {
        if (!Dm.truthy(should_be_regenning(parent as mob_living)))
            return;
        mob_living living_parent = parent as mob_living;
        living_parent.visible_message(span_notice(Dm.text("[]'s wounds begin to knit closed!", living_parent)));
        START_PROCESSING(SSobj, this);
        regeneration_start_timer = null;
        if (!Dm.truthy(outline_colour))
            return;
        living_parent.add_filter(REGENERATION_FILTER, 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", outline_colour), Dm.kv("alpha", 0f), Dm.kv("size", 1f)));
        object filter = living_parent.get_filter(REGENERATION_FILTER);
        animate(filter, Dm.named("alpha", 200), Dm.named("time", 0.5f * SECONDS), Dm.named("loop", -1));
        animate(Dm.named("alpha", 0), Dm.named("time", 0.5f * SECONDS));
    }

    // tg: code/datums/components/regenerator.dm:100
    public virtual void stop_regenerating()
    {
        STOP_PROCESSING(SSobj, this);
        mob_living living_parent = parent as mob_living;
        object filter = living_parent.get_filter(REGENERATION_FILTER);
        if (filter != null)
            animate(filter);
        living_parent.remove_filter(REGENERATION_FILTER);
    }

    // tg: code/datums/components/regenerator.dm:107
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float spt = Dm.num(seconds_per_tick ?? SSMOBS_DT());
        if (!Dm.truthy(should_be_regenning(parent as mob_living)))
        {
            stop_regenerating();
            return null;
        }

        mob_living living_parent = parent as mob_living;
        float heal_mod = living_parent.stat >= SOFT_CRIT ? 2 : 1; // heal faster if we're dying
        float need_mob_update = 0;
        if (Dm.truthy(brute_per_second))
            need_mob_update += Dm.num(Waiting.NoSleep(living_parent.adjust_brute_loss(-1 * heal_mod * Dm.num(brute_per_second) * spt, updating_health: FALSE)));
        if (Dm.truthy(burn_per_second))
            need_mob_update += Dm.num(Waiting.NoSleep(living_parent.adjust_fire_loss(-1 * heal_mod * Dm.num(burn_per_second) * spt, updating_health: FALSE)));
        if (Dm.truthy(tox_per_second))
            need_mob_update += Dm.num(Waiting.NoSleep(living_parent.adjust_tox_loss(-1 * heal_mod * Dm.num(tox_per_second) * spt, updating_health: FALSE)));
        if (Dm.truthy(oxy_per_second))
            need_mob_update += Dm.num(Waiting.NoSleep(living_parent.adjust_oxy_loss(-1 * heal_mod * Dm.num(oxy_per_second) * spt, updating_health: FALSE)));

        if (Dm.truthy(heals_wounds) && iscarbon(parent))
        {
            mob_living_carbon carbon_parent = living_parent as mob_living_carbon;
            foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(carbon_parent.all_wounds?.Copy()))
            {
                if (SPT_PROB(2 - (Dm.num(iter_wound.severity) / 2), spt))
                {
                    Waiting.NoSleep(iter_wound.remove_wound());
                    need_mob_update++;
                }
            }
        }

        if (need_mob_update != 0)
            Waiting.NoSleep(living_parent.updatehealth());
        return null;
    }

    /// Returns true if this mob should regenerate
    // tg: code/datums/components/regenerator.dm:136
    public virtual object should_be_regenning(mob_living who = null)
    {
        if (who.stat == DEAD)
            return FALSE;
        if (regen_check != null && !Dm.truthy(Waiting.NoSleep(regen_check.Invoke(who))))
        {
            reset_regeneration_timer();
            return FALSE;
        }
        if (Dm.truthy(heals_wounds) && iscarbon(who))
        {
            mob_living_carbon carbon_who = who as mob_living_carbon;
            if (Dm.length(carbon_who.all_wounds) > 0)
                return TRUE;
        }
        if (!Dm.eq(who.health, who.maxHealth))
            return TRUE;
        return FALSE;
    }
}
