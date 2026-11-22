// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/handcuffs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice E (restraints): the generic restraints, handcuffs, the cable restraints with their colours, the zipties, the shadow
// shackles, the leg cuffs, the bear trap, the energy snare and the bolas, whole. Left: the fantasy bonuses (the fantasy
// component is admin tooling), vv_edit_var of the cable colour (admin tooling), the cable restraints' screentips (M9); docs/ai/specs/not-translated.md
// section 46. The gonbola's pacify effect since gaps MS1. The var blocks are
// generated (D111).

// tg: code/game/objects/items/handcuffs.dm:11
public partial class obj_item_restraints : obj_item
{
    // tg: code/game/objects/items/handcuffs.dm:17
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is strangling []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/game/objects/items/handcuffs.dm:33
public partial class obj_item_restraints_handcuffs : obj_item_restraints
{
    // tg: code/game/objects/items/handcuffs.dm:80
    [DmParams("target_mob", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(iscarbon(target_mob)) || Dm.truthy(used))
            return null;

        await attempt_to_cuff(target_mob as mob_living_carbon, user);
        return null;
    }

    /// Handles all of the checks and application in a typical situation where someone attacks a carbon victim with the handcuff item.
    // tg: code/game/objects/items/handcuffs.dm:87
    public virtual async DmTask attempt_to_cuff(mob_living_carbon victim = null, mob_living user = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(victim._listen_lookup?[COMSIG_CARBON_CUFF_ATTEMPTED]) ? (object)NONE : victim._SendSignal(COMSIG_CARBON_CUFF_ATTEMPTED, Dm.list(victim, user)), COMSIG_CARBON_CUFF_PREVENT)))
        {
            victim.balloon_alert(user, I18n.T("can't be handcuffed!"));
            return;
        }

        if (Dm.truthy(await handcuffs_clumsiness_check(user)))
            return;

        if (victim.handcuffed != null)
        {
            victim.balloon_alert(user, I18n.T("already handcuffed!"));
            return;
        }

        if (!Dm.truthy(victim.canBeHandcuffed()))
        {
            victim.balloon_alert(user, I18n.T("can't be handcuffed!"));
            return;
        }

        victim.visible_message(
            span_danger(I18n.T("[] is trying to put [] on []!", user, this, victim)),
            span_userdanger(I18n.T("[] is trying to put [] on you!", user, this))
        );

        if (Dm.truthy(victim.is_blind()))
            to_chat(victim, span_userdanger(I18n.T("As you feel someone grab your wrists, [] start digging into your skin!", this)));

        playsound(loc, cuffsound, 30, TRUE, -2);
        log_combat(user, victim, "attempted to handcuff");

        if (!Dm.truthy(await do_after(user, get_handcuff_time(user), victim, timed_action_flags: IGNORE_SLOWDOWNS)) || !Dm.truthy(victim.canBeHandcuffed()))
        {
            victim.balloon_alert(user, I18n.T("failed to handcuff!"));
            to_chat(user, span_warning(I18n.T("You fail to handcuff []!", victim)));
            log_combat(user, victim, "failed to handcuff");
            return;
        }

        await apply_cuffs(victim, user, dispense: iscyborg(user) ? TRUE : FALSE);
        playsound(loc, cuffsuccesssound, 30, TRUE, -2);

        victim.visible_message(
            span_notice(I18n.T("[] handcuffs [].", user, victim)),
            span_userdanger(I18n.T("[] handcuffs you.", user))
        );

        log_combat(user, victim, "successfully handcuffed");
        SSblackbox.record_feedback("tally", "handcuffs", 1, GetType());
    }

    ///Return the amount of time the user would spend cuffing someone or something
    // tg: code/game/objects/items/handcuffs.dm:132
    public virtual float get_handcuff_time(mob user = null)
    {
        return handcuff_time * (HAS_TRAIT(user, TRAIT_FAST_CUFFING) ? 0.75f : 1);
    }

    // tg: code/game/objects/items/handcuffs.dm:135
    public virtual async DmTask<object> handcuffs_clumsiness_check(mob user = null)
    {
        if (!iscarbon(user) || !HAS_TRAIT(user, TRAIT_CLUMSY) || prob(50)) //Clumsy people have a 50% chance to handcuff themselves instead of their target.
            return FALSE;
        to_chat(user, span_warning(I18n.T("Uh... how do those things work?!")));
        await apply_cuffs(user as mob_living_carbon, user);
        return TRUE;
    }

    /**
     * When called, this instantly puts handcuffs on someone (if actually possible)
     *
     * Arguments:
     * * mob/living/carbon/target - Who is being handcuffed
     * * mob/user - Who or what is doing the handcuffing
     * * dispense - True if the cuffing should create a new item instead of using putting src on the mob, false otherwise. False by default.
    */
    // tg: code/game/objects/items/handcuffs.dm:149
    public virtual async DmTask apply_cuffs(mob_living_carbon target = null, mob user = null, float? dispense = null)
    {
        dispense ??= FALSE;
        if (Dm.truthy(target.handcuffed))
            return;

        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)) && !Dm.truthy(dispense))
            return;

        obj_item_restraints_handcuffs cuffs = this;
        if (Dm.truthy(dispense))
            cuffs = Dm.@new(GetType()) as obj_item_restraints_handcuffs;

        await target.equip_to_slot(cuffs, ITEM_SLOT_HANDCUFFED);

        if (Dm.truthy(dispense))
            qdel(this);
    }

    // tg: code/game/objects/items/handcuffs.dm:165
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await base.equipped__proc(user, slot, initial);
        if (Dm.eq(slot, ITEM_SLOT_HANDCUFFED))
            RegisterSignal(this, COMSIG_ITEM_DROPPED, nameof(on_uncuffed)); //Make sure zipties are no longer usable the next time someone removes them
    }

    // tg: code/game/objects/items/handcuffs.dm:170
    [SignalHandler]
    public virtual void on_uncuffed(datum source = null, mob_living wearer = null)
    {
        UnregisterSignal(this, COMSIG_ITEM_DROPPED);
    }
}

