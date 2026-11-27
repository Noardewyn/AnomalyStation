// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base mimic and the copy mimic with the machine (gaps package C1), and the crate mimic with its storage (the bitrunning psyker
// shuffle's crates). Left inert: the gun mimic (the guns' firing through a mob), xenobio: rows in the report; animatable_blacklist
// is the staff of animation's list.

public partial class datum_controller_global_vars : datum_controller
{
    //TODO: Better solution for these?
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:3
    public DmList animatable_blacklist;

    public virtual void InitGlobalanimatable_blacklist()
    {
        animatable_blacklist = typecacheof(Dm.list(typeof(obj_structure_table), typeof(obj_structure_cable), typeof(obj_structure_window), typeof(obj_structure_blob))) as DmList;
        Dm.append(ref gvars_datum_init_order, "animatable_blacklist");
    }
}

// tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:10
[DmPath("/mob/living/basic/mimic")]
public partial class mob_living_basic_mimic : mob_living_basic
{
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:38
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || !Dm.truthy(knockdown_people) || !prob(15) || target is not mob_living_carbon carbon_target)
            return dot;
        await carbon_target.Paralyze(4 * SECONDS);
        carbon_target.visible_message(span_danger(I18n.T("\\The [] knocks down \\the []!", this, carbon_target)),
            span_userdanger(I18n.T("\\The [] knocks you down!", this)));
        return dot;
    }
}

// ****************************
// CRATE MIMIC
// ****************************

// Aggro when you try to open them. Will also pickup loot when spawns and drop it when dies.
// tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:53
public partial class mob_living_basic_mimic_crate : mob_living_basic_mimic
{
    private const int CANT_INSERT_FULL = -1;

    // Pickup loot
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        @lock = Dm.@new<datum_action_innate_mimic_lock__2>();
        Waiting.NoSleep(@lock.Grant(this));
        ADD_TRAIT(this, TRAIT_AI_PAUSED, INNATE_TRAIT);
        if (ai_controller is datum_ai_controller brain)
            Waiting.NoSleep(brain.force_ai_off()); //start inert, let gullible people pull us into cargo or something and then go nuts when opened
        if (Dm.truthy(mapload)) //eat shit
        {
            foreach (obj_item item in Dm.each<obj_item>(loc))
                Waiting.NoSleep(item.forceMove(this));
        }

