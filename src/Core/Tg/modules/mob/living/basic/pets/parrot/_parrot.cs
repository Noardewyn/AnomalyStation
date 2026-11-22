// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/parrot/_parrot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but vv_edit_var (admin tooling, a row) (gaps package C2).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:3
    public DmList strippable_parrot_items;

    public virtual void InitGlobalstrippable_parrot_items()
    {
        strippable_parrot_items = create_strippable_list(Dm.list(typeof(datum_strippable_item_parrot_headset), typeof(datum_strippable_item_hand_left))) as DmList;
        Dm.append(ref gvars_datum_init_order, "strippable_parrot_items");
    }
}

/// Parrots! Klepto bastards that imitate your speech and hoard your shit.
// tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:11
public partial class mob_living_basic_parrot : mob_living_basic
{
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:1
    private const float FORCED_SPEECH_COOLDOWN_DURATION = 5 * SECONDS;

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_perching.dm PERCH_SOURCE
    private const string PERCH_SOURCE = "perched";

    ///Headset for Poly to yell at engineers :) (the generator typed it as a Type: hand-declared, with the class's constructor)
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:47
    public obj_item_radio_headset ears;

    /// Contains all of the perches that parrots will generally sit on until something catches their eye.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:56
    private static DmList desired_perches => TgState.Current.mob_living_basic_parrot__desired_perches ??= typecacheof(Dm.list(
        typeof(obj_machinery_computer),
        typeof(obj_machinery_dna_scannernew),
        typeof(obj_machinery_nuclearbomb),
        typeof(obj_machinery_recharge_station),
        typeof(obj_machinery_smartfridge),
        typeof(obj_machinery_suit_storage_unit),
        typeof(obj_machinery_telecomms),
        typeof(obj_machinery_teleport),
        typeof(obj_structure_displaycase),
        typeof(obj_structure_filingcabinet),
        typeof(obj_structure_frame_computer))) as DmList;

    ///items we wont pick up
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:71
    private static DmList ignore_items => TgState.Current.mob_living_basic_parrot__ignore_items ??= typecacheof(Dm.list(typeof(obj_item_radio))) as DmList;

    /// Food that Poly loves to eat (spoiler alert it's just crackers)
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:74
    private static DmList edibles => TgState.Current.mob_living_basic_parrot__edibles ??= Dm.list(typeof(obj_item_food_cracker));

