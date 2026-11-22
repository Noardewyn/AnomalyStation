// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/body_temp_sensitive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but check_safe_environment's unit-test branch (PERFORM_ALL_TESTS, FALSE in production, rules 1.7) (gaps package C1).

/**
 * When attached to a basic mob, it gives it the ability to be hurt by cold/hot body temperatures
 */
// tg: code/datums/elements/body_temp_sensitive.dm:4
public partial class datum_element_body_temp_sensitive : datum_element
{
    // tg: code/datums/elements/body_temp_sensitive.dm:18
    [DmParams("target", "min_body_temp", "max_body_temp", "cold_damage", "heat_damage", "mapload")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object min_body_temp = Dm.arg(__extra, 0);
        object max_body_temp = Dm.arg(__extra, 1);
        object cold_damage = Dm.arg(__extra, 2);
        object heat_damage = Dm.arg(__extra, 3);
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        if (isnum(min_body_temp))
            this.min_body_temp = Dm.num(min_body_temp);

        if (isnum(max_body_temp))
            this.max_body_temp = Dm.num(max_body_temp);

        if (isnum(cold_damage))
            this.cold_damage = Dm.num(cold_damage);

        if (isnum(heat_damage))
            this.heat_damage = Dm.num(heat_damage);

        RegisterSignal(target, COMSIG_LIVING_LIFE, nameof(on_life));
        return dot;
    }

    // tg: code/datums/elements/body_temp_sensitive.dm:40
    public override object Detach(datum source = null, params object[] __extra)
    {
        if (source != null)
            UnregisterSignal(source, COMSIG_LIVING_LIFE);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/body_temp_sensitive.dm:45
    [SignalHandler]
    public virtual void on_life(datum target = null, object seconds_per_tick = null)
    {
        mob_living living_mob = (mob_living)target;
        bool gave_alert = false;

        if (Dm.num(living_mob.bodytemperature) < min_body_temp && cold_damage > 0 && !HAS_TRAIT(living_mob, TRAIT_RESISTCOLD))
        {
            Waiting.NoSleep(living_mob.adjust_fire_loss(cold_damage * Dm.num(seconds_per_tick), forced: TRUE));
            if (!Dm.truthy(living_mob.has_status_effect(typeof(datum_status_effect_inebriated))))
            {
                if (cold_damage >= 10)
                    living_mob.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_cold), 3);
                else if (cold_damage >= 5)
                    living_mob.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_cold), 2);
                else
                    living_mob.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_cold), 1);
                gave_alert = true;
            }
        }
        else if (Dm.num(living_mob.bodytemperature) > max_body_temp && heat_damage > 0 && !HAS_TRAIT(living_mob, TRAIT_RESISTHEAT))
        {
            Waiting.NoSleep(living_mob.adjust_fire_loss(heat_damage * Dm.num(seconds_per_tick), forced: TRUE));
            if (heat_damage >= 10)
                living_mob.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_hot), 3);
            else if (heat_damage >= 5)
                living_mob.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_hot), 2);
            else
                living_mob.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_hot), 1);
            gave_alert = true;
        }

        if (!gave_alert)
            living_mob.clear_alert(ALERT_TEMPERATURE);
    }

    ///Ensures that maploaded mobs are in a safe environment. Unit test stuff.
    // tg: code/datums/elements/body_temp_sensitive.dm:78
    public virtual void check_safe_environment(mob_living living_mob = null)
    {
        Dm.Deferred("code/datums/elements/body_temp_sensitive.dm:78", "check_safe_environment: a tg unit test's map check (never called in production, PERFORM_ALL_TESTS)");
    }
}
