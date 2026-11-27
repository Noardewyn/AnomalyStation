// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/rust_healing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: standing on rust heals the holder's damage, stamina, stuns, blood and body temperature, and gives baton resistance
// (the bitrunning domains' virtual heretic; the heretic's rust path when the heretic comes). The var block is generated.

/// Buffs and heals the target while standing on rust.
// tg: code/datums/elements/rust_healing.dm:2
public partial class datum_element_rust_healing : datum_element
{
    // tg: code/datums/elements/rust_healing.dm:10
    [DmParams("target", "baton_resistance", "heal_amount", "stamina_heal_amount")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object baton_resistance = Dm.arg(__extra, 0) ?? TRUE;
        object heal_amount = Dm.arg(__extra, 1) ?? 3;
        object stamina_heal_amount = Dm.arg(__extra, 2) ?? 10;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        if (Dm.truthy(baton_resistance))
            RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(target, COMSIG_LIVING_LIFE, nameof(on_life));

        this.heal_amount = heal_amount;
        this.stamina_heal_amount = stamina_heal_amount;
        ADD_TRAIT(target as atom, TRAIT_RUSTIMMUNE, ELEMENT_TRAIT(type));
        return dot;
    }

    // tg: code/datums/elements/rust_healing.dm:23
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_LIVING_LIFE));
        REMOVE_TRAIT(source as atom, TRAIT_RUSTIMMUNE, ELEMENT_TRAIT(type));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_MOVABLE_MOVED].
     *
     * Checks if we should have baton resistance on the new turf.
     */
    // tg: code/datums/elements/rust_healing.dm:33
    [SignalHandler]
    public virtual void on_move(mob source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        if (Dm.truthy(source.is_touching_rust()))
            ADD_TRAIT(source, TRAIT_BATON_RESISTANCE, type);
        else
            REMOVE_TRAIT(source, TRAIT_BATON_RESISTANCE, type);
    }

    /**
     * Signal proc for [COMSIG_LIVING_LIFE].
     *
     * Gradually heals the heretic ([source]) on rust,
     * including baton knockdown and stamina damage.
     */
    // tg: code/datums/elements/rust_healing.dm:47
    [SignalHandler]
    public virtual void on_life(mob_living source = null, object seconds_per_tick = null)
    {
        if (!Dm.truthy(source.is_touching_rust()))
            return;

        // Heals all damage + Stamina
        float need_mob_update = FALSE;
        float delta_time = DELTA_WORLD_TIME(SSmobs) * 0.5f; // SSmobs.wait is 2 secs, so this should be halved.
        need_mob_update += Dm.num(Waiting.NoSleep(source.adjust_brute_loss(-Dm.num(heal_amount) * delta_time, updating_health: FALSE)));
        need_mob_update += Dm.num(Waiting.NoSleep(source.adjust_fire_loss(-Dm.num(heal_amount) * delta_time, updating_health: FALSE)));
        need_mob_update += Dm.num(Waiting.NoSleep(source.adjust_tox_loss(-Dm.num(heal_amount) * delta_time, updating_health: FALSE, forced: TRUE))); // Slimes are people too
        need_mob_update += Dm.num(Waiting.NoSleep(source.adjust_oxy_loss(-Dm.num(heal_amount) / 2 * delta_time, updating_health: FALSE)));
        need_mob_update += Dm.num(Waiting.NoSleep(source.adjust_stamina_loss(-Dm.num(stamina_heal_amount) * delta_time, updating_stamina: FALSE)));
        if (Dm.truthy(need_mob_update))
        {
            Waiting.NoSleep(source.updatehealth());
            Dm.@new<obj_effect_temp_visual_heal>(get_turf(source), COLOR_BROWN);
        }
        // Reduces duration of stuns/etc
        Waiting.NoSleep(source.AdjustAllImmobility((-0.5f * SECONDS) * delta_time));
        // Heals blood loss
        source.adjust_blood_volume(2.5f * delta_time, maximum: BLOOD_VOLUME_NORMAL);
        // Slowly regulates your body temp
        source.adjust_bodytemperature((Dm.num(source.get_body_temp_normal()) - source.bodytemperature) / 5);
    }
}