    ///list of commands we follow
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:78
    private static DmList pet_commands => TgState.Current.mob_living_basic_parrot__pet_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_follow), typeof(datum_pet_command_perform_trick_sequence));

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:11
    public mob_living_basic_parrot()
    {
        name = "parrot";
        desc = "The parrot squawks, \"They're a Parrot! BAWWK!\"";
        icon = Dm.resource("icons/mob/simple/animal.dmi");
        icon_state = "parrot_fly";
        icon_living = "parrot_fly";
        icon_dead = "parrot_dead";
        density = 0;
        health = 80;
        maxHealth = 80;
        pass_flags = 17;
        melee_damage_upper = 10;
        melee_damage_lower = 5;
        response_help_continuous = "pets";
        response_help_simple = "pet";
        response_disarm_continuous = "gently moves aside";
        response_disarm_simple = "gently move aside";
        response_harm_continuous = "swats";
        response_harm_simple = "swat";
        combat_mode = 1;
        attack_verb_continuous = "chomps";
        attack_verb_simple = "chomp";
        attack_vis_effect = "bite";
        friendly_verb_continuous = "grooms";
        friendly_verb_simple = "groom";
        mob_size = 1;
        gold_core_spawnable = 2;
        ai_controller = typeof(datum_ai_controller_basic_controller_parrot);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:22
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        guaranteed_butcher_results = Dm.list(Dm.kv(typeof(obj_item_food_cracker), 1));
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        setup_headset();
        update_speech_blackboards();
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_PARROT_PERCH_TYPES, desired_perches);
        controller.set_blackboard_key(BB_IGNORE_ITEMS, ignore_items);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus))); // parrots will listen for the signal this element sends in order to say something in response to the pat
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_strippable), GLOB.strippable_parrot_items));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddElement(typeof(datum_element_dextrous), 1f, null, TRUE));
        Waiting.NoSleep(AddComponent(typeof(datum_component_listen_and_repeat), get_static_list_of_phrases(), BB_PARROT_REPEAT_STRING));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), edibles, 100f, 0f));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        RegisterSignal(this, COMSIG_ATOM_ATTACKBY_SECONDARY, nameof(on_attacked)); // this means we could have a peaceful interaction, like getting a cracker
        RegisterSignal(this, COMSIG_ATOM_WAS_ATTACKED, nameof(on_injured)); // this means we got hurt and it's go time
        RegisterSignal(this, COMSIG_ANIMAL_PET, nameof(on_pet));
        RegisterSignal(this, COMSIG_KB_MOB_DROPITEM_DOWN, nameof(drop_item_on_signal));
        ADD_TRAIT(this, TRAIT_CAN_MOUNT_HUMANS, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:108
    public override object Destroy(float? force = null)
    {
        // should have cleaned these up on death, but let's be super safe in case that didn't happen
        if (!QDELETED(ears))
        {
            qdel(ears);
            ears = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:114
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (ears != null)
        {
            await ears.forceMove(drop_location() as atom);
            ears = null;
        }

        if (buckled != null)
            await buckled.unbuckle_mob(this, force: TRUE);
        buckled = null;
        pixel_x = base_pixel_x;
        pixel_y = base_pixel_y;

        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:127
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        obj_item held_item = get_active_held_item() as obj_item;
        Dm.append(ref dot, Dm.text("It appears to []", held_item == null ? "not be holding anything." : Dm.text("be holding \\a [].", held_item)));

        if (stat != DEAD)
            return dot;

        if (HAS_MIND_TRAIT(user, TRAIT_NAIVE))
            Dm.append(ref dot, span_notice((string)pick(
                "It seems tired and shagged out after a long squawk.",
                "It seems to be pining for the fjords.",
                "It's resting. It's a beautiful bird. Lovely plumage.")));
        else
            Dm.append(ref dot, span_danger((string)pick(
                "This is a late parrot.",
                "This is an ex-parrot.",
                "This parrot is no more.")));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:148
    public override object say_dead(object message = null, mob mannequin_controller = null)
    {
        return null; // this is so flarped
    }

    //literally copied from human/radio(), but there's no other way to do this. at least it's better than it used to be.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:151
    public override async DmTask<object> radio__proc(object message = null, DmList message_mods = null, DmList spans = null, object language = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.radio__proc(message, message_mods, spans, language);
        if (!Dm.eq(dot, NONE))
            return dot;

        if (Dm.truthy(message_mods[MODE_HEADSET]))
        {
            if (ears != null)
                ears.talk_into(this, message, null, spans, language, message_mods);
            return ITALICS | REDUCE_RANGE;
        }
        else if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_DEPARTMENT))
        {
            if (ears != null)
                ears.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
            return ITALICS | REDUCE_RANGE;
        }
        else if (Dm.@in(message_mods[RADIO_EXTENSION], GLOB.default_radio_channels))
        {
            if (ears != null)
            {
                ears.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
                return ITALICS | REDUCE_RANGE;
            }
        }

        return NONE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:171
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (stat == DEAD)
            return dot;
        icon_state = HAS_TRAIT(this, TRAIT_PARROT_PERCHED) ? icon_sit : icon_living;
        return dot;
    }

    /// Proc that we just use to see if we're dragging onto stomething something for perch behavior or dropping the item we currently have
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:178
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        object dot = await base.mouse_drop_dragged(over, user, src_location, over_location, @params);
        if (!Dm.truthy(await start_perching(over)))
            balloon_alert(user, "not a perching spot!");
        return dot;
    }

    /// Proc that handles sending the signal and returning a valid phrase to say. Will not do anything if we don't have a stat or if we're cliented.
    /// Will return either a string or null.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:185
    public virtual object get_phrase()
    {
        if (client != null || IS_UNCONSCIOUS_OR_CRIT(this))
            return null;

        if (forced_speech_cooldown > world.time) // COOLDOWN_FINISHED, inline (rules 11.1)
            return null;

        object return_value = !Dm.truthy(_listen_lookup?[COMSIG_NEEDS_NEW_PHRASE]) ? NONE : _SendSignal(COMSIG_NEEDS_NEW_PHRASE, Dm.list(this));
        if (Dm.truthy(Dm.bitand(return_value, NO_NEW_PHRASE_AVAILABLE)))
            return null;

        forced_speech_cooldown = world.time + FORCED_SPEECH_COOLDOWN_DURATION;
        return (ai_controller as datum_ai_controller).blackboard[BB_PARROT_REPEAT_STRING];
    }

    /// Proc that listens for when a parrot is pet so we can dispatch a voice line.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:200
    [SignalHandler]
    public virtual void on_pet(mob_living_basic source = null, mob_living petter = null, DmList modifiers = null)
    {
        object return_value = get_phrase();
        if (return_value == null)
            return;

        // the blackboard's value is the line's assoc list (listen_and_repeat), say's message takes its text: tg's own say on a list prints "/list" here: a runtime in tg
        Waiting.Detach(say(message: return_value is DmList phrase ? phrase["line"] : return_value, forced: "parrot oneliner on pet"));
    }

    /// Proc that ascertains the type of perch we're dealing with and starts the perching process.
    /// Returns TRUE if we started perching, FALSE otherwise.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:210
    public virtual async DmTask<object> start_perching(atom target = null)
    {
        if (HAS_TRAIT(this, TRAIT_PARROT_PERCHED))
        {
            balloon_alert(this, "already perched!");
            return FALSE;
        }

        if (ishuman(target))
            return await perch_on_human(target as mob_living_carbon_human);

        if (!Dm.truthy(is_type_in_typecache(target, desired_perches)))
            return FALSE;

        await forceMove(get_turf(target));
        await drop_held_item(gently: TRUE); // comfy :)
        toggle_perched(perched: TRUE);
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(after_move));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:227
    [SignalHandler]
    public virtual void after_move(atom source = null)
    {
        UnregisterSignal(this, COMSIG_MOVABLE_MOVED);
        toggle_perched(perched: FALSE);
    }

    /// Proc that will perch us on a human. Returns TRUE if we perched, FALSE otherwise.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:234
    public virtual async DmTask<object> perch_on_human(mob_living_carbon_human target = null)
    {
        if (LAZYLEN(target.buckled_mobs) >= Dm.num(target.max_buckled_mobs))
        {
            balloon_alert(this, "can't perch on them!");
            return FALSE;
        }

        await forceMove(get_turf(target));
        if (!Dm.truthy(await target.buckle_mob(this, TRUE)))
            return FALSE;

        to_chat(this, span_notice(Dm.text("You sit on []'s shoulder.", target)));
        toggle_perched(perched: TRUE);
        RegisterSignal(this, COMSIG_LIVING_SET_BUCKLED, nameof(on_unbuckle));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:248
    [SignalHandler]
    public virtual void on_unbuckle(mob_living source = null, atom_movable new_buckled = null)
    {
        if (new_buckled != null)
            return;
        UnregisterSignal(this, COMSIG_LIVING_SET_BUCKLED);
        toggle_perched(perched: FALSE);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:258
    public virtual void toggle_perched(object perched = null)
    {
        if (!Dm.truthy(perched))
        {
            REMOVE_TRAIT(this, TRAIT_PARROT_PERCHED, PERCH_SOURCE);
            remove_offsets(PERCH_SOURCE);
        }
        else
        {
            ADD_TRAIT(this, TRAIT_PARROT_PERCHED, PERCH_SOURCE);
            add_offsets(PERCH_SOURCE, y_add: 9);
        }
        update_appearance(UPDATE_ICON_STATE);
    }

    /// Master proc which will determine the intent of OUR attacks on an object and summon the relevant procs accordingly.
    /// This is pretty much meant for players, AI will use the task-specific procs instead.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:271
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;

        if (iscarbon(target) && Dm.truthy(await steal_from_mob(target as mob_living_carbon)))
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        return dot;
    }

    /// Picks up an item from the ground and puts it in our claws. Returns TRUE if we picked it up, FALSE otherwise.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:280
    public override object put_in_hand_check(obj_item item_to_pick_up = null)
    {
        if (Dm.num(item_to_pick_up.w_class) > WEIGHT_CLASS_SMALL)
        {
            balloon_alert(this, "too big to pick up!");
            return FALSE;
        }

        return base.put_in_hand_check(item_to_pick_up);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:287
    public override async DmTask<object> put_in_hand(obj_item target = null, object hand_index = null, float? forced = null, float? ignore_anim = null, float? visuals_only = null)
    {
        forced ??= FALSE;
        ignore_anim ??= TRUE;
        visuals_only ??= FALSE;
        if (target is obj_item_food_cracker)
        {
            consume_cracker(target);
            qdel(target);
            // Eating it counts as 'handling' it. A FALSE return makes attempt_pickup() and put_in_hands() drop the deleted cracker onto the floor which hangs refs
            return TRUE;
        }

        if (Dm.truthy(await base.put_in_hand(target, hand_index, forced, ignore_anim, visuals_only)))
            visible_message(
                span_notice(Dm.text("[] grabs []!", this, target)),
                span_notice(Dm.text("You grab []!", target)),
                span_hear("You hear the sounds of wings flapping furiously."));
        return TRUE;
    }

    /// Looks for an item that we can snatch and puts it in our claws. Returns TRUE if we picked it up, FALSE otherwise.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:303
    public virtual async DmTask<object> steal_from_mob(mob_living_carbon victim = null)
    {
        if (get_active_held_item() != null)
        {
            balloon_alert(this, "already holding something!");
            return FALSE;
        }

        foreach (obj_item stealable in Dm.each<obj_item>(victim.held_items))
        {
            if (Dm.num(stealable.w_class) > WEIGHT_CLASS_SMALL)
                continue;

            if (!Dm.truthy(await victim.temporarilyRemoveItemFromInventory(stealable)))
                continue;

            await dropItemToGround(stealable);
            visible_message(
                span_notice(Dm.text("[] wrestles [] out of []'s hand!", this, stealable, victim)),
                span_notice(Dm.text("You wrestles [] out of []'s hand!", stealable, victim)),
                span_hear("You hear the sounds of wings flapping furiously."));
            return TRUE;
        }

        return FALSE;
    }

    /// If we're right-clicked on with a cracker, we eat the cracker.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:326
    [SignalHandler]
    public virtual object on_attacked(mob_living_basic source = null, obj_item thing = null, mob_living attacker = null, DmList modifiers = null)
    {
        if (thing is not obj_item_food_cracker) // Poly wants a cracker
            return null;

        consume_cracker(thing); // potential clash with the tameable element so we'll leave it to that to handle qdeling the cracker.
        return COMPONENT_NO_AFTERATTACK;
    }

    /// Eats a cracker (or anything i guess). This would be nice to eventually fold into the basic_eating element but we do too much snowflake inventory code stuff for this to be reliable presently.
    /// We don't qdel the item here, we assume the invoking proc will have handled that somehow.
    /// Returns TRUE if we ate the thing.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:337
    public virtual object consume_cracker(obj_item thing = null)
    {
        to_chat(this, span_notice(Dm.text("[] eagerly devours \\the [].", this, thing)));
        if (thing is not obj_item_food_cracker)
            return TRUE; // we still ate it

        if (Dm.num(health) < Dm.num(maxHealth))
            Waiting.NoSleep(adjust_brute_loss(-10));
        speech_probability_rate *= 1.27f;
        speech_shuffle_rate += 10;
        update_speech_blackboards();
        return TRUE;
    }

    /// Handles special behavior whenever we are injured.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:350
    [SignalHandler]
    public virtual void on_injured(mob_living_basic source = null, mob_living attacker = null, object attack_flags = null)
    {
        if (client != null || !IS_UNCONSCIOUS_OR_CRIT(this))
            return;

        Waiting.Detach(drop_held_item(gently: FALSE));

        object return_value = get_phrase();
        if (return_value == null)
            return;

        Waiting.Detach(say(message: return_value is DmList phrase ? phrase["line"] : return_value, forced: "parrot oneliner on attack"));
    }

    /// Handles dropping items we're holding. Gently is a special modifier we can use for special interactions.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:364
    public virtual async DmTask drop_held_item(float? gently = null)
    {
        gently ??= TRUE;
        if (Dm.num(get_num_held_items()) == 0)
        {
            balloon_alert(this, "nothing to drop!");
            return;
        }

        if (IS_UNCONSCIOUS_OR_CRIT(this)) // don't gotta do shit
            return;

        if (!Dm.truthy(gently) && get_active_held_item() is obj_item_grenade bomb)
        {
            balloon_alert(this, "bombs away!"); // you'll likely die too so we can get away with the `!` here
            await bomb.forceMove(drop_location() as atom);
            await bomb.detonate();
            return;
        }

        balloon_alert(this, "dropped item");
        await drop_all_held_items();
    }

    /// Updates our speech blackboards mob-side to reflect the current speech on the controller to ensure everything is synchronized.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:391
    public virtual void update_speech_blackboards()
    {
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_PARROT_REPEAT_PROBABILITY, speech_probability_rate);
        controller.set_blackboard_key(BB_PARROT_PHRASE_CHANGE_PROBABILITY, speech_shuffle_rate);
    }

    /// Will simply set up the headset for the parrot to use. Stub, implemented on subtypes.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:396
    public virtual void setup_headset()
    {
    }

    /// Gets a static list of phrases we wish to pass to the element.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:400
    public virtual DmList get_static_list_of_phrases()
    {
        return TgState.Current.mob_living_basic_parrot__default_phrases ??= Dm.list("BAWWWWK george mellons griffing me!", "Cracker?", "Hello!", "Hi!");
    }

    /// Gets the available channels that this parrot has access to. Returns a list of the channels we can use.
    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:411
    public virtual DmList get_available_channels()
    {
        DmList returnable_list = Dm.list();
        if (ears == null)
            return returnable_list;

        DmList headset_channels = ears.channels as DmList;
        foreach (object channel in Dm.each<object>(headset_channels))
            Dm.append(ref returnable_list, GLOB.channel_tokens[channel]); // will return something like ":e" or ":c" y'know

        return returnable_list;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:422
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        Dm.@new<obj_effect_temp_visual_heart>(drop_location());
    }

    // tg: code/modules/mob/living/basic/pets/parrot/_parrot.dm:426
    [SignalHandler]
    public virtual object drop_item_on_signal(mob_living user = null)
    {
        Waiting.Detach(drop_held_item());
        return COMSIG_KB_ACTIVATED;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_parrot__desired_perches;
    internal DmList mob_living_basic_parrot__ignore_items;
    internal DmList mob_living_basic_parrot__edibles;
    internal DmList mob_living_basic_parrot__pet_commands;
    internal DmList mob_living_basic_parrot__default_phrases;
}

// this file is for parrots that aren't poly

/// Parrot that will just randomly spawn with a headset. Nothing too special beyond that.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_subtypes.dm:4
public partial class mob_living_basic_parrot_headsetted : mob_living_basic_parrot
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_subtypes.dm:6
    public override void setup_headset()
    {
        object headset = pick(typeof(obj_item_radio_headset_headset_cargo), typeof(obj_item_radio_headset_headset_eng), typeof(obj_item_radio_headset_headset_med), typeof(obj_item_radio_headset_headset_sci), typeof(obj_item_radio_headset_headset_sec));
        ears = Dm.@new((Type)headset, this) as obj_item_radio_headset;
    }
}
