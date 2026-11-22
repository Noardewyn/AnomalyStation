// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the suicide check (can_suicide waits for the suicide verbs, M10). The var block
// is hand-written: the generator types `overlay` and `theme` by their names as types; here they hold the overlay and the theme datum.
// The time stop's signals are registered as tg's (the time stop spell is the wizard's, not ported).

/**
 * A mob which acts as a guardian angel to another mob, sharing health but protecting them using special powers.
 * Usually either obtained in magical form by a wizard, or technological form by a traitor. Sometimes found by miners.
 */
// tg: code/modules/mob/living/basic/guardian/guardian.dm:5
[DmPath("/mob/living/basic/guardian")]
public partial class mob_living_basic_guardian : mob_living_basic
{
    /// Coloured overlay we apply
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:53
    public mutable_appearance overlay;

    /// How are we themed?
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:67
    public datum_guardian_fluff theme;

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:6
    public mob_living_basic_guardian()
    {
        name = "Guardian Spirit";
        real_name = "Guardian Spirit";
        desc = "A mysterious being that stands by its charge, ever vigilant.";
        icon = Dm.resource("icons/mob/nonhuman-player/guardian.dmi");
        icon_state = "magicbase";
        icon_living = "magicbase";
        icon_dead = "magicbase";
        gender = NEUTER;
        basic_mob_flags = DEL_ON_DEATH;
        mob_biotypes = MOB_SPECIAL;
        sentience_type = SENTIENCE_HUMANOID;
        hud_type = typeof(datum_hud_guardian);
        speed = 0;
        status_flags = CANPUSH;
        maxHealth = INFINITY; // The spirit itself is invincible and passes damage to its host
        health = INFINITY;
        unsuitable_atmos_damage = 0;
        unsuitable_cold_damage = 0;
        unsuitable_heat_damage = 0;
        bubble_icon = "guardian";
        response_help_continuous = "passes through";
        response_help_simple = "pass through";
        response_disarm_continuous = "flails at";
        response_disarm_simple = "flail at";
        response_harm_continuous = "punches";
        response_harm_simple = "punch";
        attack_sound = Dm.resource("sound/items/weapons/punch1.ogg");
        attack_verb_continuous = "punches";
        attack_verb_simple = "punch";
        obj_damage = 40;
        melee_damage_lower = 15;
        melee_damage_upper = 15;
        melee_attack_cooldown = CLICK_CD_MELEE;
        light_system = OVERLAY_LIGHT;
        light_range = 3;
        light_on = FALSE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:22
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        physiology = Dm.list(Dm.kv(STAMINA, 0)); // tg: code/modules/mob/living/basic/guardian/guardian.dm:22
        speak_emote = Dm.list("hisses"); // tg: code/modules/mob/living/basic/guardian/guardian.dm:26
    }

    /// List of actions we give to our summoner.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:79
    private static DmList control_actions => TgState.Current.mob_living_basic_guardian__control_actions ??= Dm.list(
        typeof(datum_action_cooldown_mob_cooldown_guardian_comms),
        typeof(datum_action_cooldown_mob_cooldown_recall_guardian),
        typeof(datum_action_cooldown_mob_cooldown_replace_guardian));

