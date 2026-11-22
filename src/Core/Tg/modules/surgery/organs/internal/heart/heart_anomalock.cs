// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/heart/heart_anomalock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the voltaic combat cyberheart, its flux core, the lightning overlay and the voltaic overdrive.
// The var blocks are generated (D111).

/*!
 * Contains Voltaic Combat Cyberheart
 */
// tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:6
public partial class obj_item_organ_heart_cybernetic_anomalock : obj_item_organ_heart_cybernetic
{
    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:4  #define DOAFTER_IMPLANTING_HEART "implanting" (file-local)
    private const string DOAFTER_IMPLANTING_HEART = "implanting";

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:34
    public override object Destroy(float? force = null)
    {
        deltimer(lightning_timer);
        lightning_overlay = null;
        QDEL_NULL(ref core);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("The voltaic boost will avoid healing toxin damage at all in slime-based humanoids, to prevent harmful side effects.")));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:44
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (core == null)
            return dot;
        add_lightning_overlay(30 * SECONDS);
        playsound(organ_owner, Dm.resource("sound/items/eshield_recharge.ogg"), 40);
        RegisterSignal(organ_owner, COMSIG_MOB_STATCHANGE, nameof(activate_survival_comsig));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:52
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        if (core == null)
            return;
        clear_lightning_overlay(organ_owner as mob);
        UnregisterSignal(organ_owner as datum, COMSIG_MOB_STATCHANGE);
        await tesla_zap(source: organ_owner as atom, zap_range: 20, power: 2.5e5f, cutoff: 1e3f);
        await apply_organ_damage(INFINITY);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:61
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with != user || !iscarbon(user) || core == null)
            return NONE;

        return await self_implant(user as mob_living_carbon);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:67
    public virtual async DmTask<object> self_implant(mob_living_carbon user = null)
    {
        if (Dm.truthy(DOING_INTERACTION(user, DOAFTER_IMPLANTING_HEART)))
            return ITEM_INTERACT_BLOCKING;
        user.balloon_alert(user, I18n.T("this will hurt..."));
        to_chat(user, span_userdanger(I18n.T("Black cyberveins tear your skin apart, pulling the heart into your ribcage. This feels unwise..")));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, interaction_key: DOAFTER_IMPLANTING_HEART)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await Insert(user)))
        {
            await forceMove(user.drop_location() as atom);
            return ITEM_INTERACT_BLOCKING;
        }

        playsound(user, Dm.resource("sound/items/weapons/slice.ogg"), 100, TRUE);
        await user.apply_damage(100, BRUTE, BODY_ZONE_CHEST);
        await user.emote("scream");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:85
    public virtual void add_lightning_overlay(float? time_to_last = null)
    {
        time_to_last ??= 10 * SECONDS;
        if (lightning_overlay != null)
        {
            lightning_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(clear_lightning_overlay), owner), time_to_last, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME)), file: "code/modules/surgery/organs/internal/heart/heart_anomalock.dm", line: 87);
            return;
        }
        lightning_overlay = mutable_appearance(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "lightning") as mutable_appearance;
        owner.add_overlay(lightning_overlay);
        lightning_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(clear_lightning_overlay), owner), time_to_last, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME)), file: "code/modules/surgery/organs/internal/heart/heart_anomalock.dm", line: 91);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:93
    public virtual void clear_lightning_overlay(mob organ_owner = null)
    {
        organ_owner?.cut_overlay(lightning_overlay);
        deltimer(lightning_timer);
        lightning_overlay = null;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:98
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (core != null && Dm.truthy(Dm.bitand(await self_implant(user as mob_living_carbon), ITEM_INTERACT_ANY_BLOCKER)))
            return TRUE;
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:106
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (core == null)
            return;

        if (Dm.num(owner.health) <= Dm.num(owner.crit_threshold))
            await activate_survival(owner);

        if (Dm.truthy(SSmobs.times_fired % (1 * SECONDS)))
            return;

        DmList batteries = Dm.list();
        foreach (obj_item_stock_parts_power_store cell in Dm.each_cast<obj_item_stock_parts_power_store>(assoc_to_values(owner.get_all_cells() as DmList)))
        {
            if (Dm.truthy(cell.used_charge()))
                Dm.append(batteries, cell);
        }

        if (!Dm.truthy(length(batteries)))
            return;

        obj_item_stock_parts_power_store picked = pick(batteries) as obj_item_stock_parts_power_store;
        await picked.give(Dm.num(picked.max_charge()) * 0.1f);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:128
    [SignalHandler]
    public virtual void activate_survival_comsig(mob_living_carbon organ_owner = null, object new_stat = null, object old_stat = null)
    {
        if (Dm.eq(new_stat, SOFT_CRIT) || Dm.eq(new_stat, HARD_CRIT))
            Waiting.NoSleep(activate_survival(organ_owner));
    }

    /// Does a few things to try to help you live whatever you may be going through. Returns TRUE if it activated successfully.
    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:134
    public virtual async DmTask<object> activate_survival(mob_living_carbon organ_owner = null)
    {
        if (!(survival_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return FALSE;

        await organ_owner.apply_status_effect(typeof(datum_status_effect_voltaic_overdrive));
        add_lightning_overlay(30 * SECONDS);
        survival_cooldown = world.time + survival_cooldown_time; // COOLDOWN_START, inline
        _addtimer(Dm.@new<datum_callback>(this, nameof(notify_cooldown), organ_owner), Math.Max(0, survival_cooldown - world.time) /* COOLDOWN_TIMELEFT */, file: "code/modules/surgery/organs/internal/heart/heart_anomalock.dm", line: 141);
        return TRUE;
    }

    ///Alerts our owner that the organ is ready to do its thing again
    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:145
    public virtual void notify_cooldown(mob_living_carbon organ_owner = null)
    {
        balloon_alert(organ_owner, I18n.T("your heart strengthtens"));
        playsound(organ_owner, Dm.resource("sound/items/eshield_recharge.ogg"), 40);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:149
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, required_anomaly))
            return NONE;
        if (core != null)
        {
            balloon_alert(user, I18n.T("core already in!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("core installed"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:161
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (core == null)
        {
            balloon_alert(user, I18n.T("no core!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || !Dm.truthy(core_removable))
        {
            balloon_alert(user, I18n.T("can't remove core!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("removing core..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("core removed"));
        await user.put_in_hands(core);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:176
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == core)
        {
            core = null;
            blood_regeneration_multiplier = Dm.initial(this, (obj_item_organ_heart_cybernetic_anomalock p) => p.blood_regeneration_multiplier);
            remove_organ_trait(TRAIT_SHOCKIMMUNE);
            update_appearance(UPDATE_ICON_STATE);
            RemoveElement(typeof(datum_element_empprotection), EMP_PROTECT_SELF);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:185
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (istype(arrived, typeof(obj_item_assembly_signaler_anomaly)) && core == null)
        {
            core = arrived as obj_item_assembly_signaler_anomaly;
            blood_regeneration_multiplier = 21;
            add_organ_trait(TRAIT_SHOCKIMMUNE);
            update_appearance(UPDATE_ICON_STATE);
            await AddElement(typeof(datum_element_empprotection), EMP_PROTECT_SELF);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:194
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.initial(this, (obj_item_organ_heart_cybernetic_anomalock p) => p.icon_state) + (core != null ? "-core" : "");
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:251
    public override async DmTask<object> hear_beat_noise(mob_living hearer = null)
    {
        if (prob(1))
            to_chat(hearer, span_danger(I18n.T("Yeah. Press a metal disk to the chest of a living arc flash hazard. See what that gets you."))); //the guy is LITERALLY sparking like a tesla coil.
        else
            to_chat(hearer, span_danger(I18n.T("An electrical arc strikes your stethoscope, conducting into you!")));
        if (Dm.truthy(await hearer.electrocute_act(15, "stethoscope", flags: SHOCK_NOGLOVES))) //the stethoscope is in your ears. (returns true if it does damage so we only scream in that case)
            await hearer.emote("scream");
        return span_danger(I18n.T("[] heart produces [].", owner.p_Their(), beat_noise));
    }
}

// tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:198
public partial class obj_item_organ_heart_cybernetic_anomalock_prebuilt : obj_item_organ_heart_cybernetic_anomalock
{
    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        core = Dm.@new<obj_item_assembly_signaler_anomaly_flux>();
        Waiting.NoSleep(core.forceMove(this));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:203
public partial class datum_status_effect_voltaic_overdrive : datum_status_effect
{
    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:210
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        object dot = await base.tick(seconds_between_ticks);
        if (Dm.num(owner.health) > Dm.num(owner.crit_threshold))
            return dot;
        float needs_update = 0;
        needs_update += Dm.num(await owner.heal_overall_damage(brute: 5, burn: 5, updating_health: FALSE));
        needs_update += Dm.num(await owner.adjust_oxy_loss(-5, updating_health: FALSE));
        if (!HAS_TRAIT(owner, TRAIT_TOXINLOVER))
            needs_update += Dm.num(await owner.adjust_tox_loss(-5, updating_health: FALSE));
        if (needs_update != 0)
            await owner.updatehealth();
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:222
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignal(owner, COMSIG_CARBON_LOSE_ORGAN, nameof(on_organ_lost));
        owner.add_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
        await ((datum_reagents)owner.reagents).add_reagent(typeof(datum_reagent_medicine_coagulant), 5);
        owner.add_filter("emp_shield", 2, outline_filter(1, "#639BFF") as DmList);
        to_chat(owner, span_revendanger(I18n.T("You feel a burst of energy! It's do or die!")));
        owner.add_traits(Dm.list(TRAIT_NOSOFTCRIT, TRAIT_NOHARDCRIT, TRAIT_ANALGESIA), TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:231
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, COMSIG_CARBON_LOSE_ORGAN);
        owner.remove_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_damage_slowdown));
        owner.remove_filter("emp_shield");
        owner.balloon_alert(owner, I18n.T("your heart weakens"));
        owner.remove_traits(Dm.list(TRAIT_NOSOFTCRIT, TRAIT_NOHARDCRIT, TRAIT_ANALGESIA), TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    /// Called when an organ is lost in the owner. In the event the owner just lost their voltaic (presumably, the one giving this effect), ends the buff and clears the overlay.
    // tg: code/modules/surgery/organs/internal/heart/heart_anomalock.dm:240
    [SignalHandler]
    public virtual void on_organ_lost(mob_living_carbon source = null, obj_item_organ organ = null, object special = null)
    {
        if (istype(organ, typeof(obj_item_organ_heart_cybernetic_anomalock)))
            qdel(this);
    }
}
