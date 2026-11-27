// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/jobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the detective's fedora (the pockets with the flask in them, the candy corn on alt-click; the screentip is a live-partial row) and the
// surgery caps' Initialize, attack_self and examine (the strings, the fishing difficulty element). The var
// blocks are generated. Gaps J2: the chef's hat, with the mouse pilot. Gaps CL1 (D333): the head mirror (a closer look at a lit face's mouth,
// ears and eyes on examining it more) and the highlander's beret.

//Detective
// tg: code/modules/clothing/head/jobs.dm:186
public partial class obj_item_clothing_head_fedora_det_hat : obj_item_clothing_head_fedora
{
    // tg: code/modules/clothing/head/jobs.dm:207
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_small_fedora_detective)));

        register_context();

        Dm.@new(flask_path, this);
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:217
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to take a candy corn.")));
        return dot;
    }

    /// Now to solve where all these keep coming from
    // tg: code/modules/clothing/head/jobs.dm:231
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!(candy_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            to_chat(user, span_warning(I18n.T("A candy corn was just taken! You should wait a couple minutes, lest you burn through the stash.")));
            return CLICK_ACTION_BLOCKING;
        }

        obj_item_food_candy_corn sweets = Dm.@new<obj_item_food_candy_corn>(this);
        await user.put_in_hands(sweets);
        to_chat(user, span_notice(I18n.T("You slip a candy corn from \\the [].", this)));
        candy_cooldown = world.time + 2 * MINUTES; // COOLDOWN_START(src, candy_cooldown, CANDY_CD_TIME) (:231), CANDY_CD_TIME 2 MINUTES

        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/modules/clothing/head/jobs.dm:654
public partial class obj_item_clothing_head_utility_surgerycap : obj_item_clothing_head_utility
{
    // tg: code/modules/clothing/head/jobs.dm:667
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -3));
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:671
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        balloon_alert(user, I18n.T("[] strings...", Dm.truthy(Dm.bitand(flags_inv, HIDEHAIR)) ? I18n.T("loosening") : I18n.T("tightening")));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            return dot;
        Dm.bitxor_assign(ref flags_inv, HIDEHAIR);
        balloon_alert(user, I18n.T("[] strings", Dm.truthy(Dm.bitand(flags_inv, HIDEHAIR)) ? I18n.T("tightened") : I18n.T("loosened ")));
        return TRUE;
    }

    // tg: code/modules/clothing/head/jobs.dm:682
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use in hand to [] the strings.", Dm.truthy(Dm.bitand(flags_inv, HIDEHAIR)) ? I18n.T("loosen") : I18n.T("tighten"))));
        return dot;
    }
}

