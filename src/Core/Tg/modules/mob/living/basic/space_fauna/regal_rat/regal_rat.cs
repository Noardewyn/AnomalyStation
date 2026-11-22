// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4): the ghost control, the door prying and poster tearing are inert elements (rows).

/// The cheesiest, most crowned rat of them all. Regent superior of all rats in maintenance... at least until someone else tries to claim the throne.
// tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:4
public partial class mob_living_basic_regal_rat : mob_living_basic
{
    private const string REGALRAT_INTERACTION = "regalrat"; // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:1

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        RegisterSignal(this, COMSIG_MOB_LOGIN, nameof(on_login));
        Waiting.NoSleep(AddElementTrait(TRAIT_WADDLING, INNATE_TRAIT, typeof(datum_element_waddling)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_door_pryer), 5 * SECONDS, REGALRAT_INTERACTION));
        Waiting.NoSleep(AddElement(typeof(datum_element_poster_tearer), null, REGALRAT_INTERACTION));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ghost_direct_control), null, "the Regal Rat, cheesy be their crown", null, null, poll_ghosts, null, "regal_rat", "You are an independent, invasive force on the station! Hoard coins, trash, cheese, and the like from the safety of darkness!", null, Dm.@new<datum_callback>(this, nameof(became_player_controlled)), typeof(obj_item_food_cheese_wedge)));
        DmList innate_actions = TgState.Current.mob_living_basic_regal_rat__innate_actions ??= Dm.list(
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_domain), BB_DOMAIN_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_riot), BB_RAISE_HORDE_ABILITY));
        grant_actions_by_list(innate_actions);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:76
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        datum potential_component = GetComponent(typeof(datum_component_ghost_direct_control)) as datum;
        if (!QDELETED(potential_component))
            qdel(potential_component);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:82
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (user == this)
            return dot;

        if (user is mob_living_basic_regal_rat)
        {
            Dm.append(ref dot, span_warning("Who is this foolish false king? This will not stand!"));
            return dot;
        }

        if (user is mob_living_basic_mouse)
        {
            if (Dm.truthy(user.faction_check_atom(this, exact_match: TRUE)))
                Dm.append(ref dot, span_notice(Dm.text("This is your king. Long live [] majesty!", p_their())));
            else
                Dm.append(ref dot, span_warning(Dm.text("This is a false king! Strike [] down!", p_them())));
            return dot;
        }

        Dm.append(ref dot, special_moniker);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:100
    public override async DmTask handle_environment(datum_gas_mixture environment = null, object seconds_per_tick = null, params object[] __extra)
    {
        await base.handle_environment(environment, seconds_per_tick, __extra);
        if (stat == DEAD || environment == null || environment.moles[typeof(datum_gas_miasma)] == null)
            return;
        float miasma_percentage = Dm.num(environment.moles[typeof(datum_gas_miasma)]) / Dm.num(environment.total_moles());
        if (miasma_percentage >= 0.25f)
            await heal_bodypart_damage(1);
    }

    /// Triggers an alert to all ghosts that the rat has become player controlled.
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:109
    public virtual void became_player_controlled()
    {
        notify_ghosts(
            I18n.T("All rise for [], ascendant to the throne in \\the [].", name, get_area(this)),
            source: this,
            header: I18n.T("Sentient Rat Created"),
            notify_flags: NOTIFY_CATEGORY_NOFLASH);
    }

    /// Supplementary work we do when we login. Done this way so we synchronize with the ai controller shutting off and all that jazz.
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:118
    [SignalHandler]
    public virtual void on_login()
    {
        if (!Dm.truthy(special_moniker))
            grant_titles(); // all players are special :)
    }

    /// Grants the rat a special name.
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:124
    public virtual void grant_titles()
    {
        DmList titles = TgState.Current.mob_living_basic_regal_rat__titles ??= Dm.list("Bojar", "Emperor", "King", "Lord", "Master", "Overlord", "Prince", "Shogun", "Supreme", "Tsar");
        DmList kingdoms = TgState.Current.mob_living_basic_regal_rat__kingdoms ??= Dm.list("Cheese", "Garbage", "Maintenance", "Miasma", "Plague", "Trash", "Vermin");
        DmList descriptors = TgState.Current.mob_living_basic_regal_rat__descriptors ??= Dm.list("Big Cheese", "Brute", "Champion of All Mislaid Creatures", "Foul", "Great", "Grey", "Horrible", "Populator", "Powerful", "Quiet", "Vain");

        object selected_title = pick(titles);
        object selected_kingdom = pick(kingdoms);
        name = Dm.text("[] [], the []", selected_title, selected_kingdom, pick(descriptors)); // ex "Tsar Maintenance, the Brute"
        special_moniker = Dm.text("You better not screw with [] []... How do you become a [] of that anyways?", p_their(), selected_kingdom, selected_title);
    }

    /// Checks if we are able to attack this object, as well as send out the signal to see if we get any special regal rat interactions.
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:172
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(DOING_INTERACTION(this, REGALRAT_INTERACTION)) || !Dm.truthy(allowed_to_attack(target)))
            return BASIC_MOB_END_ATTACK_CHAIN;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(target._listen_lookup?[COMSIG_RAT_INTERACT]) ? NONE : target._SendSignal(COMSIG_RAT_INTERACT, Dm.list(target, this))), COMPONENT_RAT_INTERACTED)))
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;

        if (mind == null || Dm.truthy(combat_mode))
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;

        if (Dm.truthy(await poison_target(target)))
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;

        return BASIC_MOB_CONTINUE_ATTACK_CHAIN;
    }

    /// Checks if we are allowed to attack this mob. Will return TRUE if we are potentially allowed to attack, but if we end up in a case where we should NOT attack, return FALSE.
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:192
    public virtual object allowed_to_attack(atom the_target = null)
    {
        if (QDELETED(the_target))
            return FALSE; //wat

        if (!isliving(the_target))
            return TRUE; // it might be possible to attack this? we'll find out soon enough

        mob_living living_target = the_target as mob_living;

        if (IS_DEAD_OR_FAKING(living_target))
        {
            balloon_alert(this, "already dead!");
            return FALSE;
        }

        if (Dm.truthy(living_target.faction_check_atom(this, exact_match: TRUE)))
        {
            balloon_alert(this, "one of your soldiers!");
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:218
    public virtual async DmTask<object> poison_target(atom target = null)
    {
        if (target.reagents == null || !Dm.truthy(target.is_injectable(this, TRUE)))
            return FALSE;

        visible_message(
            span_warning(Dm.text("[] starts licking [] passionately!", this, target)),
            span_notice(Dm.text("You start licking []...", target)),
            span_warning("You hear a disgusting slurping sound...")
        );

        if (!Dm.truthy(await do_after(this, 2 * SECONDS, target, interaction_key: REGALRAT_INTERACTION)))
            return TRUE; // don't return false here because they tried to lick and the do_after was interrupted, otherwise cancelling the do_after will make them hit the target.

        await (target.reagents as datum_reagents).add_reagent(typeof(datum_reagent_rat_spit), rand(1, 3), no_react: TRUE);
        balloon_alert(this, "licked");
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat.dm:243
    public virtual void cheese_heal(obj_item target = null, object amount = null, object message = null)
    {
        if (Dm.num(health) >= Dm.num(maxHealth))
        {
            balloon_alert(this, "you feel full!");
            return;
        }

        to_chat(this, message);
        Waiting.NoSleep(heal_bodypart_damage(Dm.num(amount)));
        qdel(target);
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_regal_rat__innate_actions;
    internal DmList mob_living_basic_regal_rat__titles;
    internal DmList mob_living_basic_regal_rat__kingdoms;
    internal DmList mob_living_basic_regal_rat__descriptors;
}