/**
 * # Cable restraints
 *
 * Ghetto handcuffs. Removing those is faster.
*/
// tg: code/game/objects/items/handcuffs.dm:201
public partial class obj_item_restraints_handcuffs_cable : obj_item_restraints_handcuffs
{
    // tg: code/game/objects/items/handcuffs.dm:218
    [DmParams("mapload", "new_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_color = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_color));

        // var/static/list/hovering_item_typechecks and AddElement(/datum/element/contextual_screentip_item_typechecks, ...) (:226-232)
        Dm.Deferred("code/game/objects/items/handcuffs.dm:227", "Initialize: AddElement(/datum/element/contextual_screentip_item_typechecks, hovering_item_typechecks) of the cable restraints: screentips (M10+)");
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob), ITEM_SLOT_HANDCUFFED));

        if (Dm.truthy(new_color))
            set_cable_color(new_color);

        // tg: code/game/objects/items/handcuffs.dm:233  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_restraints_handcuffs_cable__Initialize__slapcraft_recipe_list));
        return dot;
    }

    // tg: code/game/objects/items/handcuffs.dm:240
    public virtual void set_cable_color(object new_color = null)
    {
        color = GLOB.cable_colors[new_color];
        cable_color = (string)new_color;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/items/handcuffs.dm:252
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(cable_color))
        {
            string new_inhand_icon = Dm.text("coil_[]", cable_color);
            if (!Dm.eq(new_inhand_icon, inhand_icon_state))
                inhand_icon_state = new_inhand_icon; //small memory optimization.
        }
        return dot;
    }
}

/**
 * # Zipties
 *
 * One-use handcuffs that take 45 seconds to resist out of instead of one minute. This turns into the used version when applied.
*/
// tg: code/game/objects/items/handcuffs.dm:345
public partial class obj_item_restraints_handcuffs_cable_zipties : obj_item_restraints_handcuffs_cable
{
    // tg: code/game/objects/items/handcuffs.dm:358
    public override void on_uncuffed(datum source = null, mob_living wearer = null)
    {
        base.on_uncuffed(source, wearer);
        desc = "A pair of broken zipties.";
        icon_state = "cuff_used";
        used = TRUE;
    }
}

