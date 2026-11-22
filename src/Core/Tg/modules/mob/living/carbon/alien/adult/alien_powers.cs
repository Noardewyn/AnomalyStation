// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/alien_powers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the carbon's plasma vessel helpers (gaps P6, D298: the implantable thrusters read them, :381-395) and (gaps X5,
// D333) the xenomorphs' powers: weeds, whisper, plasma transfer, the corrosive acid, the neurotoxin spit, the resin shapes, the
// sentinel's sneak and the regurgitation. The var blocks are generated (D111).

/*NOTES:
These are general powers. Specific powers are stored under the appropriate alien creature type.
*/

/*Alien spit now works like a taser shot. It won't home in on the target but will act the same once it does hit.
Doesn't work on other aliens/AI.*/

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:9
public partial class datum_action_cooldown_alien : datum_action_cooldown
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:20
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        //not free
        if (plasma_cost != 0)
            name = I18n.T("[] ([]P)", Dm.initial(this, (datum_action p) => p.name), plasma_cost);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:26
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!iscarbon(owner))
            return FALSE;
        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        if (Dm.num(carbon_owner.getPlasma()) < plasma_cost)
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:38
    public override async DmTask<object> PreActivate(atom target = null)
    {
        // Parent calls Activate(), so if parent returns TRUE,
        // it means the activation happened successfuly by this point
        object dot = await base.PreActivate(target);
        if (!Dm.truthy(dot))
            return FALSE;
        // Xeno actions like "evolve" may result in our action (or our alien) being deleted
        // In that case, we can just exit now as a "success"
        if (QDELETED(this) || QDELETED(owner))
            return TRUE;

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        await carbon_owner.adjustPlasma(-plasma_cost);
        // It'd be really annoying if click-to-fire actions stayed active,
        // even if our plasma amount went under the required amount.
        if (Dm.truthy(click_to_activate) && Dm.num(carbon_owner.getPlasma()) < plasma_cost)
            await unset_click_ability(owner, refund_cooldown: FALSE);

        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:58
public partial class datum_action_cooldown_alien_make_structure : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:62
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!isturf(owner.loc) || isspaceturf(owner.loc))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:71
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (!Dm.truthy(check_for_duplicate()))
            return FALSE;

        if (!Dm.truthy(await check_for_vents()))
            return FALSE;

        return await base.PreActivate(target);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:80
    public override async DmTask<object> Activate(atom target = null)
    {
        Dm.@new(made_structure_type, owner.loc);
        return TRUE;
    }

    /// Checks if there's a duplicate structure in the owner's turf
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:85
    public virtual object check_for_duplicate()
    {
        obj_structure existing_thing = Dm.locate_type(made_structure_type, owner.loc) as obj_structure;
        if (existing_thing != null)
        {
            to_chat(owner, span_warning(I18n.T("There is already \\a [] here!", existing_thing)));
            return FALSE;
        }

        return TRUE;
    }

    /// Checks if there's an atmos machine (vent) in the owner's turf
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:94
    public virtual async DmTask<object> check_for_vents()
    {
        obj_machinery_atmospherics_components_unary atmos_thing = Dm.locate<obj_machinery_atmospherics_components_unary>(owner.loc);
        if (atmos_thing != null)
        {
            object are_you_sure = await tgui_alert(owner, I18n.T("Laying eggs and shaping resin here would block access to []. Do you want to continue?", atmos_thing), I18n.T("Blocking Atmospheric Component"), Dm.list("Yes", "No"));
            if (!Dm.eq(are_you_sure, "Yes"))
                return FALSE;
            if (QDELETED(this) || QDELETED(owner) || !Dm.truthy(check_for_duplicate()))
                return FALSE;
        }

        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:105
public partial class datum_action_cooldown_alien_make_structure_plant_weeds : datum_action_cooldown_alien_make_structure
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:112
    public override async DmTask<object> Activate(atom target = null)
    {
        owner.visible_message(span_alertalien(I18n.T("[] plants some alien weeds!", owner)));
        return await base.Activate(target);
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:116
public partial class datum_action_cooldown_alien_whisper : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:122
    public override async DmTask<object> Activate(atom target = null)
    {
        DmList possible_recipients = Dm.list();
        foreach (mob_living recipient in Dm.each<mob_living>(oview(owner)))
            Dm.append(possible_recipients, recipient);

        if (!Dm.truthy(Dm.length(possible_recipients)))
        {
            to_chat(owner, span_noticealien(I18n.T("There's no one around to whisper to.")));
            return FALSE;
        }

        mob_living chosen_recipient = await tgui_input_list(owner, I18n.T("Select whisper recipient"), I18n.T("Whisper"), sort_names(possible_recipients) as DmList) as mob_living;
        if (chosen_recipient == null)
            return FALSE;

        object to_whisper = await tgui_input_text(owner, title: I18n.T("Alien Whisper"), max_length: MAX_MESSAGE_LEN);
        if (QDELETED(chosen_recipient) || QDELETED(this) || QDELETED(owner) || !Dm.truthy(await IsAvailable(feedback: TRUE)) || !Dm.truthy(to_whisper))
            return FALSE;
        if (Dm.truthy(chosen_recipient.can_block_magic(MAGIC_RESISTANCE_MIND, charge_cost: 0)))
        {
            to_chat(owner, span_warning(I18n.T("As you reach into []'s mind, you are stopped by a mental blockage. It seems you've been foiled.", chosen_recipient)));
            return FALSE;
        }

        log_directed_talk(owner, chosen_recipient, to_whisper, LOG_SAY, tag: "alien whisper");
        to_chat(chosen_recipient, Dm.text("[][]", span_noticealien(I18n.T("You hear a strange, alien voice in your head...")), to_whisper));
        to_chat(owner, span_noticealien(I18n.T("You said: \"[]\" to []", to_whisper, chosen_recipient)));
        foreach (mob dead_mob in Dm.each(GLOB.dead_mob_list))
        {
            if (!isobserver(dead_mob))
                continue;
            string follow_link_user = FOLLOW_LINK(dead_mob, owner);
            string follow_link_whispee = FOLLOW_LINK(dead_mob, chosen_recipient);
            to_chat(dead_mob, Dm.text("[] [] [] [] [] []", follow_link_user, span_name(Dm.text("[]", owner)), span_alertalien(I18n.T("Alien Whisper --> ")), follow_link_whispee, span_name(Dm.text("[]", chosen_recipient)), span_noticealien(Dm.text("[]", to_whisper))));
        }

        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:154
public partial class datum_action_cooldown_alien_transfer : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:160
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        DmList aliens_around = Dm.list();
        foreach (mob_living_carbon alien in Dm.each<mob_living_carbon>(view(owner)))
        {
            if (Dm.eq(alien.getPlasma(), -1) || alien == owner)
                continue;
            Dm.append(aliens_around, alien);
        }

        if (!Dm.truthy(Dm.length(aliens_around)))
        {
            to_chat(owner, span_noticealien(I18n.T("There are no other aliens around.")));
            return FALSE;
        }

        mob_living_carbon donation_target = await tgui_input_list(owner, I18n.T("Target to transfer to"), I18n.T("Plasma Donation"), sort_names(aliens_around) as DmList) as mob_living_carbon;
        if (donation_target == null)
            return FALSE;

        object amount = await tgui_input_number(owner, I18n.T("Amount"), I18n.T("Transfer Plasma to []", donation_target), max_value: Dm.num(carbon_owner.getPlasma()));
        if (QDELETED(donation_target) || QDELETED(this) || QDELETED(owner) || !Dm.truthy(await IsAvailable(feedback: TRUE)) || amount == null || Dm.num(amount) <= 0)
            return FALSE;

        if (Dm.num(get_dist(owner, donation_target)) > 1)
        {
            to_chat(owner, span_noticealien(I18n.T("You need to be closer!")));
            return FALSE;
        }

        await donation_target.adjustPlasma(amount);
        await carbon_owner.adjustPlasma(-Dm.num(amount));

        to_chat(donation_target, span_noticealien(I18n.T("[] has transferred [] plasma to you.", owner, amount)));
        to_chat(owner, span_noticealien(I18n.T("You transfer [] plasma to [].", amount, donation_target)));
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:195
public partial class datum_action_cooldown_alien_acid_corrosion : datum_action_cooldown_alien_acid
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:205
    public override async DmTask<object> set_click_ability(mob on_who = null)
    {
        object dot = await base.set_click_ability(on_who);
        if (!Dm.truthy(dot))
            return dot;

        to_chat(on_who, span_noticealien(I18n.T("You prepare to vomit acid. <b>Click a target to acid it!</b>")));
        await on_who.update_icons();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:213
    public override async DmTask<object> unset_click_ability(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        object dot = await base.unset_click_ability(on_who, refund_cooldown);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(refund_cooldown))
            to_chat(on_who, span_noticealien(I18n.T("You empty your corrosive acid glands.")));
        await on_who.update_icons();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:222
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (Dm.num(get_dist(owner, target)) > 1)
            return FALSE;
        if (ismob(target)) //If it could corrode mobs, it would one-shot them.
        {
            owner.balloon_alert(owner, I18n.T("doesn't work on creatures!"));
            return FALSE;
        }

        return await base.PreActivate(target);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:231
    public override async DmTask<object> Activate(atom target = null)
    {
        if (isturf(target))
            await target.AddComponent(typeof(datum_component_acid), corrosion_acid_power, corrosion_acid_volume, GLOB.acid_overlay, typeof(particles_acid), Dm.named("turf_acid_ignores_mobs", TRUE));
        else if (!Dm.truthy(await target.acid_act(corrosion_acid_power, corrosion_acid_volume)))
        {
            to_chat(owner, span_noticealien(I18n.T("You cannot dissolve this object.")));
            return FALSE;
        }

        owner.visible_message(
            span_alertalien(I18n.T("[] vomits globs of vile stuff all over []. It begins to sizzle and melt under the bubbling mess of acid!", owner, target)),
            span_noticealien(I18n.T("You vomit globs of acid over []. It begins to sizzle and melt.", target)));
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:244
public partial class datum_action_cooldown_alien_acid_neurotoxin : datum_action_cooldown_alien_acid
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:250
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        mob_living_carbon as_carbon = owner as mob_living_carbon;
        if (as_carbon is mob_living_carbon && Dm.truthy(as_carbon.is_mouth_covered(ITEM_SLOT_MASK)))
            return FALSE;
        if (!isturf(owner.loc))
            return FALSE;
        return await base.IsAvailable(feedback);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:258
    public override async DmTask<object> set_click_ability(mob on_who = null)
    {
        object dot = await base.set_click_ability(on_who);
        if (!Dm.truthy(dot))
            return dot;

        to_chat(on_who, span_notice(I18n.T("You prepare your neurotoxin gland. <B>Left-click to fire at a target!</B>")));

        button_icon_state = "alien_neurotoxin_1";
        await build_all_button_icons();
        await on_who.update_icons();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:269
    public override async DmTask<object> unset_click_ability(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        object dot = await base.unset_click_ability(on_who, refund_cooldown);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(refund_cooldown))
            to_chat(on_who, span_notice(I18n.T("You empty your neurotoxin gland.")));

        button_icon_state = "alien_neurotoxin_0";
        await build_all_button_icons();
        await on_who.update_icons();
        return dot;
    }

    // We do this in InterceptClickOn() instead of Activate()
    // because we use the click parameters for aiming the projectile
    // (or something like that)
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:284
    public override async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom target = null)
    {
        object dot = await base.InterceptClickOn(clicker, @params, target);
        if (!Dm.truthy(dot))
        {
            await unset_click_ability(clicker, refund_cooldown: FALSE);
            return FALSE;
        }

        DmList modifiers = params2list(@params);
        clicker.visible_message(
            span_danger(I18n.T("[] spits neurotoxin!", clicker)),
            span_alertalien(I18n.T("You spit neurotoxin.")));
        obj_projectile_neurotoxin neurotoxin = Dm.@new<obj_projectile_neurotoxin>(clicker.loc);
        await neurotoxin.aim_projectile(target, clicker, modifiers);
        neurotoxin.firer = clicker;
        await neurotoxin.fire();
        await clicker.newtonian_move(get_angle(target, clicker));
        return TRUE;
    }

    // Has to return TRUE, otherwise is skipped.
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:303
    public override async DmTask<object> Activate(atom target = null)
    {
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:306
public partial class datum_action_cooldown_alien_make_structure_resin : datum_action_cooldown_alien_make_structure
{
    /// A list of all structures we can make.
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:312  var/static/list/structures
    public static ref DmList structures => ref TgState.Current.datum_action_cooldown_alien_make_structure_resin__structures;

    // Snowflake to check for multiple types of alien resin structures
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:319
    public override object check_for_duplicate()
    {
        foreach (object blocker_name in Dm.each(structures))
        {
            Type blocker_type = structures[blocker_name] as Type;
            if (Dm.locate_type(blocker_type, owner.loc) != null)
            {
                to_chat(owner, span_warning(I18n.T("There is already a resin structure there!")));
                return FALSE;
            }
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:328
    public override async DmTask<object> Activate(atom target = null)
    {
        object choice = await tgui_input_list(owner, I18n.T("Select a shape to build"), I18n.T("Resin building"), structures);
        if (choice == null || QDELETED(this) || QDELETED(owner) || !Dm.truthy(check_for_duplicate()) || !Dm.truthy(await IsAvailable(feedback: TRUE)))
            return FALSE;

        Type choice_path = structures[choice] as Type;
        if (!ispath(choice_path))
            return FALSE;

        owner.visible_message(
            span_notice(I18n.T("[] vomits up a thick purple substance and begins to shape it.", owner)),
            span_notice(I18n.T("You shape a [] out of resin.", choice)));

        Dm.@new(choice_path, owner.loc);
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:354
public partial class datum_action_cooldown_alien_regurgitate : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:362
    public override async DmTask<object> Activate(atom target = null)
    {
        if (!iscarbon(owner))
            return null;
        mob_living_carbon_alien_adult alieninated_owner = owner as mob_living_carbon_alien_adult;
        obj_item_organ_stomach_alien melting_pot = alieninated_owner.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_alien;
        if (melting_pot == null)
        {
            owner.visible_message(span_clown(I18n.T("[] gags, and spits up a bit of purple liquid. Ewwww.", this)),
                span_alien(I18n.T("You feel a pain in your... chest? There's nothing there there's nothing there no no n-")));
            return null;
        }

        if (!Dm.truthy(Dm.length(melting_pot.stomach_contents)))
        {
            to_chat(owner, span_alien(I18n.T("There's nothing in your stomach, what exactly do you plan on spitting up?")));
            return null;
        }
        owner.visible_message(span_danger(I18n.T("[] hurls out the contents of their stomach!", owner)));
        float dir_angle = Dm.num(dir2angle(owner.dir));

        playsound(owner, Dm.resource("sound/mobs/non-humanoids/alien/alien_york.ogg"), 100);
        await melting_pot.eject_stomach(slice_off_turfs(owner, border_diamond_range_turfs(owner, 9), dir_angle - angle_delta, dir_angle + angle_delta), 4, mob_speed, spit_speed);
        return null;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:381 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    /// Gets the plasma level of this carbon's plasma vessel, or -1 if they don't have one
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:382
    public virtual object getPlasma()
    {
        obj_item_organ_alien_plasmavessel vessel = get_organ_by_type(typeof(obj_item_organ_alien_plasmavessel)) as obj_item_organ_alien_plasmavessel;
        if (vessel == null)
            return -1;
        return vessel.stored_plasma;
    }

    /// Adjusts the plasma level of the carbon's plasma vessel if they have one
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:389
    public virtual async DmTask<object> adjustPlasma(object amount = null)
    {
        obj_item_organ_alien_plasmavessel vessel = get_organ_by_type(typeof(obj_item_organ_alien_plasmavessel)) as obj_item_organ_alien_plasmavessel;
        if (vessel == null)
            return FALSE;
        vessel.adjust_plasma(amount);
        foreach (datum_action_cooldown_alien ability in Dm.each<datum_action_cooldown_alien>(actions))
            await ability.build_all_button_icons();
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/alien/adult/alien_powers.dm:312
    internal DmList datum_action_cooldown_alien_make_structure_resin__structures = Dm.list(
        Dm.kv("resin wall", typeof(obj_structure_alien_resin_wall)),
        Dm.kv("resin membrane", typeof(obj_structure_alien_resin_membrane)),
        Dm.kv("resin nest", typeof(obj_structure_bed_nest)));
}
