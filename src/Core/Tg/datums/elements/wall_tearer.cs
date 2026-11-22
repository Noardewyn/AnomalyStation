// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/wall_tearer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Allows attached mobs to destroy walls over time
// tg: code/datums/elements/wall_tearer.dm:12
public partial class datum_element_wall_tearer : datum_element
{
    // tg: code/datums/elements/wall_tearer.dm:2
    private const float WALL_TEAR_ALLOWED = 1;
    // tg: code/datums/elements/wall_tearer.dm:6
    private const float WALL_TEAR_FAIL_CANCEL_CHAIN = -1;

    // tg: code/datums/elements/wall_tearer.dm:24
    [DmParams("target", "allow_reinforced", "tear_time", "reinforced_multiplier", "do_after_key")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        this.allow_reinforced = Dm.arg(__extra, 0) ?? TRUE;
        this.tear_time = Dm.arg(__extra, 1) ?? 2 * SECONDS;
        this.reinforced_multiplier = Dm.arg(__extra, 2) ?? 2f;
        this.do_after_key = Dm.arg(__extra, 3);
        RegisterSignals(target, Dm.list(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, COMSIG_LIVING_UNARMED_ATTACK), nameof(on_attacked_wall));
        return dot;
    }

    // tg: code/datums/elements/wall_tearer.dm:34
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, COMSIG_LIVING_UNARMED_ATTACK));
        return dot;
    }

    /// Try to tear up a wall
    // tg: code/datums/elements/wall_tearer.dm:39
    [SignalHandler]
    public virtual object on_attacked_wall(mob_living tearer = null, atom target = null, object proximity_flag = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(tearer, target)) || (do_after_key != null && Dm.truthy(DOING_INTERACTION(tearer, do_after_key))))
        {
            tearer.balloon_alert(tearer, "busy!");
            return COMPONENT_HOSTILE_NO_ATTACK;
        }
        float is_valid = validate_target(target, tearer);
        if (is_valid != WALL_TEAR_ALLOWED)
            return is_valid == WALL_TEAR_FAIL_CANCEL_CHAIN ? COMPONENT_HOSTILE_NO_ATTACK : NONE;
        Waiting.Detach(rip_and_tear(tearer, target)); // INVOKE_ASYNC
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/datums/elements/wall_tearer.dm:50
    public virtual async DmTask rip_and_tear(mob_living tearer = null, atom target = null)
    {
        // We need to do this three times to actually destroy it
        float rip_time = (istype(target, typeof(turf_closed_wall_r_wall)) ? Dm.num(tear_time) * Dm.num(reinforced_multiplier) : Dm.num(tear_time)) / 3;
        if (rip_time > 0)
        {
            tearer.visible_message(span_warning(Dm.text("[] begins tearing through []!", tearer, target)));
            playsound(tearer, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 100, vary: TRUE);
            target.balloon_alert(tearer, "tearing...");
            if (!Dm.truthy(await do_after(tearer, rip_time, target, interaction_key: do_after_key)))
            {
                tearer.balloon_alert(tearer, "interrupted!");
                return;
            }
        }
        // Might have been replaced, removed, or reinforced during our do_after
        float is_valid = validate_target(target, tearer);
        if (is_valid != WALL_TEAR_ALLOWED)
            return;
        await tearer.do_attack_animation(target);
        await target.AddComponent(typeof(datum_component_torn_wall));
        is_valid = validate_target(target, tearer); // And now we might have just destroyed it
        if (is_valid == WALL_TEAR_ALLOWED)
            await tearer.UnarmedAttack(target, TRUE);
    }

    /// Check if the target atom is a wall we can actually rip up
    // tg: code/datums/elements/wall_tearer.dm:71
    public virtual float validate_target(atom target = null, mob_living tearer = null)
    {
        if (!isclosedturf(target) || isindestructiblewall(target))
            return 0; // WALL_TEAR_INVALID

        bool reinforced = istype(target, typeof(turf_closed_wall_r_wall));
        if (!Dm.truthy(allow_reinforced) && reinforced)
        {
            target.balloon_alert(tearer, "it's too strong!");
            return WALL_TEAR_FAIL_CANCEL_CHAIN;
        }
        return WALL_TEAR_ALLOWED;
    }
}