///handcuffs applied by cult magic and heretics sacrifice
// tg: code/game/objects/items/handcuffs.dm:391
public partial class obj_item_restraints_handcuffs_cult : obj_item_restraints_handcuffs
{
    // tg: code/game/objects/items/handcuffs.dm:398
    public override void on_uncuffed(datum source = null, mob_living wearer = null)
    {
        base.on_uncuffed(source, wearer);
        wearer.visible_message(span_danger(I18n.T("[]'s shackles shatter in a discharge of dark magic!", wearer)), span_userdanger(I18n.T("Your [] shatters in a discharge of dark magic!", this)));
        qdel(this);
    }
}

/**
 * # Bear trap
 *
 * This opens, closes, and bites people's legs.
 */
// tg: code/game/objects/items/handcuffs.dm:432
public partial class obj_item_restraints_legcuffs_beartrap : obj_item_restraints_legcuffs
{
    // tg: code/game/objects/items/handcuffs.dm:447
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/objects/items/handcuffs.dm:450  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_restraints_legcuffs_beartrap__Initialize__loc_connections;

    // tg: code/game/objects/items/handcuffs.dm:455
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_restraints_legcuffs_beartrap p) => p.icon_state), armed);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/handcuffs.dm:459
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is sticking [] head in \\the []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        playsound(loc, "sound/items/weapons/bladeslice.ogg", 50, TRUE, -1);
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/handcuffs.dm:464
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!ishuman(user) || IS_UNCONSCIOUS_OR_CRIT(user) || HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
            return dot;

        playsound(loc, Dm.resource("sound/items/weapons/handcuffs.ogg"), 30, TRUE, -3);

        armed = armed != 0 ? FALSE : TRUE;
        update_appearance();

        if (Dm.truthy(armed) && (HAS_TRAIT(user, TRAIT_DUMB) || HAS_TRAIT(user, TRAIT_CLUMSY)) && prob(25))
        {
            to_chat(user, span_warning(I18n.T("Your hand slips, setting off the trigger!")));
            string hand_zone = Dm.eq(user.held_index_to_dir(user.active_hand_index), "r") ? BODY_ZONE_PRECISE_R_HAND : BODY_ZONE_PRECISE_L_HAND;
            await spring_trap(user, def_zone: hand_zone);
            return dot;
        }

        to_chat(user, span_notice(I18n.T("[] is now []", this, Dm.truthy(armed) ? "armed" : "disarmed")));
        return dot;
    }

    // tg: code/game/objects/items/handcuffs.dm:483
    public override async DmTask<object> attempt_pickup(mob_living user = null, float? skip_grav = null)
    {
        skip_grav ??= FALSE;
        if (!Dm.truthy(armed))
            return await base.attempt_pickup(user, skip_grav);

        if ((HAS_TRAIT(user, TRAIT_DUMB) || HAS_TRAIT(user, TRAIT_CLUMSY)) && prob(25))
        {
            to_chat(user, span_warning(I18n.T("Your hand slips, setting off the trigger!")));
            string hand_zone = Dm.eq(user.held_index_to_dir(user.active_hand_index), "r") ? BODY_ZONE_PRECISE_R_HAND : BODY_ZONE_PRECISE_L_HAND;
            await spring_trap(user, def_zone: hand_zone);
            return TRUE;
        }

        return await base.attempt_pickup(user, skip_grav);
    }

    // tg: code/game/objects/items/handcuffs.dm:495
    public override async DmTask<object> on_found(mob finder = null)
    {
        if (!Dm.truthy(armed))
            return FALSE;

        mob_living victim = finder as mob_living;
        if (Dm.truthy(victim))
        {
            string hand_zone = Dm.eq(victim.held_index_to_dir(victim.active_hand_index), "r") ? BODY_ZONE_PRECISE_R_HAND : BODY_ZONE_PRECISE_L_HAND;
            await spring_trap(victim, def_zone: hand_zone, ignore_movetypes: TRUE);
            return TRUE;
        }

        return FALSE;
    }

    /**
     * Closes a bear trap
     *
     * Closes a bear trap.
     * Arguments:
     */
    // tg: code/game/objects/items/handcuffs.dm:512
    public virtual void close_trap()
    {
        armed = FALSE;
        update_appearance();
        playsound(this, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
    }

    // tg: code/game/objects/items/handcuffs.dm:517
    [SignalHandler]
    public virtual void trap_stepped_on(datum source = null, atom_movable entering = null, params object[] __extra)
    {
        Waiting.NoSleep(spring_trap(entering));
    }

    /**
     * Tries to spring the trap on the target movable.
     *
     * This proc is safe to call without knowing if the target is valid or if the trap is armed.
     *
     * Does not trigger on tiny mobs.
     * If ignore_movetypes is FALSE, does not trigger on floating / flying / etc. mobs.
     */
    // tg: code/game/objects/items/handcuffs.dm:530
    public virtual async DmTask spring_trap(atom_movable target = null, float? ignore_movetypes = null, float? hit_prone = null, object def_zone = null)
    {
        ignore_movetypes ??= FALSE;
        hit_prone ??= FALSE;
        def_zone ??= BODY_ZONE_CHEST;
        if (!Dm.truthy(armed) || !isliving(target))
            return;

        mob_living victim = target as mob_living;
        if (istype(victim.buckled, typeof(obj_vehicle)))
        {
            obj_vehicle ridden_vehicle = victim.buckled as obj_vehicle;
            if (!Dm.truthy(ridden_vehicle.are_legs_exposed)) //close the trap without injuring/trapping the rider if their legs are inside the vehicle at all times.
            {
                close_trap();
                ridden_vehicle.visible_message(span_danger(I18n.T("[] triggers \\the [].", ridden_vehicle, this)));
                return;
            }
        }

        //don't close the trap if they're as small as a mouse
        if (Dm.num(victim.mob_size) <= MOB_SIZE_TINY)
            return;
        if (!Dm.truthy(ignore_movetypes) && Dm.truthy(Dm.bitand(victim.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return;

        close_trap();
        if (Dm.truthy(ignore_movetypes))
            victim.visible_message(span_danger(I18n.T("\\The [] ensnares []!", this, victim)),
                    span_userdanger(I18n.T("\\The [] ensnares you!", this)));
        else
            victim.visible_message(span_danger(I18n.T("[] triggers \\the [].", victim, this)),
                    span_userdanger(I18n.T("You trigger \\the []!", this)));

        if (iscarbon(victim) && (victim.body_position == STANDING_UP || Dm.truthy(hit_prone)) && !(Dm.eq(def_zone, BODY_ZONE_PRECISE_R_HAND) || Dm.eq(def_zone, BODY_ZONE_PRECISE_L_HAND)))
        {
            mob_living_carbon carbon_victim = victim as mob_living_carbon;
            def_zone = pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG);
            if (!Dm.truthy(carbon_victim.legcuffed) && carbon_victim.num_legs >= 2) //beartrap can't cuff your leg if there's already a beartrap or legcuffs, or you don't have two legs.
            {
                Waiting.Detach(carbon_victim.equip_to_slot(this, ITEM_SLOT_LEGCUFFED));
                SSblackbox.record_feedback("tally", "handcuffs", 1, GetType());
            }
        }

        await victim.apply_damage(trap_damage, BRUTE, def_zone, sharpness: SHARP_POINTY, wound_bonus: trap_damage / 2, exposed_wound_bonus: trap_damage);
    }
}

// tg: code/game/objects/items/handcuffs.dm:572
public partial class obj_item_restraints_legcuffs_beartrap_energy : obj_item_restraints_legcuffs_beartrap
{
    // tg: code/game/objects/items/handcuffs.dm:582
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(dissipate)), 10 * SECONDS, file: "code/game/objects/items/handcuffs.dm", line: 591);
        return dot;
    }

    /**
     * Handles energy snares disappearing
     *
     * If the snare isn't closed on anyone, it will disappear in a shower of sparks.
     * Arguments:
     */
    // tg: code/game/objects/items/handcuffs.dm:592
    public virtual void dissipate()
    {
        if (!ismob(loc))
        {
            do_sparks(1, TRUE, this);
            qdel(this);
        }
    }

    // tg: code/game/objects/items/handcuffs.dm:597
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (isturf(loc))
            await spring_trap(user);

        return await base.attack_hand(user, modifiers, __extra);
    }
}

