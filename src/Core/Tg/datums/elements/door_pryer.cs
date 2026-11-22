// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/door_pryer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the game logs (gaps package C6).

/// Attached to a basic mob. Causes attacks on doors to attempt to open them.
// tg: code/datums/elements/door_pryer.dm:5
public partial class datum_element_door_pryer : datum_element
{
    // tg: code/datums/elements/door_pryer.dm:13
    [DmParams("target", "pry_time", "interaction_key")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        this.pry_time = Dm.arg(__extra, 0) ?? 10 * SECONDS;
        this.interaction_key = Dm.arg(__extra, 1);
        RegisterSignal(target, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_attack));
        return dot;
    }

    // tg: code/datums/elements/door_pryer.dm:21
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_UNARMED_ATTACK);
        return dot;
    }

    /// If we're targeting an airlock, open it
    // tg: code/datums/elements/door_pryer.dm:26
    [SignalHandler]
    public virtual object on_attack(mob_living_basic attacker = null, atom target = null, object proximity_flag = null)
    {
        if (!Dm.truthy(proximity_flag) || target is not obj_machinery_door_airlock airlock_target)
            return NONE;
        if (!Dm.truthy(airlock_target.density))
            return NONE; // It's already open numbnuts

        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(attacker, target)) || (interaction_key != null && Dm.truthy(DOING_INTERACTION(attacker, interaction_key))))
        {
            attacker.balloon_alert(attacker, "busy!");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (Dm.truthy(attacker.combat_mode))
            return null; // Attack the door

        if (Dm.truthy(airlock_target.locked) || Dm.truthy(airlock_target.welded) || airlock_target.seal != null)
        {
            airlock_target.balloon_alert(attacker, "it's sealed!");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        Waiting.Detach(open_door(attacker, airlock_target)); // INVOKE_ASYNC
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Try opening the door, and if we can't then try forcing it
    // tg: code/datums/elements/door_pryer.dm:50
    public virtual async DmTask open_door(mob_living_basic attacker = null, obj_machinery_door_airlock airlock_target = null)
    {
        if (!Dm.truthy(airlock_target.hasPower()))
        {
            attacker.visible_message(span_warning(Dm.text("[] forces the [] to open.", attacker, airlock_target)));
            await airlock_target.open(FORCING_DOOR_CHECKS, attacker);
            return;
        }

        if (Dm.truthy(await airlock_target.allowed__proc(attacker)))
        {
            await airlock_target.open(DEFAULT_DOOR_CHECKS, attacker);
            return;
        }

        attacker.visible_message(message: span_warning(Dm.text("[] starts forcing the [] open!", attacker, airlock_target)), blind_message: span_hear("You hear a metal screeching sound."));

        playsound(airlock_target, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 100, TRUE);
        airlock_target.balloon_alert(attacker, "prying...");
        if (!Dm.truthy(await do_after(attacker, Dm.num(pry_time), airlock_target)))
        {
            airlock_target.balloon_alert(attacker, "interrupted!");
            return;
        }
        if (Dm.truthy(airlock_target.locked))
            return;
        attacker.visible_message(span_warning(Dm.text("[] forces the [] to open.", attacker, airlock_target)));
        await airlock_target.open(BYPASS_DOOR_CHECKS, attacker);
        await airlock_target.take_damage(AIRLOCK_PRY_DAMAGE, BRUTE, sound_effect: FALSE);
    }
}