// tg: code/modules/clothing/head/jobs.dm:8
public partial class obj_item_clothing_head_utility_chefhat : obj_item_clothing_head_utility
{
    // tg: code/modules/clothing/head/jobs.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_chefhat)));
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:33
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        mob_living_basic new_boss = get_mouse(arrived) as mob_living_basic;
        if (new_boss == null)
            return dot;
        RegisterSignal(new_boss, COMSIG_MOB_PRE_EMOTED, nameof(on_mouse_emote));
        RegisterSignal(new_boss, COMSIG_MOVABLE_PRE_MOVE, nameof(on_mouse_moving));
        RegisterSignal(new_boss, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, nameof(on_mouse_moving));
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:42
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        mob_living_basic old_boss = get_mouse(gone) as mob_living_basic;
        if (old_boss == null)
            return dot;
        UnregisterSignal(old_boss, Dm.list(COMSIG_MOB_PRE_EMOTED, COMSIG_MOVABLE_PRE_MOVE, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE));
        return dot;
    }

    /// Returns a mob stored inside a mob container, if there is one
    // tg: code/modules/clothing/head/jobs.dm:50
    public virtual object get_mouse(atom possible_mouse = null)
    {
        if (possible_mouse is not obj_item_mob_holder) // ispickedupmob
            return null;
        obj_item_mob_holder mousey_holder = possible_mouse as obj_item_mob_holder;
        return Dm.locate<mob_living_basic>(mousey_holder.contents);
    }

    /// Relays emotes emoted by your boss to the hat wearer for full immersion
    // tg: code/modules/clothing/head/jobs.dm:57
    [SignalHandler]
    public virtual object on_mouse_emote(mob_living source = null, object key = null, object emote_message = null, object type_override = null, object intentional = null, datum_emote emote = null)
    {
        mob_living_carbon wearer = loc as mob_living_carbon;
        if (wearer == null || Dm.truthy(INCAPACITATED_IGNORING(wearer, INCAPABLE_RESTRAINTS)))
            return null;
        if (!prob(mouse_control_probability))
            return COMPONENT_CANT_EMOTE;
        Waiting.Detach(wearer.emote(key, Dm.num(type_override), emote_message, FALSE));
        return COMPONENT_CANT_EMOTE;
    }

    /// Relays movement made by the mouse in your hat to the wearer of the hat
    // tg: code/modules/clothing/head/jobs.dm:68
    [SignalHandler]
    public virtual object on_mouse_moving(mob_living source = null, atom moved_to = null)
    {
        if (!prob(mouse_control_probability) || !(move_cooldown <= world.time))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE; // Didn't roll well enough or on cooldown

        mob_living_carbon wearer = loc as mob_living_carbon;
        if (wearer == null || Dm.truthy(INCAPACITATED_IGNORING(wearer, INCAPABLE_RESTRAINTS)))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE; // Not worn or can't move

        object move_direction = get_dir(wearer, moved_to);
        if (!Dm.truthy(Waiting.NoSleep(wearer.Process_Spacemove(Dm.num(move_direction)))))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE; // Currently drifting in space
        if (!Dm.truthy(has_gravity()) || !isturf(wearer.loc))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE; // Not in a location where we can move

        Waiting.NoSleep(step_towards(wearer, moved_to));
        float move_delay = Dm.num(wearer.cached_multiplicative_slowdown);
        if (Dm.truthy(ISDIAGONALDIR(Dm.num(move_direction))))
            move_delay *= Dm.sqrt(2);
        move_cooldown = world.time + move_delay; // COOLDOWN_START
        return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
    }

    // tg: code/modules/clothing/head/jobs.dm:90
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is donning []! It looks like [] trying to become a chef.", user, this, user.p_theyre())));
        await user.say("Bork Bork Bork!", forced: "chef hat suicide");
        await sleep(2 * SECONDS);
        user.visible_message(span_suicide(I18n.T("[] climbs into an imaginary oven!", user)));
        await user.say("BOOORK!", forced: "chef hat suicide");
        playsound(user, Dm.resource("sound/machines/ding.ogg"), 50, TRUE);
        return FIRELOSS;
    }
}

// tg: code/modules/clothing/head/jobs.dm:258
public partial class obj_item_clothing_head_fedora_inspector_hat : obj_item_clothing_head_fedora
{
    // tg: code/modules/clothing/head/jobs.dm:276
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        become_hearing_sensitive(ROUNDSTART_TRAIT);
        qdel(atom_storage); // QDEL_NULL
        atom_storage = null;
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:281
    public virtual object set_prefix(object desired_prefix = null)
    {
        prefix = Dm.str(desired_prefix);

        // Regenerated the phrases here.
        foreach (object old_regex in Dm.each(phrases_by_regex.Copy()))
        {
            object old_phrase = phrases_by_regex[old_regex];
            obj_item old_item = items_by_regex[old_regex] as obj_item;
            items_by_regex.Remove(old_regex);
            phrases_by_regex.Remove(old_regex);
            set_phrase(old_phrase, old_item);
        }

        return TRUE;
    }

    // tg: code/modules/clothing/head/jobs.dm:295
    public virtual object set_phrase(object desired_phrase = null, obj_item associated_item = null)
    {
        regex phrase_regex = Dm.regex(Dm.text("[][\\s\\W]+[]", prefix, desired_phrase), "i");

        phrases_by_regex[phrase_regex] = desired_phrase;
        items_by_regex[phrase_regex] = associated_item;

        return TRUE;
    }