// tg: code/game/objects/items/handcuffs.dm:607
public partial class obj_item_restraints_legcuffs_bola : obj_item_restraints_legcuffs
{
    // tg: code/game/objects/items/handcuffs.dm:623
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        if (!Dm.truthy(await base.throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart, throw_datum_typepath, __extra)))
            return null;
        playsound(loc, Dm.resource("sound/items/weapons/bolathrow.ogg"), 75, TRUE);
        return null;
    }

    // tg: code/game/objects/items/handcuffs.dm:628
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(dot) || !iscarbon(hit_atom)) //if it gets caught or the target can't be cuffed,
            return dot; //abort
        //The mob has been hit, save the reference for ensnaring
        ensnare_mob_ref = WEAKREF(hit_atom as datum) as datum_weakref;
        return dot;
    }

    // tg: code/game/objects/items/handcuffs.dm:634
    public override async DmTask<object> after_throw(datum_callback callback = null)
    {
        object dot = await base.after_throw(callback);
        if (ensnare_mob_ref == null)
            return dot;
        atom ensnare_mob = ensnare_mob_ref.resolve() as atom;
        if (ensnare_mob != null)
            await ensnare(ensnare_mob);
        ensnare_mob_ref = null;
        return dot;
    }

    /**
     * Attempts to legcuff someone with the bola
     *
     * Arguments:
     * * snared_mob - the carbon that we will try to ensnare
     */
    // tg: code/game/objects/items/handcuffs.dm:649
    public virtual async DmTask ensnare(atom snared_mob = null)
    {
        mob_living_carbon snared = snared_mob as mob_living_carbon;
        if (Dm.truthy(snared.legcuffed) || snared.num_legs < 2)
            return;
        visible_message(span_danger(I18n.T("\\The [] ensnares []!", this, snared)), span_userdanger(I18n.T("\\The [] ensnares you!", this)));
        await snared.equip_to_slot(this, ITEM_SLOT_LEGCUFFED);
        SSblackbox.record_feedback("tally", "handcuffs", 1, GetType());
        await snared.Knockdown(knockdown);
        playsound(this, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
    }
}