        crate = Dm.@new((Type)crate, null); // Nullspaced so we don't accidentally spew it out when opening
        atom_movable pretend = (atom_movable)crate;
        icon = pretend.icon;
        icon_state = pretend.icon_state;
        base_icon_state = pretend.base_icon_state;
        icon_living = icon_state;
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:96
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref crate);
        @lock = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:101
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        if (Dm.truthy(human_user.combat_mode))
            return await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(trigger()))
        {
            to_chat(user, span_danger(I18n.T("As you try to open [] it []nearly clamps down on your fingers!", this, Dm.truthy(length(contents)) ? I18n.T("stiffens up and ") : "")));
            return TRUE;
        }
        toggle_open(user);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:110
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        toggle_open(); // show our cool lid at the dumbass humans
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:114
    public virtual object trigger()
    {
        datum_ai_controller brain = ai_controller as datum_ai_controller;
        if (brain == null || client != null)
            return FALSE;
        if (!Dm.eq(brain.ai_status, AI_STATUS_OFF))
            return FALSE;
        visible_message(span_danger(I18n.T("[] starts to move!", this)));
        REMOVE_TRAIT(this, TRAIT_AI_PAUSED, INNATE_TRAIT);
        Waiting.NoSleep(brain.clear_forced_off());
        if (Dm.truthy(length(contents)))
            locked = TRUE; //if this was a crate with loot then we dont want people to just leftclick it to open it then bait it somewhere and steal its loot
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:126
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        if (Dm.num(amount) > 0)
            trigger();
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:131
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        obj_structure_closet_crate lootbox = Dm.@new<obj_structure_closet_crate>(get_turf(this));
        // Put loot in crate
        foreach (obj loot in Dm.each<obj>(this))
            await loot.forceMove(lootbox);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:138
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        if (target == this)
        {
            toggle_open();
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }
        return await base.early_melee_attack(target, modifiers, ignore_cooldown);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:144
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (mover is obj_structure_closet)
            return FALSE;
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:149
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        if (Dm.eq((ai_controller as datum_ai_controller)?.ai_status, AI_STATUS_OFF) && client == null)
            return await ((atom_movable)crate).examine(user);
        return await base.examine(user, __extra);
    }

    /**
    * Used to open and close the mimic
    *
    * Will insert tile contents into the mimic when closing
    * Will dump mimic contents into the time when opening
    * Does nothing if the mimic locked itself
    */
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:161
    public virtual void toggle_open(mob user = null)
    {
        if (Dm.truthy(locked))
        {
            if (user != null)
                balloon_alert(user, I18n.T("too stiff!"));
            return;
        }
        if (!Dm.truthy(opened))
        {
            ADD_TRAIT(this, TRAIT_UNDENSE, MIMIC_TRAIT);
            opened = TRUE;
            icon_state = Dm.text("[]open", base_icon_state);
            playsound(this, Dm.resource("sound/machines/crate/crate_open.ogg"), 50, TRUE);
            foreach (atom_movable movable in Dm.each<atom_movable>(this))
                Waiting.NoSleep(movable.forceMove(loc));
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_UNDENSE, MIMIC_TRAIT);
            opened = FALSE;
            icon_state = Dm.str(base_icon_state);
            playsound(this, Dm.resource("sound/machines/crate/crate_close.ogg"), 50, TRUE);
            foreach (atom_movable movable in Dm.each<atom_movable>(get_turf(this)))
            {
                if (movable != this && Dm.eq(insert(movable), CANT_INSERT_FULL))
                {
                    playsound(this, Dm.resource("sound/items/trayhit/trayhit2.ogg"), 50, TRUE);
                    break;
                }
            }
        }
    }

    /**
    * Called by toggle_open to put items inside the mimic when it's being closed
    *
    * Will return CANT_INSERT_FULL (-1) if the insertion fails due to the storage capacity of the mimic having been reached
    * Will return FALSE if insertion fails
    * Will return TRUE if insertion succeeds
    * Arguments:
    * * AM - item to be inserted
    */
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:192
    public virtual object insert(atom_movable movable = null)
    {
        if (contents.len >= storage_capacity)
            return CANT_INSERT_FULL;
        if (Dm.truthy(insertion_allowed(movable)))
        {
            Waiting.NoSleep(movable.forceMove(this));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:200
    public virtual object insertion_allowed(atom_movable movable = null)
    {
        if (Dm.truthy(movable.anchored))
            return FALSE;
        if (ismob(movable))
        {
            if (!isliving(movable))  //Don't let ghosts and such get trapped in the beast.
                return FALSE;
            mob_living living = movable as mob_living;
            if (Dm.truthy(living.anchored) || living.buckled != null || Dm.truthy(living.incorporeal_move) || Dm.truthy(living.has_buckled_mobs()))
                return FALSE;
            if (living.mob_size > MOB_SIZE_TINY) // Tiny mobs are treated as items.
            {
                if (Dm.truthy(living.density) || living.mob_size > max_mob_size)
                    return FALSE;
                float mobs_stored = 0;
                foreach (mob_living living_mob in Dm.each<mob_living>(contents))
                {
                    mobs_stored++;
                    if (mobs_stored >= mob_storage_capacity)
                        return FALSE;
                }
            }
            Waiting.NoSleep(living.stop_pulling());
        }
        else if (movable is obj_structure_closet)
            return FALSE;
        else if (isobj(movable))
        {
            if (Dm.truthy(movable.has_buckled_mobs()))
                return FALSE;
            else if (isitem(movable) && !HAS_TRAIT(movable, TRAIT_NODROP))
                return TRUE;
        }
        else
            return FALSE;
        return TRUE;
    }
}

// kept as tg has it: the Activate below is declared on /datum/action/innate/mimic/lock, not on the crate's /datum/action/innate/mimic_lock,
// so the crate's lock action does nothing when used
// tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:246 (rules 2.3: /datum/action/innate/mimic_lock is
// datum_action_innate_mimic_lock__2)
[DmPath("/datum/action/innate/mimic/lock")]
public partial class datum_action_innate_mimic_lock : datum_action_innate_mimic
{
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:246
    public override async DmTask<object> Activate()
    {
        mob_living_basic_mimic_crate mimic = owner as mob_living_basic_mimic_crate;
        mimic.locked = Dm.not(mimic.locked);
        if (!Dm.truthy(mimic.locked))
            to_chat(mimic, span_warning(I18n.T("You loosen up, allowing yourself to be opened and closed.")));
        else
            to_chat(mimic, span_warning(I18n.T("You stiffen up, preventing anyone from opening or closing you.")));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:258
[DmPath("/mob/living/basic/mimic/copy")]
public partial class mob_living_basic_mimic_copy : mob_living_basic_mimic
{
    /// googly eyes overlay (rules 10.6: a TgState field, made on first use)
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:266
    private static mutable_appearance googly_eyes => TgState.Current.mob_living_basic_mimic_copy__googly_eyes ??= mutable_appearance(Dm.resource("icons/mob/simple/mob.dmi"), "googly_eyes") as mutable_appearance;

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:274
    [DmParams("mapload", "copy", "creator", "destroy_original", "no_googlies")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj copy = Dm.arg<obj>(__extra, 0);
        mob_living creator = Dm.arg<mob_living>(__extra, 1);
        object destroy_original = Dm.arg(__extra, 2) ?? FALSE;
        object no_googlies = Dm.arg(__extra, 3) ?? FALSE;
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_PERMANENTLY_MORTAL, INNATE_TRAIT); // They won't remember their original contents upon ressurection and would just be floating eyes
        if (Dm.truthy(no_googlies))
            overlay_googly_eyes = FALSE;
        Waiting.NoSleep(CopyObject(copy, creator, destroy_original));
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:281
    public override object Destroy(float? force = null)
    {
        creator_ref = null;
        copied_ref = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:286
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //dead or deleted
            return dot;
        if (Dm.truthy(idledamage) && !Dm.truthy(ckey) && !Dm.truthy((ai_controller as datum_ai_controller)?.blackboard[BB_CURRENT_TARGET])) //Objects eventually revert to normal if no one is around to terrorize
            Waiting.NoSleep(adjust_brute_loss(0.5f * Dm.num(seconds_per_tick)));
        foreach (mob_living victim in Dm.each<mob_living>(contents)) //a fix for animated statues from the flesh to stone spell
        {
            Waiting.NoSleep(death());
            return dot;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:296
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        foreach (atom_movable movable in Dm.each_cast<atom_movable>(this))
            await movable.forceMove(get_turf(this));
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:301
    public override async DmTask<object> wabbajack(object what_to_randomize = null, float? change_flags = null)
    {
        visible_message(span_warning(I18n.T("[] resists polymorphing into a new creature!", this)));
        return null;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:307
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null) // if our object gets deleted it calls Exited
    {
        object dot = await base.Exited(gone, direction);
        atom_movable copied = (copied_ref as datum_weakref)?.resolve() as atom_movable;
        if (copied == null)
        {
            copied_ref = null;
            return dot;
        }
        if (QDELETED(this) || gone != copied)
            return dot;
        await death();
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:317
    public virtual async DmTask change_owner(mob owner = null)
    {
        mob creator_resolved = (creator_ref as datum_weakref)?.resolve() as mob;
        if (creator_resolved == null)
            creator_ref = null;
        if (QDELETED(owner) || creator_resolved == owner)
            return;
        if (owner is mob_living living_owner)
        {
            unfriend(creator_resolved as mob_living);
            await befriend(living_owner);
        }
        creator_ref = WEAKREF(owner) as datum_weakref;
    }

    /// Check whether this object can be copied. If destroy_original is true, this proc is ignored.
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:329
    public virtual object check_object(obj target = null)
    {
        return Dm.num((isitem(target) || isstructure(target)) && !Dm.truthy(is_type_in_typecache(target, GLOB.animatable_blacklist)));
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:332
    public virtual async DmTask<object> CopyObject(obj original = null, mob_living user = null, object destroy_original = null)
    {
        destroy_original ??= FALSE;
        if (QDELETED(original) || (!Dm.truthy(destroy_original) && !Dm.truthy(check_object(original))))
            return FALSE;
        if (!Dm.truthy(destroy_original))
            await original.forceMove(this);
        copied_ref = WEAKREF(original) as datum_weakref;
        CopyObjectVisuals(original);
        if (Dm.truthy(overlay_googly_eyes))
            add_overlay(googly_eyes);
        if (isstructure(original) || ismachinery(original))
        {
            health = (Dm.num(anchored) * 50) + 50;
            if (Dm.truthy(original.density) && Dm.truthy(original.anchored))
            {
                knockdown_people = TRUE;
                melee_damage_lower = Dm.num(melee_damage_lower) * 2;
                melee_damage_upper = Dm.num(melee_damage_upper) * 2;
            }
        }
        else if (original is obj_item I)
        {
            health = 15 * Dm.num(I.w_class);
            melee_damage_lower = 2 + Dm.num(I.force);
            melee_damage_upper = 2 + Dm.num(I.force);
        }
        maxHealth = health;
        if (user != null)
            await change_owner(user);
        if (Dm.truthy(destroy_original))
            qdel(original);
        return TRUE;
    }

    /// Copies the object visually including name and desc
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:360
    public virtual void CopyObjectVisuals(obj original = null)
    {
        name = original.name;
        desc = original.desc;
        icon = original.icon;
        icon_state = original.icon_state;
        icon_living = icon_state;
        copy_overlays(original, cut_old: TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:266  var/static/mutable_appearance/googly_eyes
    internal mutable_appearance mob_living_basic_mimic_copy__googly_eyes;
}