    // tg: code/modules/clothing/head/jobs.dm:304
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("You can put items inside, and get them out by saying a phrase, or using it in-hand!")));
        dot.Add(span_notice(I18n.T("The prefix is <b>[]</b>, and you can change it with alt-click!\n", prefix)));
        foreach (object found_regex in Dm.each(phrases_by_regex))
        {
            object found_phrase = phrases_by_regex[found_regex];
            obj_item found_item = items_by_regex[found_regex] as obj_item;
            // icon2html(found_item, user): the chat icons (section 76)
            dot.Add(span_notice(I18n.T("You can remove [] by saying <b>\"[] []\"</b>!", found_item, prefix, found_phrase)));
        }
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:313
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        mob_living_carbon wearer = loc as mob_living_carbon;
        if (wearer == null || speaker != wearer) //if we are worn
            return null;

        raw_message = htmlrendertext(raw_message);

        foreach (regex found_regex in Dm.each<regex>(phrases_by_regex.Copy()))
        {
            if (!Dm.truthy(found_regex.Find(raw_message)))
                continue;
            obj_item found_item = items_by_regex[found_regex] as obj_item;
            if (Dm.truthy(await wearer.put_in_hands(found_item)))
            {
                wearer.visible_message(span_warning(I18n.T("[] drops [] into the hands of []!", this, found_item, wearer)));
                dot = Dm.bitor(HEAR_HEARD, HEAR_UNDERSTOOD);
            }
            else
            {
                balloon_alert(wearer, I18n.T("can't put in hands!"));
                break;
            }
        }

        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:334
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (length(contents) >= max_items)
        {
            balloon_alert(user, I18n.T("full!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool.w_class > max_weight)
        {
            balloon_alert(user, I18n.T("too big!"));
            return ITEM_INTERACT_BLOCKING;
        }

        object desired_phrase = await tgui_input_text(user, I18n.T("What is the activation phrase?"), I18n.T("Activation phrase"), "gadget", max_length: 26);
        if (!Dm.truthy(desired_phrase) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return ITEM_INTERACT_BLOCKING;

        if (tool.loc != user || !Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You install [] into the [] slot of [].", tool, thtotext(length(contents)), this)));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
        set_phrase(desired_phrase, tool);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/head/jobs.dm:360
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        if (length(items_by_regex) == 0)
            return CLICK_ACTION_BLOCKING;
        DmList found_items = Dm.list();
        foreach (object found_regex in Dm.each(items_by_regex))
            found_items.Add(items_by_regex[found_regex]);
        obj found_item = await tgui_input_list(user, I18n.T("What item do you want to remove?"), I18n.T("Item Removal"), found_items) as obj;
        if (found_item == null || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return CLICK_ACTION_BLOCKING;
        await user.put_in_inactive_hand(found_item as obj_item);
        return null;
    }

    // tg: code/modules/clothing/head/jobs.dm:372
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object new_prefix = await tgui_input_text(user, I18n.T("What should be the new prefix?"), I18n.T("Activation prefix"), prefix, max_length: 24);
        if (!Dm.truthy(new_prefix) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return CLICK_ACTION_BLOCKING;
        set_prefix(new_prefix);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/head/jobs.dm:379
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        foreach (object found_regex in Dm.each(items_by_regex.Copy()))
        {
            obj_item found_item = items_by_regex[found_regex] as obj_item;
            if (gone != found_item)
                continue;
            items_by_regex.Remove(found_regex);
            phrases_by_regex.Remove(found_regex);
            break;
        }
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:389
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        atom atom_location = drop_location() as atom;
        foreach (object found_regex in Dm.each(items_by_regex.Copy()))
        {
            obj_item result = items_by_regex[found_regex] as obj_item;
            await result.forceMove(atom_location);
            items_by_regex.Remove(found_regex);
            phrases_by_regex.Remove(found_regex);
        }

        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/clothing/head/jobs.dm:400
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC(items_by_regex); //Anything that failed to drop gets deleted.
        return base.Destroy(force);
    }
}

// tg: code/modules/clothing/head/jobs.dm:426  (gaps J4: the head of security's garment bag)
public partial class obj_item_clothing_head_hats_hos_cap : obj_item_clothing_head_hats_hos
{
    // tg: code/modules/clothing/head/jobs.dm:431
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Give it a little publicity
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", Dm.list(typeof(datum_crafting_recipe_sturdy_shako)))));
        return dot;
    }
}

// tg: code/modules/clothing/head/jobs.dm:516  (gaps J4: the warden's garment bag)
public partial class obj_item_clothing_head_hats_warden_drill : obj_item_clothing_head_hats_warden
{
    private const string DRILL_DEFAULT = "default";
    private const string DRILL_SHOUTING = "shouting";
    private const string DRILL_YELLING = "yelling";
    private const string DRILL_CANADIAN = "canadian";

    // tg: code/modules/clothing/head/jobs.dm:524
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, tool)))
            return TRUE;
        switch (mode)
        {
            case DRILL_DEFAULT:
                to_chat(user, span_notice(I18n.T("You set the voice circuit to the middle position.")));
                mode = DRILL_SHOUTING;
                break;
            case DRILL_SHOUTING:
                to_chat(user, span_notice(I18n.T("You set the voice circuit to the last position.")));
                mode = DRILL_YELLING;
                break;
            case DRILL_YELLING:
                to_chat(user, span_notice(I18n.T("You set the voice circuit to the first position.")));
                mode = DRILL_DEFAULT;
                break;
            case DRILL_CANADIAN:
                to_chat(user, span_danger(I18n.T("You adjust voice circuit but nothing happens, probably because it's broken.")));
                break;
        }
        return TRUE;
    }

    // tg: code/modules/clothing/head/jobs.dm:541
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await base.wirecutter_act(user, tool);
        if (mode != DRILL_CANADIAN)
        {
            to_chat(user, span_danger(I18n.T("You broke the voice circuit!")));
            mode = DRILL_CANADIAN;
        }
        return TRUE;
    }

    // tg: code/modules/clothing/head/jobs.dm:548
    [DmParams("M", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
            RegisterSignal(user, COMSIG_MOB_SAY, nameof(handle_speech));
        else
            UnregisterSignal(user, COMSIG_MOB_SAY);
    }

    // tg: code/modules/clothing/head/jobs.dm:555
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_MOB_SAY);
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:559
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        string message = Dm.str(speech_args[SPEECH_MESSAGE]);
        if (message.Length > 0 && message[0] != '*')
        {
            switch (mode)
            {
                case DRILL_SHOUTING:
                    message += "!";
                    break;
                case DRILL_YELLING:
                    message += "!!";
                    break;
                case DRILL_CANADIAN:
                    DmList canadian_words = strings("canadian_replacement.json", "canadian") as DmList;

                    foreach (object key in Dm.each(canadian_words))
                    {
                        object value = canadian_words[key];
                        if (islist(value))
                            value = pick(value as DmList);

                        message = replacetextEx(message, Dm.text(" []", uppertext(key)), Dm.text(" []", uppertext(value)));
                        message = replacetextEx(message, Dm.text(" []", capitalize(key)), Dm.text(" []", capitalize(value)));
                        message = replacetextEx(message, Dm.text(" []", key), Dm.text(" []", value));
                    }

                    if (prob(30))
                        message += Dm.str(pick(Dm.list(", eh?", ", EH?")));
                    break;
            }
            speech_args[SPEECH_MESSAGE] = message;
        }
    }
}