/**
 * A security variant of the bola.
 *
 * It's harder to remove, smaller and has a defined price.
 */
// tg: code/game/objects/items/handcuffs.dm:677
public partial class obj_item_restraints_legcuffs_bola_energy : obj_item_restraints_legcuffs_bola
{
    // tg: code/game/objects/items/handcuffs.dm:688
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_UNCATCHABLE, TRAIT_GENERIC); // People said energy bolas being uncatchable is a feature.
        return dot;
    }

    // tg: code/game/objects/items/handcuffs.dm:692
    public override async DmTask ensnare(atom hit_atom = null)
    {
        obj_item_restraints_legcuffs_beartrap_energy_cyborg B = Dm.@new<obj_item_restraints_legcuffs_beartrap_energy_cyborg>(get_turf(hit_atom));
        await B.spring_trap(hit_atom as atom_movable, ignore_movetypes: TRUE, hit_prone: TRUE);
        if (B.loc != hit_atom)
            qdel(B);
        qdel(this);
    }
}

/**
 * A pacifying variant of the bola.
 *
 * It's much harder to remove, doesn't cause a slowdown and gives people /datum/status_effect/gonbola_pacify.
 */
// tg: code/game/objects/items/handcuffs.dm:704
public partial class obj_item_restraints_legcuffs_bola_gonbola : obj_item_restraints_legcuffs_bola
{
    // tg: code/game/objects/items/handcuffs.dm:714
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (iscarbon(hit_atom))
        {
            var C = (mob_living_carbon)hit_atom;
            effectReference = Waiting.NoSleep(C.apply_status_effect(typeof(datum_status_effect_gonbola_pacify))) as datum_status_effect_gonbola_pacify;
        }
        return dot;
    }

    // tg: code/game/objects/items/handcuffs.dm:720
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (effectReference != null)
            QDEL_NULL(ref effectReference);
        return dot;
    }
}

// The static lists of the restraints (rules 10.6: type-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/game/objects/items/handcuffs.dm:233  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/bola, /datum/crafting_recipe/gonbola)
    internal DmList obj_item_restraints_handcuffs_cable__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_bola), typeof(datum_crafting_recipe_gonbola));

    // tg: code/game/objects/items/handcuffs.dm:450  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(trap_stepped_on))
    internal DmList obj_item_restraints_legcuffs_beartrap__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_item_restraints_legcuffs_beartrap.trap_stepped_on)));
}
