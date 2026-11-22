// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/assassin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/**
 * Can enter stealth mode to become invisible and deal bonus damage on their next attack, an ambush predator.
 */
// tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:4
public partial class mob_living_basic_guardian_assassin : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_GUARDIAN_ASSASSIN_REVEALED, nameof(on_forced_unstealth));
        return dot;
    }

    // Toggle stealth
    // tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:26
    public override async DmTask toggle_modes()
    {
        object stealthed = has_status_effect(typeof(datum_status_effect_guardian_stealth));
        datum_action_cooldown_guardian_toggle_mode_assassin stealth_ability = Dm.locate_type(typeof(datum_action_cooldown_guardian_toggle_mode_assassin), actions) as datum_action_cooldown_guardian_toggle_mode_assassin;
        if (Dm.truthy(stealthed))
        {
            to_chat(this, span_bolddanger("You exit stealth."));
            remove_status_effect(typeof(datum_status_effect_guardian_stealth));
            if (stealth_ability != null)
                await stealth_ability.build_all_button_icons();
            return;
        }
        if (!Dm.truthy(is_deployed()))
        {
            to_chat(this, span_bolddanger("You have to be manifested to enter stealth!"));
            return;
        }
        await apply_status_effect(typeof(datum_status_effect_guardian_stealth));
        if (stealth_ability != null)
            await stealth_ability.build_all_button_icons();
    }

    /// Called when we are removed from stealth involuntarily
    // tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:43
    [SignalHandler]
    public virtual void on_forced_unstealth(mob_living source = null)
    {
        visible_message(span_danger(Dm.text("\\The [] suddenly appears!", this)));
        manifest_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START, inline
        datum_action_cooldown_guardian_toggle_mode_assassin stealth_ability = Dm.locate_type(typeof(datum_action_cooldown_guardian_toggle_mode_assassin), actions) as datum_action_cooldown_guardian_toggle_mode_assassin;
        if (stealth_ability != null)
            stealth_ability.StartCooldownSelf(stealth_cooldown_time);
    }
}

/// Status effect which makes us sneakier and do bonus damage
// tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:52
public partial class datum_status_effect_guardian_stealth : datum_status_effect
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:60
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        Dm.@new<obj_effect_temp_visual_guardian_phase_out>(get_turf(owner));
        owner.melee_damage_lower += damage_bonus;
        owner.melee_damage_upper += damage_bonus;
        if (isbasicmob(owner))
        {
            mob_living_basic basic_owner = (mob_living_basic)owner;
            basic_owner.armour_penetration = 100;
            basic_owner.wound_bonus = stealth_wound_bonus;
            basic_owner.obj_damage = 0;
        }
        to_chat(owner, span_bolddanger("You enter stealth, empowering your next attack."));
        animate(owner, Dm.named("alpha", 15), Dm.named("time", 0.5f * SECONDS));

        RegisterSignals(owner, Dm.list(COMSIG_GUARDIAN_RECALLED, COMSIG_HOSTILE_POST_ATTACKINGTARGET), nameof(forced_exit));
        RegisterSignals(owner, Dm.list(COMSIG_LIVING_ADJUST_BRUTE_DAMAGE, COMSIG_LIVING_ADJUST_BURN_DAMAGE, COMSIG_LIVING_ADJUST_OXY_DAMAGE, COMSIG_LIVING_ADJUST_TOX_DAMAGE), nameof(on_health_changed)); // COMSIG_LIVING_ADJUST_STANDARD_DAMAGE_TYPES (signals_mob_living.dm:95), a list literal
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:76
    public override async DmTask<object> on_remove()
    {
        owner.melee_damage_lower -= damage_bonus;
        owner.melee_damage_upper -= damage_bonus;
        if (isbasicmob(owner))
        {
            mob_living_basic basic_owner = (mob_living_basic)owner;
            basic_owner.armour_penetration = Dm.initial(basic_owner, (mob_living_basic p) => p.armour_penetration);
            basic_owner.wound_bonus = Dm.initial(basic_owner, (mob_living_basic p) => p.wound_bonus);
            basic_owner.obj_damage = Dm.initial(basic_owner, (mob_living_basic p) => p.obj_damage);
        }
        animate(owner, Dm.named("alpha", Dm.initial(owner, (atom p) => p.alpha)), Dm.named("time", 0.5f * SECONDS));
        UnregisterSignal(owner, Dm.list(COMSIG_GUARDIAN_RECALLED, COMSIG_HOSTILE_POST_ATTACKINGTARGET, COMSIG_LIVING_ADJUST_BRUTE_DAMAGE, COMSIG_LIVING_ADJUST_BURN_DAMAGE, COMSIG_LIVING_ADJUST_OXY_DAMAGE, COMSIG_LIVING_ADJUST_TOX_DAMAGE));
        await DmTask.CompletedTask;
        return null;
    }

    /// If we take damage, exit the status effect
    // tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:88
    [SignalHandler]
    public virtual void on_health_changed(mob_living our_mob = null, object type = null, object amount = null, object forced = null)
    {
        if (Dm.num(amount) <= 0)
            return;
        forced_exit();
    }

    /// Forcibly exit the status effect
    // tg: code/modules/mob/living/basic/guardian/guardian_types/assassin.dm:95
    [SignalHandler]
    public virtual void forced_exit()
    {
        // SEND_SIGNAL(owner, COMSIG_GUARDIAN_ASSASSIN_REVEALED) (rules 11.1 (e), a statement)
        if (Dm.truthy(owner._listen_lookup?[COMSIG_GUARDIAN_ASSASSIN_REVEALED]))
            owner._SendSignal(COMSIG_GUARDIAN_ASSASSIN_REVEALED, Dm.list(owner));
        qdel(this);
    }
}