    /// List of actions we give to ourselves.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:85
    protected static DmList self_actions => TgState.Current.mob_living_basic_guardian__self_actions ??= Dm.list(
        typeof(datum_action_cooldown_guardian_check_type),
        typeof(datum_action_cooldown_guardian_toggle_light),
        typeof(datum_action_cooldown_guardian_communicate),
        typeof(datum_action_cooldown_guardian_manifest),
        typeof(datum_action_cooldown_guardian_recall));

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:93
    [DmParams("mapload", "theme")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_guardian_fluff theme = Dm.arg(__extra, 0) as datum_guardian_fluff;
        object dot = base.Initialize(mapload);
        Dm.append(GLOB.parasites, this);
        this.theme = theme;
        theme?.apply(this);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_temp_visual_guardian_phase_out)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        // life link
        update_appearance(UPDATE_ICON);
        Waiting.NoSleep(manifest_effects());
        Waiting.NoSleep(create_actions());
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:105
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.parasites, this);
        if (Dm.truthy(is_deployed()))
            Waiting.NoSleep(recall_effects());
        Waiting.NoSleep(cut_summoner(different_person: TRUE));
        return base.Destroy(force);
    }

    ///Creates the guardian's default action buttons and sets them to go in their proper location.
    ///Subtypes overwrite this for special ability types and whatnot.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:114
    public virtual async DmTask create_actions()
    {
        foreach (object action_type in Dm.each(Dm.add(self_actions, toggle_button_type)))
        {
            if (action_type == null) //no toggle button type
                continue;
            if (Dm.locate_type((Type)action_type, actions) != null)
                continue;
            datum_action new_action = Dm.@new((Type)action_type, this) as datum_action;
            await new_action.Grant(this);
        }
        await update_action_buttons();
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:124
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:128
    public override async DmTask<object> Login() //if we have a mind, set its name to ours when it logs in
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        if (summoner == null)
        {
            to_chat(this, span_boldholoparasite("For some reason, somehow, you have no summoner. Please report this bug immediately."));
            stack_trace("Guardian created with client but no summoner.", "code/modules/mob/living/basic/guardian/guardian.dm", 133);
        }
        else
        {
            to_chat(this, span_holoparasite(Dm.text("You are a <b>[]</b>, bound to serve [].", theme.name, summoner.real_name)));
            to_chat(this, span_holoparasite(Dm.text("You are capable of manifesting or recalling to your master with the buttons on your HUD. You will also find a button to communicate with [] privately there.", summoner.p_them())));
            to_chat(this, span_holoparasite(Dm.text("While personally invincible, you will die if [] does, and any damage dealt to you will have a portion passed on to [] as you feed upon [] to sustain yourself.", summoner.real_name, summoner.p_them(), summoner.p_them())));
        }
        to_chat(this, playstyle_string);
        if (guardian_colour != null)
            return dot; // Already set up so we don't need to do it again
        locked = TRUE;
        await guardian_rename();
        await guardian_recolour();
        locked = FALSE;
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:147
    public override async DmTask mind_initialize()
    {
        await base.mind_initialize();
        if (summoner == null)
        {
            to_chat(this, span_boldholoparasite("For some reason, somehow, you have no summoner. Please report this bug immediately."));
            return;
        }
        await mind.enslave_mind_to_creator(summoner); // Once our mind is created, we become enslaved to our summoner. cant be done in the first run of set_summoner, because by then we dont have a mind yet.
    }

    /// Pick a new colour for our guardian
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:155
    public virtual async DmTask<object> guardian_recolour()
    {
        if (client == null)
            return null;
        object chosen_guardian_colour = await tgui_color_picker(this, "What would you like your colour to be?", "Choose Your Colour", COLOR_WHITE);
        if (chosen_guardian_colour == null) //redo proc until we get a color
        {
            to_chat(this, span_warning("Invalid colour, please try again."));
            return await guardian_recolour();
        }
        await set_guardian_colour(chosen_guardian_colour);
        return null;
    }

    /// Apply a new colour to our guardian
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:165
    public virtual async DmTask set_guardian_colour(object colour = null)
    {
        guardian_colour = colour;
        set_light_color(guardian_colour);
        if (overlay != null)
            overlay.color = guardian_colour;
        update_appearance(UPDATE_ICON);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:171
    public virtual async DmTask<object> guardian_rename()
    {
        if (client == null)
            return null;

        object new_name = await sanitize_name(reject_bad_text(await tgui_input_text(this, "What would you like your name to be?", "Choose Your Name", generate_random_name(), MAX_NAME_LEN)));
        if (!Dm.truthy(new_name)) //redo proc until we get a good name
        {
            to_chat(this, span_warning("Invalid name, please try again."));
            return await guardian_rename();
        }
        to_chat(this, span_notice(Dm.text("Your new name [] anchors itself in your mind.", span_name(Dm.str(new_name)))));
        await fully_replace_character_name(null, new_name, log_new_name: TRUE);
        return null;
    }

    /// Picks a random name as a suggestion
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:183
    public virtual object generate_random_name()
    {
        DmList surname_options = Dm.list("Guardian"); // Fallback in case you define a guardian with no theme
        object sw__1 = theme?.fluff_type;
        if (Dm.eq(sw__1, GUARDIAN_MAGIC))
            surname_options = GLOB.guardian_fantasy_surnames;
        else if (Dm.eq(sw__1, GUARDIAN_TECH))
            surname_options = GLOB.guardian_tech_surnames;

        return Dm.text("[] []", pick(GLOB.guardian_first_names), pick(surname_options));
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:193
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        if (!Dm.truthy(is_deployed()))
        {
            balloon_alert(this, "not tangible!");
            return FALSE;
        }
        return await base.melee_attack(target, modifiers, ignore_cooldown);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:199
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!QDELETED(summoner))
        {
            to_chat(summoner, span_bolddanger(Dm.text("Your [] died somehow!", name)));
            await summoner.dust();
        }
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:205
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object sw__1 = severity;
        if (Dm.eq(sw__1, EXPLODE_DEVASTATE))
        {
            investigate_log("has been gibbed by an explosion.", INVESTIGATE_DEATHS);
            Waiting.NoSleep(gib());
            return TRUE;
        }
        else if (Dm.eq(sw__1, EXPLODE_HEAVY))
            Waiting.NoSleep(adjust_brute_loss(60));
        else if (Dm.eq(sw__1, EXPLODE_LIGHT))
            Waiting.NoSleep(adjust_brute_loss(30));

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:218
    public override async DmTask gib(float? drop_bitflags = null)
    {
        await death(TRUE);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:221
    public override async DmTask dust(object just_ash = null, object drop_items = null, object give_moodlet = null, object force = null)
    {
        await death(TRUE);
    }

    /// Link up with a summoner mob.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:225
    public virtual async DmTask set_summoner(mob_living to_who = null, float? different_person = null)
    {
        different_person ??= FALSE;
        if (QDELETED(this))
            return; // Just in case
        if (QDELETED(to_who))
        {
            ghostize(FALSE);
            qdel(this); // No life of free invulnerability for you.
            return;
        }
        await cut_summoner(different_person);
        await AddComponent(typeof(datum_component_life_link), to_who, Dm.@new<datum_callback>(this, nameof(on_harm)), Dm.@new<datum_callback>(this, nameof(on_summoner_death)));
        summoner = to_who;

        foreach (Type action_type in Dm.each_cast<Type>(control_actions))
        {
            if (Dm.locate_type(action_type, summoner.actions) != null)
                continue;
            datum_action new_action = Dm.@new(action_type, summoner) as datum_action;
            await new_action.Grant(summoner);
        }

        if (Dm.truthy(different_person))
        {
            if (mind != null)
                await mind.enslave_mind_to_creator(to_who);
            else //mindless guardian, manually give them factions
            {
                add_faction(summoner.get_faction());
                add_ally(summoner.allies);
                summoner.add_ally(this);
            }
        }
        remove_all_languages(LANGUAGE_MASTER);
        copy_languages(to_who, LANGUAGE_MASTER); // make sure holoparasites speak same language as master
        RegisterSignal(to_who, COMSIG_QDELETING, nameof(on_summoner_deletion));
        RegisterSignal(to_who, COMSIG_LIVING_ON_WABBAJACKED, nameof(on_summoner_wabbajacked));
        RegisterSignal(to_who, COMSIG_LIVING_SHAPESHIFTED, nameof(on_summoner_shapeshifted));
        RegisterSignal(to_who, COMSIG_LIVING_UNSHAPESHIFTED, nameof(on_summoner_unshapeshifted));
        RegisterSignal(to_who, COMSIG_ATOM_STOPPING_TIME, nameof(on_summoner_timestop));
        RegisterSignal(to_who, SIGNAL_ADDTRAIT(TRAIT_TIME_STOP_IMMUNE), nameof(on_summoner_timestop_immune_gained));
        RegisterSignal(to_who, SIGNAL_REMOVETRAIT(TRAIT_TIME_STOP_IMMUNE), nameof(on_summoner_timestop_immune_removed));
        if (HAS_TRAIT(to_who, TRAIT_TIME_STOP_IMMUNE))
            ADD_TRAIT(this, TRAIT_TIME_STOP_IMMUNE, REF(summoner));
        await recall(forced: TRUE);
        await leash_to(this, summoner);
        if (to_who.stat == DEAD)
            await on_summoner_death(this, to_who);
        await summoner.updatehealth();
    }

    /// Remove all references to our summoner
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:267
    public virtual async DmTask cut_summoner(float? different_person = null)
    {
        different_person ??= FALSE;
        if (summoner == null)
            return;
        if (Dm.truthy(is_deployed()))
            await recall_effects();
        turf summoner_turf = get_turf(summoner) as turf;
        if (summoner_turf != null)
            await forceMove(summoner_turf);
        unleash();
        UnregisterSignal(summoner, Dm.list(
            COMSIG_QDELETING,
            COMSIG_LIVING_ON_WABBAJACKED,
            COMSIG_LIVING_SHAPESHIFTED,
            COMSIG_LIVING_UNSHAPESHIFTED,
            COMSIG_ATOM_STOPPING_TIME,
            SIGNAL_ADDTRAIT(TRAIT_TIME_STOP_IMMUNE),
            SIGNAL_REMOVETRAIT(TRAIT_TIME_STOP_IMMUNE)
        ));
        REMOVE_TRAIT(this, TRAIT_TIME_STOP_IMMUNE, REF(summoner));
        if (Dm.truthy(different_person))
        {
            summoner.remove_ally(this);
            remove_faction(summoner.get_faction());
            if (mind != null)
                await mind.remove_all_antag_datums();
        }
        if (!Dm.truthy(length(Dm.sub(summoner.get_all_linked_holoparasites(), this))))
        {
            foreach (Type action_type in Dm.each_cast<Type>(control_actions))
            {
                datum_action remove_action = Dm.locate_type(action_type, summoner.actions) as datum_action;
                if (remove_action == null)
                    continue;
                await remove_action.Remove(summoner);
            }
        }
        summoner = null;
    }

    /// Connects these two mobs by a leash
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:299
    public virtual async DmTask leash_to(atom_movable leashed = null, atom_movable leashed_to = null)
    {
        await leashed.AddComponent(
            typeof(datum_component_leash),
            Dm.named("owner", leashed_to),
            Dm.named("distance", range),
            Dm.named("force_teleport_out_effect", typeof(obj_effect_temp_visual_guardian_phase_out)),
            Dm.named("force_teleport_in_effect", typeof(obj_effect_temp_visual_guardian_phase))
        );
    }

    /// Removes the leash from this guardian
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:309
    public virtual void unleash()
    {
        qdel(GetComponent(typeof(datum_component_leash)));
    }

    /// Called when our owner dies. We fucked up, so now neither of us get to exist.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:313
    public virtual async DmTask on_summoner_death(mob_living source = null, mob_living former_owner = null)
    {
        await cut_summoner();
        if (former_owner.loc != null)
            await forceMove(former_owner.loc);
        to_chat(this, span_danger("Your summoner has died!"));
        visible_message(span_bolddanger(Dm.text("\\The [] dies along with its user!", this)));
        former_owner.visible_message(span_bolddanger(Dm.text("[]'s body is completely consumed by the strain of sustaining []!", former_owner, this)));
        await former_owner.dust(drop_items: TRUE);
    }

    /// Called when our health changes, inform our owner of why they are getting hurt (if they are)
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:323
    public virtual async DmTask on_harm(mob_living source = null, mob_living summoner = null, object amount = null)
    {
        if (QDELETED(this) || QDELETED(summoner) || Dm.num(amount) <= 2)
            return;
        to_chat(summoner, span_bolddanger(Dm.text("[] is under attack! You take damage!", name)));
        summoner.visible_message(span_bolddanger(Dm.text("Blood sprays from [] as [] takes damage!", summoner, this)));
        if (summoner.stat >= HARD_CRIT)
        {
            to_chat(summoner, span_bolddanger(Dm.text("Your head pounds, you can't take the strain of sustaining [] in this condition!", this)));
            await summoner.adjust_organ_loss(ORGAN_SLOT_BRAIN, Dm.num(amount) * 0.5f);
        }
    }

    /// When our owner is deleted, we go too.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:333
    [SignalHandler]
    public virtual void on_summoner_deletion(mob_living source = null)
    {
        Waiting.NoSleep(cut_summoner());
        to_chat(this, span_danger("Your summoner is gone, you feel yourself fading!"));
        ghostize(FALSE);
        qdel(this);
    }

    /// Signal proc for [COMSIG_LIVING_ON_WABBAJACKED], when our summoner is wabbajacked we should be alerted.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:341
    [SignalHandler]
    public virtual void on_summoner_wabbajacked(mob_living source = null, mob_living new_mob = null)
    {
        Waiting.NoSleep(set_summoner(new_mob));
        to_chat(this, span_holoparasite("Your summoner has changed form!"));
    }

    /// Signal proc for [COMSIG_LIVING_SHAPESHIFTED], when our summoner is shapeshifted we should change to the new mob
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:347
    [SignalHandler]
    public virtual void on_summoner_shapeshifted(mob_living source = null, mob_living new_shape = null)
    {
        Waiting.NoSleep(set_summoner(new_shape));
        to_chat(this, span_holoparasite(Dm.text("Your summoner has shapeshifted into that of a []!", new_shape)));
    }

    /// Signal proc for [COMSIG_LIVING_UNSHAPESHIFTED], when our summoner unshapeshifts go back to that mob
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:353
    [SignalHandler]
    public virtual void on_summoner_unshapeshifted(mob_living source = null, mob_living old_summoner = null)
    {
        Waiting.NoSleep(set_summoner(old_summoner));
        to_chat(this, span_holoparasite("Your summoner has shapeshifted back into their normal form!"));
    }

    /// Signal proc for [COMSIG_ATOM_STOPPING_TIME] - when our summoner timestops, we should share their immunity.
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:359
    [SignalHandler]
    public virtual void on_summoner_timestop(mob_living source = null, datum_action_cooldown_spell_timestop timestop = null, DmList immune_atoms = null)
    {
        if (Dm.truthy(timestop.owner_is_immune_to_self_timestop))
            Dm.combine(immune_atoms, this);
    }

    /// Signal proc for [SIGNAL_ADDTRAIT(TRAIT_TIME_STOP_IMMUNE)] - when our summoner gains time stop immunity, we should too
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:365
    [SignalHandler]
    public virtual void on_summoner_timestop_immune_gained(mob_living source = null, params object[] __extra)
    {
        ADD_TRAIT(this, TRAIT_TIME_STOP_IMMUNE, REF(summoner));
    }

    /// Signal proc for [SIGNAL_REMOVETRAIT(TRAIT_TIME_STOP_IMMUNE)] - when our summoner loses time stop immunity, we should too
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:370
    [SignalHandler]
    public virtual void on_summoner_timestop_immune_removed(mob_living source = null, params object[] __extra)
    {
        REMOVE_TRAIT(this, TRAIT_TIME_STOP_IMMUNE, REF(summoner));
    }

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:374
    public override async DmTask<object> wabbajack(object what_to_randomize = null, float? change_flags = null)
    {
        visible_message(span_warning(Dm.text("[] resists the polymorph!", this))); // Ha, no
        await DmTask.CompletedTask;
        return null;
    }

    /// Returns true if you are out and about
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:381
    public virtual object is_deployed()
    {
        return Dm.num(summoner == null || loc != summoner);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/guardian/guardian.dm:79  var/static/list/control_actions
    internal DmList mob_living_basic_guardian__control_actions;

    // tg: code/modules/mob/living/basic/guardian/guardian.dm:85  var/static/list/self_actions
    internal DmList mob_living_basic_guardian__self_actions;
}
