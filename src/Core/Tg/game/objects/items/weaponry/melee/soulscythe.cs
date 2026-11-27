// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/soulscythe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated. The spirit's HUD (_onclick/hud/soulscythe.dm, its blood level) waits for the
// HUD screens.

// tg: code/game/objects/items/weaponry/melee/soulscythe.dm:3
public partial class obj_item_soulscythe : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:1  #define MAX_BLOOD_LEVEL 100 (file-local)
    internal const float MAX_BLOOD_LEVEL = 100;

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        soul = Dm.@new<mob_living_basic_soulscythe>(this);
        RegisterSignal(soul, COMSIG_LIVING_RESIST, nameof(on_resist));
        RegisterSignal(soul, COMSIG_MOB_ATTACK_RANGED, nameof(on_attack));
        RegisterSignal(soul, COMSIG_MOB_ATTACK_RANGED_SECONDARY, nameof(on_secondary_attack));
        RegisterSignal(this, COMSIG_ATOM_INTEGRITY_CHANGED, nameof(on_integrity_change));
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_PLANT), Dm.named("damage_multiplier", 1.5f)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:44
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, Dm.truthy(soul.ckey) ? span_nicegreen(I18n.T("There is a soul inhabiting it.")) : span_danger(I18n.T("It's dormant.")));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:48
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (target_mob.stat != DEAD)
            give_blood(10);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:53
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(soul.ckey) && !Dm.truthy(soul.faction_check_atom(user)))
        {
            to_chat(user, span_warning(I18n.T("You can't pick up []!", this)));
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:59
    public override async DmTask<object> pickup(mob user = null)
    {
        object dot = await base.pickup(user);
        if (Dm.truthy(soul.ckey))
            animate(this); //stop spinnage
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:64
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(soul.ckey))
            reset_spin(); //resume spinnage
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:69
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(@using) || Dm.truthy(soul.ckey) || IS_UNCONSCIOUS_OR_CRIT(soul))
            return null;
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_STATION_SENTIENCE)))
        {
            balloon_alert(user, I18n.T("you can't awaken the scythe!"));
            return null;
        }
        @using = TRUE;
        balloon_alert(user, I18n.T("you hold the scythe up..."));
        ADD_TRAIT(this, TRAIT_NODROP, type);
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            check_jobban: ROLE_PAI,
            poll_time: 20 * SECONDS,
            checked_target: this,
            ignore_category: POLL_IGNORE_POSSESSED_BLADE,
            alert_pic: this,
            role_name_text: "soulscythe soul",
            chat_text_border_icon: this) as mob;
        on_poll_concluded(user as mob_living, chosen_one as mob_dead_observer);
        return null;
    }

    /// Ghost poll has concluded and a candidate has been chosen.
    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:90
    public virtual void on_poll_concluded(mob_living master = null, mob_dead_observer ghost = null)
    {
        if (ghost == null)
        {
            balloon_alert(master, I18n.T("the scythe is dormant!"));
            REMOVE_TRAIT(this, TRAIT_NODROP, type);
            @using = FALSE;
            return;
        }

        soul.PossessByPlayer(ghost.ckey);
        soul.copy_languages(master, LANGUAGE_MASTER); //Make sure the sword can understand and communicate with the master.
        soul.set_allies(Dm.list(Dm.text("[]", REF(master))));
        soul.set_faction(null);
        balloon_alert(master, I18n.T("the scythe glows"));
        add_overlay("soulscythe_gem");
        density = TRUE;
        if (!ismob(loc))
            reset_spin();

        REMOVE_TRAIT(this, TRAIT_NODROP, type);
        @using = FALSE;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:110
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!(move_cooldown <= world.time) || Dm.truthy(charging)) // COOLDOWN_FINISHED(src, move_cooldown)
            return null;
        if (!isturf(loc))
        {
            balloon_alert(user, I18n.T("resist out!"));
            move_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START(src, move_cooldown, 1 SECONDS)
            return null;
        }
        if (!Dm.truthy(use_blood(1, FALSE)))
            return null;
        if (!Dm.eq(pixel_x, base_pixel_x) || !Dm.eq(pixel_y, base_pixel_y))
            animate(this, 0.2f * SECONDS, Dm.named("pixel_x", base_pixel_y), Dm.named("pixel_y", base_pixel_y), Dm.named("flags", ANIMATION_PARALLEL));
        await try_step_multiz(direction);
        move_cooldown = world.time + (Dm.@in(direction, GLOB.cardinals) ? 0.1f * SECONDS : 0.2f * SECONDS); // COOLDOWN_START
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:124
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (!Dm.truthy(charging))
            return dot;
        charging = FALSE;
        throwforce *= 0.5f;
        reset_spin();
        if (ismineralturf(hit_atom))
        {
            turf_closed_mineral hit_rock = (turf_closed_mineral)hit_atom;
            Waiting.NoSleep(hit_rock.gets_drilled());
        }
        if (hit_atom is mob_living hit_mob)
        {
            if (hit_mob.stat != DEAD)
                give_blood(15);
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:139
    public override object AllowClick()
    {
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:142
    public virtual object use_blood(float? amount = null, float? message = null)
    {
        amount ??= 0;
        message ??= TRUE;
        if (amount > Dm.num(soul.get_blood_volume()))
        {
            if (Dm.truthy(message))
                to_chat(soul, span_warning(I18n.T("Not enough blood!")));
            return FALSE;
        }
        soul.adjust_blood_volume(-amount);
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:150
    public virtual void give_blood(object amount = null)
    {
        soul.adjust_blood_volume(amount, maximum: MAX_BLOOD_LEVEL);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:153
    [SignalHandler]
    public virtual void on_resist(mob_living user = null)
    {
        if (isturf(loc))
            return;
        Waiting.Detach(break_out()); // INVOKE_ASYNC
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:160
    public virtual async DmTask break_out()
    {
        if (!Dm.truthy(use_blood(10)))
            return;
        balloon_alert(soul, I18n.T("you resist..."));
        if (!Dm.truthy(await do_after(soul, 5 * SECONDS, this, timed_action_flags: IGNORE_TARGET_LOC_CHANGE)))
        {
            balloon_alert(soul, I18n.T("interrupted!"));
            return;
        }
        balloon_alert(soul, I18n.T("you break out"));
        if (ismob(loc))
        {
            mob holder = (mob)loc;
            await holder.temporarilyRemoveItemFromInventory(this);
        }
        await forceMove(drop_location() as atom);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:173
    [SignalHandler]
    public virtual void on_integrity_change(datum source = null, object old_value = null, object new_value = null)
    {
        soul.set_health(new_value);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:178
    [SignalHandler]
    public virtual void on_attack(mob_living source = null, atom attacked_atom = null, object modifiers = null)
    {
        if (!(attack_cooldown <= world.time) || !isturf(loc)) // COOLDOWN_FINISHED(src, attack_cooldown)
            return;
        if (get_dist(source, attacked_atom) > 1)
            Waiting.Detach(shoot_target(attacked_atom)); // INVOKE_ASYNC
        else
            Waiting.Detach(slash_target(attacked_atom)); // INVOKE_ASYNC
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:188
    [SignalHandler]
    public virtual void on_secondary_attack(mob_living source = null, atom attacked_atom = null, object modifiers = null)
    {
        if (!(attack_cooldown <= world.time) || !isturf(loc)) // COOLDOWN_FINISHED(src, attack_cooldown)
            return;
        Waiting.Detach(charge_target(attacked_atom)); // INVOKE_ASYNC
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:195
    public virtual async DmTask shoot_target(atom attacked_atom = null)
    {
        if (!Dm.truthy(use_blood(15)))
            return;
        attack_cooldown = world.time + 3 * SECONDS; // COOLDOWN_START(src, attack_cooldown, 3 SECONDS)
        obj_projectile projectile = Dm.@new<obj_projectile_soulscythe>(get_turf(this));
        await projectile.aim_projectile(attacked_atom, this);
        projectile.firer = this;
        await projectile.fire(null, attacked_atom);
        visible_message(span_danger(I18n.T("[] fires at []!", this, attacked_atom)), span_notice(I18n.T("You fire at []!", attacked_atom)));
        playsound(this, Dm.resource("sound/effects/magic/fireball.ogg"), 50, TRUE);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:206
    public virtual async DmTask slash_target(atom attacked_atom = null)
    {
        if (isliving(attacked_atom) && Dm.truthy(use_blood(10)))
        {
            mob_living attacked_mob = (mob_living)attacked_atom;
            if (attacked_mob.stat != DEAD)
                give_blood(15);
            await attacked_mob.apply_damage(damage: force * (ismining(attacked_mob) ? 2 : 1), sharpness: SHARP_EDGED, exposed_wound_bonus: 5);
            to_chat(attacked_mob, span_userdanger(I18n.T("You're slashed by []!", this)));
        }
        else if ((ismachinery(attacked_atom) || isstructure(attacked_atom)) && Dm.truthy(use_blood(5)))
        {
            obj attacked_obj = (obj)attacked_atom;
            await attacked_obj.take_damage(force, BRUTE, MELEE, FALSE);
        }
        else
            return;
        attack_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START(src, attack_cooldown, 1 SECONDS)
        animate(this);
        SpinAnimation(5);
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_spin)), 1 * SECONDS, file: "code/game/objects/items/weaponry/melee/soulscythe.dm", line: 221);
        visible_message(span_danger(I18n.T("[] slashes []!", this, attacked_atom)), span_notice(I18n.T("You slash []!", attacked_atom)));
        playsound(this, Dm.resource("sound/items/weapons/bladeslice.ogg"), 50, TRUE);
        await do_attack_animation(attacked_atom, ATTACK_EFFECT_SLASH);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:226
    public virtual async DmTask charge_target(atom attacked_atom = null)
    {
        if (Dm.truthy(charging) || !Dm.truthy(use_blood(30)))
            return;
        attack_cooldown = world.time + 5 * SECONDS; // COOLDOWN_START(src, attack_cooldown, 5 SECONDS)
        animate(this);
        charging = TRUE;
        visible_message(span_danger(I18n.T("[] starts charging...", this)));
        balloon_alert(soul, I18n.T("you start charging..."));
        if (!Dm.truthy(await do_after(soul, 2 * SECONDS, this, timed_action_flags: IGNORE_TARGET_LOC_CHANGE)))
        {
            balloon_alert(soul, I18n.T("interrupted!"));
            return;
        }
        visible_message(span_danger(I18n.T("[] charges at []!", this, attacked_atom)), span_notice(I18n.T("You charge at []!", attacked_atom)));
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(this));
        playsound(this, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE);
        SpinAnimation(1);
        throwforce *= 2;
        await throw_at(attacked_atom, 10, 3, soul, FALSE);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:244
    public virtual void reset_spin()
    {
        animate(this);
        SpinAnimation(15);
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:248
    public override object Destroy(float? force = null)
    {
        soul.ghostize();
        QDEL_NULL(ref soul);
        return base.Destroy(force);
    }
}

/// Soulscythe mob, just a way for players to control the scythe.
// tg: code/game/objects/items/weaponry/melee/soulscythe.dm:254
[DmPath("/mob/living/basic/soulscythe")]
public partial class mob_living_basic_soulscythe : mob_living_basic
{
    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:264
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE, TRAIT_LAVA_IMMUNE), INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:268
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(this))
            adjust_blood_volume(round(1 * Dm.num(seconds_per_tick)), maximum: obj_item_soulscythe.MAX_BLOOD_LEVEL);
        return null;
    }
}

/// Special projectile for the soulscythe.
// tg: code/game/objects/items/weaponry/melee/soulscythe.dm:273
public partial class obj_projectile_soulscythe : obj_projectile
{
    // tg: code/game/objects/items/weaponry/melee/soulscythe.dm:282
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        if (target is mob_living as_living)
        {
            if (ismining(as_living))
                damage *= 2;
        }
        return await base.on_hit(target, blocked, pierce_hit);
    }
}