// tg: code/modules/clothing/head/jobs.dm:706
public partial class obj_item_clothing_head_utility_head_mirror : obj_item_clothing_head_utility
{
    // tg: code/modules/clothing/head/jobs.dm:716
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -3)); //FISH DOCTOR?!
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:720
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("In a properly lit room, you can use this to examine people's eyes, ears, and mouth <i>closer</i>.")));
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:724
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, slot_flags)))
            RegisterSignal(user, COMSIG_MOB_EXAMINING_MORE, nameof(examining));
        else
            UnregisterSignal(user, COMSIG_MOB_EXAMINING_MORE);
    }

    // tg: code/modules/clothing/head/jobs.dm:731
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_MOB_EXAMINING_MORE);
        return dot;
    }

    // tg: code/modules/clothing/head/jobs.dm:735
    [SignalHandler]
    public virtual void examining(mob_living examiner = null, atom examining = null, DmList examine_list = null, DmList examine_overrides = null)
    {
        if (!ishuman(examining) || examining == examiner || Dm.truthy(examiner.is_blind()) || !Dm.truthy(Waiting.NoSleep(examiner.Adjacent(examining))))
            return;
        mob_living_carbon_human human_examined = (mob_living_carbon_human)examining;
        if (!Dm.truthy(human_examined.get_bodypart(BODY_ZONE_HEAD)))
            return;
        if (!Dm.truthy(examiner.has_light_nearby()))
        {
            Dm.append(ref examine_list, span_warning(I18n.T("You attempt to use your [] to examine []'s head better... but it's too dark. Should've invested in a head lamp.", name, examining)));
            return;
        }
        if (Dm.eq(examiner.dir, examining.dir)) // disallow examine from behind - every other dir is OK
        {
            Dm.append(ref examine_list, span_warning(I18n.T("You attempt to use your [] to examine []'s head better... but [] facing the wrong way.", name, examining, examining.p_theyre())));
            return;
        }

        DmList final_message = Dm.list(I18n.T("You examine []'s head closer with your [], you notice [] []...", examining, name, examining.p_they(), examining.p_have()));
        if (Dm.truthy(human_examined.is_mouth_covered()))
            Dm.append(ref final_message, I18n.T("\tYou can't see [] mouth.", examining.p_their()));
        else
        {
            obj_item_organ_tongue has_tongue = human_examined.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
            float pill_count = 0;
            foreach (datum_action_item_action_activate_pill pill in Dm.each<datum_action_item_action_activate_pill>(human_examined.actions))
                pill_count++;

            if (pill_count >= 1 && has_tongue != null)
                Dm.append(ref final_message, I18n.T("\t[] pill\\s in [] mouth, and \\a [].", pill_count, examining.p_their(), has_tongue));
            else if (pill_count >= 1)
                Dm.append(ref final_message, I18n.T("\t[] pill\\s in [] mouth, but oddly no tongue.", pill_count, examining.p_their()));
            else if (has_tongue != null)
                Dm.append(ref final_message, I18n.T("\t\\A [] in [] mouth - go figure.", has_tongue, examining.p_their()));
            else
                Dm.append(ref final_message, I18n.T("\tNo tongue in [] mouth, oddly enough.", examining.p_their()));
        }

        if (Dm.truthy(human_examined.is_ears_covered()))
            Dm.append(ref final_message, I18n.T("\tYou can't see [] ears.", examining.p_their()));
        else
        {
            obj_item_organ_ears has_ears = human_examined.get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
            if (has_ears != null)
            {
                if (Dm.truthy(has_ears.temporary_deafness))
                    Dm.append(ref final_message, I18n.T("\tDamaged eardrums in [] ear canals.", examining.p_their()));
                else
                    Dm.append(ref final_message, I18n.T("\tA set of [][].", Dm.truthy(has_ears.damage) ? "" : I18n.T("healthy "), has_ears.name));
            }
            else
                Dm.append(ref final_message, I18n.T("\tNo eardrums and empty ear canals... how peculiar."));
        }

        if (Dm.truthy(human_examined.is_eyes_covered()))
            Dm.append(ref final_message, I18n.T("\tYou can't see [] eyes.", examining.p_their()));
        else
        {
            obj_item_organ_eyes has_eyes = human_examined.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            if (has_eyes != null)
                Dm.append(ref final_message, I18n.T("\tA pair of [][].", Dm.truthy(has_eyes.damage) ? "" : I18n.T("healthy "), has_eyes.name));
            else
                Dm.append(ref final_message, I18n.T("\tEmpty eye sockets."));
        }

        Dm.append(ref examine_list, span_notice(I18n.T("<i>[]</i>", jointext(final_message, "\n"))));
    }
}

// tg: code/modules/clothing/head/jobs.dm:832
public partial class obj_item_clothing_head_beret_highlander : obj_item_clothing_head_beret
{
    // tg: code/modules/clothing/head/jobs.dm:836
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HIGHLANDER_TRAIT);
        return dot;
    }
}
