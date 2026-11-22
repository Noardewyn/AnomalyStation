// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/flashlight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base flashlight (the owner, 2026-10-17: M4a's flashlight), so the flashlight, the penlights, the seclite, the
// desk lamps and the lanterns turn on and off in a hand, on a belt and on the floor (their light: overlay_lighting.cs, the
// lamps' COMPLEX_LIGHT through update_light). M8 slice D: the penlight's eye and mouth examination (eye_examine,
// mouth_examine, interact_with_atom). Left for their milestones: the cuffable handle (the cuffable_item element is not translated)
// (the live-partial rows of "## Flashlight (M4a)": gaps DV2, below). M7 lighting slice L1 (D168): the flares (with the torches and the candles) and the glowsticks, whole;
// their leaves are the light middleman's flicker (the lighting module, D24), the admin log of a mob set on fire (M10), the
// glowstick's edible component (food, M8), lighting a cigarette from a candle (smoking, M9) and the glowstick fluid's effect
// on a mob that drinks it (live-partial rows, M8). Gaps DV2 (D333): the base's suicide, the light disruptor's
// blackout and a painted flashlight's tinted light, the penlight's medical holosign, the glowstick's suicide, the flashdark and the
// space furnace (with its space protection bubble, datums/proximity_monitor/fields/space_protection.dm). The var blocks are generated (D111), but for the glowstick's `timer_id` (below).

// tg: code/game/objects/items/devices/flashlight.dm:6
public partial class obj_item_flashlight : obj_item
{
    // tg: code/game/objects/items/devices/flashlight.dm:1-4  #define FAILURE, SUCCESS, NO_FUEL, ALREADY_LIT (file-local, #undef at :1123-1126;
    // the candle's code uses them too, so they are internal members of the file's main type, rules 11.4)
    internal const float FAILURE = 0;
    internal const float SUCCESS = 1;
    internal const float NO_FUEL = 2;
    internal const float ALREADY_LIT = 3;

    // tg: code/game/objects/items/devices/flashlight.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(has_closed_handle))
            Dm.Deferred("code/game/objects/items/devices/flashlight.dm:53", "Initialize: AddElement(/datum/element/cuffable_item), handcuffing the flashlight to a hand (the element is not translated)");
        if (Dm.truthy(start_on))
            Waiting.NoSleep(set_light_on(TRUE));
        update_brightness();
        register_context();
        init_slapcrafting();
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:60
    public virtual void init_slapcrafting()
    {
        // tg: code/game/objects/items/devices/flashlight.dm:61  var/static/list/slapcraft_recipe_list (a type-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_flashlight__slapcraft_recipe_list));
    }

    // tg: code/game/objects/items/devices/flashlight.dm:68
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        // single use lights can be toggled on once
        if (held_item == null && (Dm.truthy(toggle_context) || !Dm.truthy(light_on)))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Toggle light";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_flashlight && (Dm.truthy(toggle_context) || !Dm.truthy(light_on)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Toggle light";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:80
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(light_on))
        {
            icon_state = Dm.text("[]-on", Dm.initial(this, (obj_item_flashlight p) => p.icon_state));
            if (inhand_icon_state != null)
                inhand_icon_state = Dm.text("[]-on", Dm.initial(this, (obj_item_flashlight p) => p.inhand_icon_state));
        }
        else
        {
            icon_state = Dm.initial(this, (obj_item_flashlight p) => p.icon_state);
            if (inhand_icon_state != null)
                inhand_icon_state = Dm.initial(this, (obj_item_flashlight p) => p.inhand_icon_state);
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:91
    public virtual void update_brightness()
    {
        update_appearance(UPDATE_ICON);
        if (light_system == COMPLEX_LIGHT)
            update_light();
    }

    // tg: code/game/objects/items/devices/flashlight.dm:96
    public virtual async DmTask<object> toggle_light(mob user = null)
    {
        playsound(this, Dm.truthy(light_on) ? sound_off : sound_on, 40, TRUE);
        // COOLDOWN_FINISHED(src, disabled_time) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(disabled_time <= world.time))
        {
            if (user != null)
                balloon_alert(user, I18n.T("disrupted!"));
            await set_light_on(FALSE);
            update_brightness();
            await update_item_action_buttons();
            return FALSE;
        }
        float old_light_on = light_on;
        await set_light_on(!Dm.truthy(light_on) ? TRUE : FALSE);
        update_brightness();
        await update_item_action_buttons();
        return light_on != old_light_on ? TRUE : FALSE; // If the value of light_on didn't change, return false. Otherwise true.
    }

    // tg: code/game/objects/items/devices/flashlight.dm:111
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        return await toggle_light(user);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:114
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await attack_self(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:118  (gaps DV2)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(user.is_blind()))
        {
            user.visible_message(span_suicide(I18n.T("[] is putting [] close to [] eyes and turning it on... but [] blind!", user, this, user.p_their(), user.p_theyre())));
            return SHAME;
        }
        user.visible_message(span_suicide(I18n.T("[] is putting [] close to [] eyes and turning it on! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return FIRELOSS;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:125
    public virtual async DmTask<object> eye_examine(mob_living_carbon_human patient = null, mob_living user = null)
    {
        DmList dot = Dm.list();
        bool helmet = patient.head is obj_item head && Dm.truthy(Dm.bitand(head.flags_cover, HEADCOVERSEYES));
        bool mask = patient.wear_mask is obj_item wear_mask && Dm.truthy(Dm.bitand(wear_mask.flags_cover, MASKCOVERSEYES));
        bool glasses = patient.glasses is obj_item worn_glasses && Dm.truthy(Dm.bitand(worn_glasses.flags_cover, GLASSESCOVERSEYES));
        if (helmet || mask || glasses)
        {
            to_chat(user, span_warning(I18n.T("You're going to need to remove that [] first!", helmet ? I18n.T("helmet") : mask ? I18n.T("mask") : I18n.T("glasses"))));
            return null;
        }

        obj_item_organ_eyes eyes = patient.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        obj_item_organ brain = patient.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ;
        obj_item_organ_zombie_infection tumor = patient.get_organ_slot(ORGAN_SLOT_ZOMBIE) as obj_item_organ_zombie_infection; //this slot only ever holds zombie tumors, so we can just check if this exists
        object braaaainz = tumor?.causes_damage; //prevents steath tumors (admin bullshittery or romerol) from showing up to preserve stealthiness
        if (eyes == null)
        {
            to_chat(user, span_warning(I18n.T("[] doesn't have any eyes!", patient)));
            return null;
        }

        await patient.flash_act(visual: TRUE, length: Dm.truthy(user.combat_mode) ? 2.5f * SECONDS : 1 * SECONDS); // Apply a 1 second flash effect to the target. The duration increases to 2.5 Seconds if you have combat mode on.

        if (patient == user) //they're using it on themselves
        {
            user.visible_message(span_warning(I18n.T("[] shines [] into [] eyes.", user, this, patient.p_their())), ignored_mobs: user);
            Dm.append(ref dot, span_info(I18n.T("You direct [] to into your eyes:\n", this)));

            if (Dm.truthy(patient.is_blind()))
                Dm.append(ref dot, span_notice_ml(I18n.T("You're not entirely certain what you were expecting...\n")));
            else
                Dm.append(ref dot, span_notice_ml(I18n.T("Trippy!\n")));
        }
        else
        {
            user.visible_message(span_warning(I18n.T("[] directs [] to []'s eyes.", user, this, patient)), ignored_mobs: user);
            Dm.append(ref dot, span_info(I18n.T("You direct [] to []'s eyes:\n", this, patient)));

            if (patient.stat == DEAD || Dm.truthy(patient.is_blind()) || Dm.num(patient.get_eye_protection()) >= FLASH_PROTECTION_WELDER) //this used to be just > but literally nothing accessable in the game gave greater than welder without also covering eyes
                Dm.append(ref dot, span_danger_ml(I18n.T("[] [] don't react to the light!\n", patient.p_Their(), eyes.pupils_name))); //mob is dead
            else if (Dm.num(brain.damage) > 20)
                Dm.append(ref dot, span_danger_ml(I18n.T("[] [] contract unevenly!\n", patient.p_Their(), eyes.pupils_name))); //mob has sustained damage to their brain
            else
                Dm.append(ref dot, span_notice_ml(I18n.T("[] [] narrow.\n", patient.p_Their(), eyes.pupils_name))); //they're okay :D

            if (HAS_TRAIT(patient, TRAIT_XRAY_VISION))
                Dm.append(ref dot, span_danger_ml(I18n.T("[] [] give an eerie glow!\n", patient.p_Their(), eyes.pupils_name))); //mob has X-ray vision
            if (!Dm.eq(eyes.penlight_message, Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ_eyes e) => e.penlight_message))) //prevent default eyes from cluttering text, if this still happens somehow it displays "default message please report"
                Dm.append(ref dot, span_notice_ml(Dm.text("[]\n", await eyes.penlight_examine(user, this))));
            if (Dm.truthy(braaaainz))
                Dm.append(ref dot, span_danger_ml(I18n.T("<b>[] eyes are webbed by fibrous black tendrils!</b>\n", patient.p_Their())));
            if (Dm.truthy(patient.has_status_effect(typeof(datum_status_effect_drugginess))) || Dm.truthy(patient.has_status_effect(typeof(datum_status_effect_trance))))
                Dm.append(ref dot, span_danger_ml(I18n.T("[] [] are responsive, but entirely unfocused.", patient.p_Their(), eyes.pupils_name)));
            if (Dm.truthy(patient.has_status_effect(typeof(datum_status_effect_stoned))))
                Dm.append(ref dot, span_danger_ml(I18n.T("[] eyes are wide, lazy, and bloodshot.", patient.p_Their()))); //this shit is GAS, batman. *wheezing chuckle*
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:173
    public virtual async DmTask<object> mouth_examine(mob_living_carbon_human patient = null, mob_living user = null)
    {
        DmList dot = Dm.list();
        if (Dm.truthy(patient.is_mouth_covered()))
        {
            to_chat(user, span_warning(I18n.T("You're going to need to remove that [] first!", patient.head is obj_item head && Dm.truthy(Dm.bitand(head.flags_cover, HEADCOVERSMOUTH)) ? I18n.T("helmet") : I18n.T("mask"))));
            return null;
        }

        DmList mouth_organs = Dm.list();
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(patient.organs))
        {
            if (Dm.eq(organ.zone, BODY_ZONE_PRECISE_MOUTH))
                Dm.append(ref mouth_organs, organ);
        }
        string organ_list = "";
        int organ_count = (int)LAZYLEN(mouth_organs);
        if (organ_count > 0)
        {
            for (int I = 1; I <= organ_count; I++)
            {
                if (I > 1)
                {
                    if (I == Dm.length(mouth_organs))
                        organ_list += I18n.T(", and ");
                    else
                        organ_list += ", ";
                }
                obj_item_organ organ_to_display = (obj_item_organ)mouth_organs[I];
                organ_list += Dm.eq(organ_to_display.gender, "plural") ? Dm.str(organ_to_display.name) : I18n.T("\\an []", organ_to_display.name);
            }
        }

        int pill_count = 0;
        foreach (object AP in Dm.each(patient.actions))
        {
            if (AP is datum_action_item_action_activate_pill)
                pill_count++;
        }

        if (patient == user) //if we're looking on our own mouth
        {
            bool can_use_mirror = false;
            if (isturf(user.loc))
            {
                obj_structure_mirror mirror = Dm.locate<obj_structure_mirror>(user.loc);
                if (mirror != null)
                {
                    if (Dm.eq(user.dir, NORTH))
                        can_use_mirror = mirror.pixel_y > 0;
                    else if (Dm.eq(user.dir, SOUTH))
                        can_use_mirror = mirror.pixel_y < 0;
                    else if (Dm.eq(user.dir, EAST))
                        can_use_mirror = mirror.pixel_x > 0;
                    else if (Dm.eq(user.dir, WEST))
                        can_use_mirror = mirror.pixel_x < 0;
                }
            }

            patient.visible_message(span_notice(I18n.T("[] directs [] to [] mouth.", patient, this, patient.p_their())), ignored_mobs: user);
            Dm.append(ref dot, span_info_ml(I18n.T("You point [] into your mouth:\n", this)));
            if (!can_use_mirror)
            {
                to_chat(user, span_notice(I18n.T("You can't see anything without a mirror.")));
                return null;
            }
            if (organ_count > 0)
                Dm.append(ref dot, span_notice_ml(I18n.T("Inside your mouth [] [].\n", organ_count > 1 ? I18n.T("are") : I18n.T("is"), organ_list)));
            else
                Dm.append(ref dot, span_notice_ml(I18n.T("There's nothing inside your mouth.\n")));
            if (pill_count > 0)
                Dm.append(ref dot, span_notice_ml(I18n.T("You have [] implanted pill[].\n", pill_count, pill_count > 1 ? "s" : "")));
        }
        else //if we're looking in someone elses mouth
        {
            user.visible_message(span_notice(I18n.T("[] directs [] to []'s mouth.", user, this, patient)), ignored_mobs: user);
            Dm.append(ref dot, span_info_ml(I18n.T("You point [] into []'s mouth:\n", this, patient)));
            if (organ_count > 0)
                Dm.append(ref dot, span_notice_ml(I18n.T("Inside [] mouth [] [].\n", patient.p_their(), organ_count > 1 ? I18n.T("are") : I18n.T("is"), organ_list)));
            else
                Dm.append(ref dot, span_notice_ml(I18n.T("[] doesn't have any organs in [] mouth.\n", patient, patient.p_their())));
            if (pill_count > 0)
                Dm.append(ref dot, span_notice_ml(I18n.T("[] has [] pill[] implanted in [] teeth.\n", patient, pill_count, pill_count > 1 ? "s" : "", patient.p_their())));
        }

        //assess any suffocation damage
        bool hypoxia_status = Dm.num(patient.get_oxy_loss()) > 20;

        if (patient == user)
        {
            if (hypoxia_status)
                Dm.append(ref dot, span_danger_ml(I18n.T("Your lips appear blue!\n"))); //you have suffocation damage
            else
                Dm.append(ref dot, span_notice_ml(I18n.T("Your lips appear healthy.\n"))); //you're okay!
        }
        else
        {
            if (hypoxia_status)
                Dm.append(ref dot, span_danger_ml(I18n.T("[] lips appear blue!\n", patient.p_Their()))); //they have suffocation damage
            else
                Dm.append(ref dot, span_notice_ml(I18n.T("[] lips appear healthy.\n", patient.p_Their()))); //they're okay!
        }

        //assess blood level
        if (patient == user)
            Dm.append(ref dot, span_info_ml(I18n.T("You press a finger to your gums:\n")));
        else
            Dm.append(ref dot, span_info_ml(I18n.T("You press a finger to [] gums:\n", patient.p_their())));

        float cached_blood_volume = Dm.num(patient.get_blood_volume(apply_modifiers: TRUE));

        if (cached_blood_volume <= BLOOD_VOLUME_SAFE && cached_blood_volume > BLOOD_VOLUME_OKAY)
            Dm.append(ref dot, span_danger_ml(I18n.T("Color returns slowly!\n"))); //low blood
        else if (cached_blood_volume <= BLOOD_VOLUME_OKAY)
            Dm.append(ref dot, span_danger_ml(I18n.T("Color does not return!\n"))); //critical blood
        else
            Dm.append(ref dot, span_notice_ml(I18n.T("Color returns quickly.\n"))); //they're okay :D
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:266
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!ishuman(interacting_with))
            return NONE;
        if (!Dm.truthy(light_on))
            return NONE;
        add_fingerprint(user);
        if (Dm.truthy(user.combat_mode) || (!Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_EYES) && !Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_MOUTH)))
            return NONE;
        if ((HAS_TRAIT(user, TRAIT_CLUMSY) || HAS_TRAIT(user, TRAIT_DUMB)) && prob(50)) //too dumb to use flashlight properly
            return ITEM_INTERACT_SKIP_TO_ATTACK; //just hit them in the head

        object dot = ITEM_INTERACT_BLOCKING;
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return dot;
        }
        mob_living_carbon_human scanning = interacting_with as mob_living_carbon_human;
        if (scanning.get_bodypart(BODY_ZONE_HEAD) == null)
        {
            to_chat(user, span_warning(I18n.T("[] doesn't have a head!", scanning)));
            return dot;
        }
        if (light_power < 0.5f)
        {
            to_chat(user, span_warning(I18n.T("[] isn't bright enough to see anything!", this)));
            return dot;
        }

        DmList render_list = Dm.list();
        if (Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_EYES))
            Dm.append(ref render_list, await eye_examine(scanning, user));
        else if (Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_MOUTH))
            Dm.append(ref render_list, await mouth_examine(scanning, user));

        if (Dm.truthy(length(render_list)))
        {
            //display our packaged information in an examine block for easy reading
            to_chat(user, boxed_message(jointext(render_list, "")), type: MESSAGE_TYPE_INFO);
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    /// for directional sprites - so we get the same sprite in the inventory each time we pick one up
    // tg: code/game/objects/items/devices/flashlight.dm:303
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await base.equipped__proc(user, slot, initial);
        await setDir(Dm.initial(this, (obj_item_flashlight p) => p.dir));
        // This is dumb, but if we don't do this then the lighting overlay may be facing the wrong direction depending on how it is picked up
        if (Dm.truthy(user._listen_lookup?[COMSIG_ATOM_DIR_CHANGE]))
            user._SendSignal(COMSIG_ATOM_DIR_CHANGE, Dm.list(user, user.dir, user.dir));
    }

    /// for directional sprites - so when we drop the flashlight, it drops facing the same way the user is facing
    // tg: code/game/objects/items/devices/flashlight.dm:309
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        silent ??= FALSE;
        object dot = await base.dropped(user, silent, __extra);
        if (user != null && dir != user.dir)
            await setDir(user.dir);
        return dot;
    }

    /// when hit by a light disruptor - turns the light off, forces the light to be disabled for a few seconds
    // tg: code/game/objects/items/devices/flashlight.dm:315  (gaps DV2)
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        if (Dm.truthy(light_on))
            await toggle_light();
        disabled_time = world.time + Dm.num(disrupt_duration); // COOLDOWN_START(src, disabled_time, disrupt_duration), inline (rules 11.1 (a))
        return TRUE;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:322  (gaps DV2)
    public override async DmTask update_atom_colour()
    {
        await base.update_atom_colour();
        if (Dm.truthy(ignore_base_color))
            return;
        DmList applied_matrix = cached_color_filter;
        if (!Dm.truthy(applied_matrix))
            applied_matrix = color_transition_filter(color, SATURATION_OVERRIDE) as DmList;
        object or__1 = applied_matrix["space"];
        object new_light_color = apply_matrix_to_color(Dm.initial(this, (obj_item_flashlight p) => p.light_color), applied_matrix["color"] as DmList, Dm.truthy(or__1) ? Dm.num(or__1) : COLORSPACE_RGB);
        set_light_color(new_light_color);
    }
}

// tg: code/game/objects/items/devices/flashlight.dm:330  (gaps DV2: the penlight's medical holosign)
public partial class obj_item_flashlight_pen : obj_item_flashlight
{
    // tg: code/game/objects/items/devices/flashlight.dm:348
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(holosign_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, holosign_cooldown), inline (rules 11.1 (a))
        {
            balloon_alert(user, I18n.T("not ready!"));
            return ITEM_INTERACT_BLOCKING;
        }

        turf target_turf = get_turf(interacting_with);
        mob_living living_target = Dm.locate_type(typeof(mob_living), target_turf) as mob_living;

        if (!Dm.truthy(living_target) || (living_target == user))
            return ITEM_INTERACT_BLOCKING;

        to_chat(living_target, span_boldnotice(I18n.T("[] is offering medical assistance; please halt your actions.", user)));
        Dm.@new<obj_effect_temp_visual_medical_holosign>(target_turf, user); //produce a holographic glow
        holosign_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START(src, holosign_cooldown, 10 SECONDS), inline
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/devices/flashlight.dm:378  (gaps DV2)
public partial class obj_effect_temp_visual_medical_holosign : obj_effect_temp_visual
{
    // tg: code/game/objects/items/devices/flashlight.dm:384
    [DmParams("mapload", "creator")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object creator = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, creator));
        playsound(loc, Dm.resource("sound/machines/ping.ogg"), 50, FALSE); //make some noise!
        if (Dm.truthy(creator))
            visible_message(span_danger(I18n.T("[] created a medical hologram!", creator)));
        return dot;
    }
}

// tg: code/game/objects/items/devices/flashlight.dm:441
public partial class obj_item_flashlight_flare : obj_item_flashlight
{
    // tg: code/game/objects/items/devices/flashlight.dm:474
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (light_system == OVERLAY_LIGHT || light_system == OVERLAY_LIGHT_DIRECTIONAL || light_system == OVERLAY_LIGHT_BEAM) // IS_OVERLAY_LIGHT_SYSTEM (__DEFINES/lighting.dm), inline
        {
            // middleman = new(src, "flashlight"); RegisterSignal(middleman, COMSIG_LIGHT_MIDDLEMAN_UPDATED, PROC_REF(light_updated));
            // middleman.being_overriding_light(): the flare's flicker, the lighting module (D24, rules 13: the client computes
            // light from the replicated light_on, range, power and colour).
            Dm.Deferred("code/game/objects/items/devices/flashlight.dm:476", "Initialize: the light middleman's flicker (the lighting module, D24)");
        }
        if (Dm.truthy(randomize_fuel))
            fuel = rand(10 * MINUTES, 15 * MINUTES);
        if (Dm.truthy(light_on))
        {
            attack_verb_continuous = string_list(Dm.list("burns", "singes")) as DmList;
            attack_verb_simple = string_list(Dm.list("burn", "singe")) as DmList;
            hitsound = Dm.resource("sound/items/tools/welder.ogg");
            force = on_damage;
            damtype = BURN;
            update_brightness();
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:490
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_sulfur), 15));
    }

    // tg: code/game/objects/items/devices/flashlight.dm:493
    public override void init_slapcrafting()
    {
        return;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:496
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        if (middleman != null)
            QDEL_NULL(ref middleman);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:502
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!isliving(target))
            return;
        mob_living victim = (mob_living)target;
        if (Dm.truthy(get_temperature()) && Dm.truthy(victim.ignite_mob()))
            message_admins(Dm.text("[] set [] on fire with [] at []", ADMIN_LOOKUPFLW(user), key_name_admin(victim), this, AREACOORD(user)));
            user.log_message(Dm.text("set [] on fire with []", key_name(victim), this), LOG_ATTACK);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:510  (the user is not passed on: tg's override has no parameters)
    public override async DmTask<object> toggle_light(mob user = null)
    {
        if (Dm.truthy(light_on) || !Dm.truthy(fuel))
            return FALSE;
        object dot = await base.toggle_light();

        name = Dm.text("lit []", Dm.initial(this, (obj_item_flashlight_flare p) => p.name));
        attack_verb_continuous = string_list(Dm.list("burns", "singes")) as DmList;
        attack_verb_simple = string_list(Dm.list("burn", "singe")) as DmList;
        hitsound = Dm.resource("sound/items/tools/welder.ogg");
        force = on_damage;
        damtype = BURN;
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:523
    public virtual void turn_off()
    {
        Waiting.NoSleep(set_light_on(FALSE));
        name = Dm.initial(this, (obj_item_flashlight_flare p) => p.name);
        attack_verb_continuous = Dm.initial(this, (obj_item_flashlight_flare p) => p.attack_verb_continuous);
        attack_verb_simple = Dm.initial(this, (obj_item_flashlight_flare p) => p.attack_verb_simple);
        hitsound = Dm.initial(this, (obj_item_flashlight_flare p) => p.hitsound);
        force = Dm.initial(this, (obj_item_flashlight_flare p) => p.force);
        damtype = Dm.initial(this, (obj_item_flashlight_flare p) => p.damtype);
        update_brightness();
    }

    // tg: code/game/objects/items/devices/flashlight.dm:533
    [SignalHandler]
    public virtual void light_updated(datum source = null)
    {
        // fire_flicker_middleman(middleman): the flare's flicker, the lighting module (D24); no middleman is made (Initialize).
        Dm.Deferred("code/game/objects/items/devices/flashlight.dm:535", "light_updated: fire_flicker_middleman (the lighting module, D24)");
    }

    // tg: code/game/objects/items/devices/flashlight.dm:537
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        if (fuel != INFINITY && Dm.truthy(can_be_extinguished))
            turn_off();
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:542
    public override void update_brightness()
    {
        base.update_brightness();
        inhand_icon_state = Dm.text("[]", Dm.initial(this, (obj_item_flashlight_flare p) => p.inhand_icon_state)) + (Dm.truthy(light_on) ? "-on" : "");
        update_appearance();
    }

    // tg: code/game/objects/items/devices/flashlight.dm:547
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        open_flame(heat);
        fuel = max(fuel.GetValueOrDefault() - Dm.num(seconds_per_tick) * (1 * SECONDS), 0);

        if (!Dm.truthy(fuel) || !Dm.truthy(light_on))
        {
            turn_off();
            STOP_PROCESSING(SSobj, this);

            if (!Dm.truthy(fuel) && trash_type != null)
            {
                Dm.@new(trash_type, loc);
                qdel(this);
            }
        }
        return null;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:559
    public virtual async DmTask<object> ignition(mob user = null)
    {
        if (!Dm.truthy(fuel))
        {
            if (user != null)
                balloon_alert(user, I18n.T("out of fuel!"));
            return NO_FUEL;
        }
        if (Dm.truthy(light_on))
        {
            if (user != null)
                balloon_alert(user, I18n.T("already lit!"));
            return ALREADY_LIT;
        }
        if (!Dm.truthy(await toggle_light()))
            return FAILURE;

        if (fuel != INFINITY)
            START_PROCESSING(SSobj, this);

        return SUCCESS;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:576
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await ignition();
        return await base.fire_act(exposed_temperature, exposed_volume);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:580
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.eq(await ignition(user), SUCCESS))
            user.visible_message(span_notice(I18n.T("[] lights \\the [].", user, this)), span_notice(I18n.T("You light \\the []!", Dm.initial(this, (obj_item_flashlight_flare p) => p.name))));
        return null;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:584
    public override object get_temperature()
    {
        return light_on * heat;
    }
}

// tg: code/game/objects/items/devices/flashlight.dm:587
public partial class obj_item_flashlight_flare_candle : obj_item_flashlight_flare
{
    // tg: code/game/objects/items/devices/flashlight.dm:611
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_floor_placeable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    /**
     * Just checks the wax level of the candle for displaying the correct sprite.
     *
     * This gets called in process() every tick. If the wax level has changed, then we call our update.
     */
    // tg: code/game/objects/items/devices/flashlight.dm:621
    public virtual void check_wax_level()
    {
        if (Dm.in_to(fuel, 25 * MINUTES, INFINITY))
            current_wax_level = 1;
        else if (Dm.in_to(fuel, 15 * MINUTES, 25 * MINUTES))
            current_wax_level = 2;
        else if (Dm.in_to(fuel, 0, 15 * MINUTES))
            current_wax_level = 3;

        if (last_wax_level != current_wax_level)
        {
            last_wax_level = current_wax_level;
            update_appearance(Dm.bitor(UPDATE_ICON, UPDATE_NAME));
        }
    }

    // tg: code/game/objects/items/devices/flashlight.dm:634
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("candle[][]", current_wax_level, Dm.truthy(light_on) ? "_lit" : "");
        inhand_icon_state = Dm.text("candle[]", Dm.truthy(light_on) ? "_lit" : "");
        return dot;
    }

    /**
     * Try to ignite the candle.
     *
     * Candles are ignited a bit differently from flares, in that they must be manually lit from other fire sources.
     * This will perform all the necessary checks to ensure that can happen, and display a message if it worked.
     *
     * Arguments:
     * * obj/item/fire_starter - the item being used to ignite the candle.
     * * mob/user - the user to display a message to.
     */
    // tg: code/game/objects/items/devices/flashlight.dm:649
    public virtual async DmTask<object> try_light_candle(atom fire_starter = null, mob user = null)
    {
        if (!istype(fire_starter, typeof(obj_item)))
            return null;
        if (!istype(user, typeof(mob)))
            return null;

        object success_msg = await ((obj_item)fire_starter).ignition_effect(this, user);
        object ignition_result = null;

        if (Dm.truthy(success_msg))
            ignition_result = await ignition();

        object sw__1 = ignition_result;
        if (Dm.eq(sw__1, SUCCESS))
        {
            update_appearance(Dm.bitor(UPDATE_ICON, UPDATE_NAME));
            user.visible_message(success_msg);
            return SUCCESS;
        }
        else if (Dm.eq(sw__1, ALREADY_LIT))
        {
            balloon_alert(user, I18n.T("already lit!"));
            return ALREADY_LIT;
        }
        else if (Dm.eq(sw__1, NO_FUEL))
        {
            balloon_alert(user, I18n.T("out of fuel!"));
            return NO_FUEL;
        }
        return null;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:673
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(get_temperature()))
        {
            if (istype(tool, typeof(obj_item_cigarette)))
            {
                obj_item_cigarette cig = (obj_item_cigarette)tool;
                if (!Dm.truthy(await cig.attempt_light(user, this, "")))
                    return NONE;
                if (cig.loc == user)
                {
                    user.visible_message(
                        span_rose(I18n.T("[] holds [] [] to [] and lights it, like a true romantic.", user, user.p_their(), cig.name, this)),
                        span_rose(I18n.T("You hold your [] to [] and light it, like a true romantic.", cig.name, this)),
                        visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                }
                else
                {
                    user.visible_message(
                        span_rose(I18n.T("[] lights [] with [], like a true romantic.", user, cig, this)),
                        span_rose(I18n.T("You light [] with [], like a true romantic.", cig, this)),
                        visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                }
                return ITEM_INTERACT_SUCCESS;
            }
            return NONE;
        }
        if (Dm.truthy(await try_light_candle(tool, user)))
            return ITEM_INTERACT_SUCCESS;
        return NONE;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:697
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(get_temperature()))
            return NONE;
        if (Dm.truthy(await try_light_candle(interacting_with, user)))
            return ITEM_INTERACT_SUCCESS;
        return NONE;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:704
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        if (!Dm.truthy(get_temperature()))
            return "";
        if (isitem(A) && ReferenceEquals(A.loc, user))
            return span_rose(I18n.T("[] holds [] in the flame of [], letting it catch fire.", user, A, this));
        return span_rose(I18n.T("[] lights [] ablaze with [], like a true romantic.", user, A, this));
    }

    // tg: code/game/objects/items/devices/flashlight.dm:711
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(light_on) && (fuel != INFINITY || !Dm.truthy(can_be_extinguished))) // can't extinguish eternal candles
        {
            turn_off();
            user.visible_message(span_notice(I18n.T("[] snuffs [].", user, this)));
        }
        return null;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:716
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        check_wax_level();
        return dot;
    }
}

// The syndicate campaign G1c (D316): the EMP flashlight, whole.
// tg: code/game/objects/items/devices/flashlight.dm:814
public partial class obj_item_flashlight_emp : obj_item_flashlight
{
    // tg: code/game/objects/items/devices/flashlight.dm:821
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:825
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:829
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        charge_timer += Dm.num(seconds_per_tick);
        if (charge_timer < charge_delay)
            return FALSE;
        charge_timer -= charge_delay;
        emp_cur_charges = Math.Min(emp_cur_charges + 1, emp_max_charges);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:837
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(light_on) && (Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_EYES) || Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_MOUTH))) // call original attack when examining organs
            await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        return null;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:842
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        if (emp_cur_charges > 0)
        {
            emp_cur_charges -= 1;

            if (interacting_with is mob empd)
            {
                log_combat(user, empd, "attacked", "EMP-light");
                empd.visible_message(span_danger(I18n.T("[] blinks \\the [] at \\the [].", user, this, empd)),
                    span_userdanger(I18n.T("[] blinks \\the [] at you.", user, this)));
            }
            else
                interacting_with.visible_message(span_danger(I18n.T("[] blinks \\the [] at \\the [].", user, this, interacting_with)));
            to_chat(user, span_notice(I18n.T("\\The [] now has [] charge[].", this, emp_cur_charges, emp_cur_charges == 1 ? "" : "s")));
            await interacting_with.emp_act(EMP_HEAVY);
        }
        else
            to_chat(user, span_warning(I18n.T("\\The [] needs time to recharge!", this)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/devices/flashlight.dm:870
public partial class obj_item_flashlight_glowstick : obj_item_flashlight
{
    /// The timer id powering our burning
    // tg: code/game/objects/items/devices/flashlight.dm:897
    // The generated field would be a float? (the converter types a var that holds TIMER_ID_NULL by its value), which a timer id
    // (an object, rules 3.2 V3) does not fit: the hand-written member keeps the id, and the constructor with tg's overrides
    // (flashlight.dm:871-885) is here.
    public object timer_id = TIMER_ID_NULL;

    // tg: code/game/objects/items/devices/flashlight.dm:870
    public obj_item_flashlight_glowstick()
    {
        name = "glowstick";
        desc = "A military-grade glowstick.";
        custom_price = PAYCHECK_LOWER;
        w_class = WEIGHT_CLASS_SMALL;
        light_range = 3.5f;
        light_power = 2;
        light_system = OVERLAY_LIGHT;
        color = LIGHT_COLOR_GREEN;
        icon_state = "glowstick";
        base_icon_state = "glowstick";
        inhand_icon_state = null;
        worn_icon_state = "lightstick";
        sound_on = Dm.resource("sound/effects/wounds/crack2.ogg"); // the cracking sound isn't just for wounds silly
        toggle_context = FALSE;
        ignore_base_color = TRUE;
        has_closed_handle = FALSE;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:887  (a hand-written var block has its own dm_init_vars, types-converter.md 3.4)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = null;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:899
    [DmParams("mapload", "fuel_override", "fuel_type_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object fuel_override = Dm.arg(__extra, 0);
        object fuel_type_override = Dm.arg(__extra, 1);
        max_fuel = fuel_override == null ? rand(20, 25) : Dm.num(fuel_override);
        if (Dm.truthy(fuel_type_override))
            fuel_type = fuel_type_override as Type;
        create_reagents(max_fuel + oxygen_added, Dm.bitor(DRAWABLE, INJECTABLE));
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(fuel_type, max_fuel));
        object dot = base.Initialize(mapload, Dm.forward(__extra, fuel_override, fuel_type_override));
        set_light_color(color);
        // AddComponentFrom(SOURCE_EDIBLE_INNATE, /datum/component/edible, food_flags = FOOD_NO_EXAMINE, volume = reagents.total_volume,
        // bite_consumption = round(reagents.total_volume / (rand(20, 30) * 0.1))): eating a glowstick (food, M8)
        Dm.Deferred("code/game/objects/items/devices/flashlight.dm:906", "Initialize: AddComponentFrom(SOURCE_EDIBLE_INNATE, /datum/component/edible, ...): a glowstick is edible (food, M8)");
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_change));
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:916
    public override async DmTask<object> grind_results()
    {
        DmList dot = Dm.list(Dm.kv(typeof(datum_reagent_phenol), 15), Dm.kv(typeof(datum_reagent_hydrogen), 10));
        if (!Dm.truthy(light_on))
            dot[typeof(datum_reagent_oxygen)] = 5;
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:921
    public virtual object get_fuel()
    {
        return (reagents as datum_reagents).get_reagent_amount(fuel_type);
    }

    /// Burns down the glowstick by the specified time
    /// Returns the amount of time we need to burn before a visual change will occur
    // tg: code/game/objects/items/devices/flashlight.dm:926
    public virtual async DmTask<object> burn_down(object amount = null)
    {
        amount ??= 0;
        datum_reagents own_reagents = reagents as datum_reagents;
        if (!Dm.truthy(await own_reagents.remove_all(amount)))
        {
            await turn_off();
            return 0;
        }

        float fuel = Dm.num(get_fuel());
        if (fuel <= 0)
        {
            await turn_off();
            return 0;
        }

        await own_reagents.expose_temperature(Dm.num(amount) * temp_per_fuel);
        if (fuel >= max_fuel * 0.4f)
        {
            set_light_range(3);
            set_light_power(1.5f);
        }
        else if (fuel >= max_fuel * 0.3f)
        {
            set_light_range(2);
            set_light_power(1.25f);
        }
        else if (fuel >= max_fuel * 0.2f)
            set_light_power(1);
        else if (fuel >= max_fuel * 0.1f)
        {
            set_light_range(1.5f);
            set_light_power(0.5f);
        }

        return round(own_reagents.total_volume * 0.1f);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:951
    public virtual async DmTask burn_loop(object amount = null)
    {
        amount ??= 0;
        timer_id = TIMER_ID_NULL;
        float burn_next = Dm.num(await burn_down(amount));
        if (burn_next <= 0)
            return;
        timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(burn_loop), burn_next), burn_next * MINUTES, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), TIMER_OVERRIDE), file: "code/game/objects/items/devices/flashlight.dm", line: 955);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:958
    public virtual async DmTask turn_on()
    {
        datum_reagents own_reagents = reagents as datum_reagents;
        await own_reagents.add_reagent(typeof(datum_reagent_oxygen), oxygen_added);
        await set_light_on(TRUE); // Just in case
        datum_action toggle = Dm.locate_type(typeof(datum_action_item_action_toggle_light), actions) as datum_action;
        // No sense having a toggle light action that we don't use eh?
        if (toggle != null)
            remove_item_action(toggle);
        await burn_loop(round(own_reagents.total_volume * 0.1f));
    }

    // tg: code/game/objects/items/devices/flashlight.dm:967
    public virtual async DmTask turn_off()
    {
        datum_action toggle = Dm.locate_type(typeof(datum_action_item_action_toggle_light), actions) as datum_action;
        if (Dm.truthy(get_fuel()) && toggle == null)
            await add_item_action(typeof(datum_action_item_action_toggle_light));
        if (!Dm.eq(timer_id, TIMER_ID_NULL))
        {
            deltimer(timer_id);
            timer_id = TIMER_ID_NULL;
        }
        await set_light_on(FALSE);
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:977
    [SignalHandler]
    public virtual void on_reagent_change(datum source = null)
    {
        if (!Dm.truthy(get_fuel()) && Dm.truthy(light_on))
            Waiting.NoSleep(turn_off());
    }

    // tg: code/game/objects/items/devices/flashlight.dm:983
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.num(get_fuel()) <= 0 ? "-empty" : "");
        inhand_icon_state = Dm.text("[][]", base_icon_state, (Dm.num(get_fuel()) > 0 && Dm.truthy(light_on)) ? "-on" : "");
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:988
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.num(get_fuel()) <= 0 && !Dm.truthy(light_on))
            return dot;

        mutable_appearance glowstick_overlay = mutable_appearance(icon, "glowstick-glow") as mutable_appearance;
        glowstick_overlay.color = color;
        Dm.append(ref dot, glowstick_overlay);
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:997
    public override async DmTask<object> toggle_light(mob user = null)
    {
        if (Dm.num(get_fuel()) <= 0)
            return FALSE;
        if (Dm.truthy(light_on))
            return FALSE;
        return await base.toggle_light(user);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1004
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.num(get_fuel()) <= 0)
        {
            balloon_alert(user, I18n.T("glowstick is spent!"));
            return null;
        }
        if (Dm.truthy(light_on))
        {
            balloon_alert(user, I18n.T("already lit!"));
            return null;
        }

        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
        {
            user.visible_message(span_notice(I18n.T("[] cracks and shakes [].", user, this)), span_notice(I18n.T("You crack and shake [], turning it on!", this)));
            await turn_on();
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1017  (gaps DV2)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!Dm.truthy(get_fuel()))
        {
            user.visible_message(span_suicide(I18n.T("[] is trying to squirt []'s fluids into [] eyes... but it's empty!", user, this, user.p_their())));
            return SHAME;
        }
        obj_item_organ_eyes eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (!Dm.truthy(eyes))
        {
            user.visible_message(span_suicide(I18n.T("[] is trying to squirt []'s fluids into [] eyes... but [] []n't have any!", user, this, user.p_their(), user.p_they(), user.p_do())));
            return SHAME;
        }
        user.visible_message(span_suicide(I18n.T("[] is squirting []'s fluids into [] eyes! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        await burn_loop(get_fuel());
        return FIRELOSS;
    }
}

// Gaps H3 (the bar): the disco machine's spotlights.
// tg: code/game/objects/items/devices/flashlight.dm:1059
public partial class obj_item_flashlight_spotlight : obj_item_flashlight
{
    // tg: code/game/objects/items/devices/flashlight.dm:1078
    [DmParams("mapload", "_light_range", "_light_power", "_light_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object _light_range = Dm.arg(__extra, 0);
        object _light_power = Dm.arg(__extra, 1);
        object _light_color = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, __extra);
        if (_light_range != null)
        {
            base_light_range = Dm.num(_light_range);
            set_light_range(_light_range);
        }
        if (_light_power != null)
            set_light_power(_light_power);
        if (_light_color != null)
            set_light_color(_light_color);
        return dot;
    }
}

// tg: code/game/objects/items/devices/flashlight.dm:1088  (gaps DV2: the flashdark's bubble of darkness, a negative light)
public partial class obj_item_flashlight_flashdark : obj_item_flashlight
{
    // tg: code/game/objects/items/devices/flashlight.dm:1101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_overlay_lighting), dark_light_range, dark_light_power, Dm.named("force", TRUE)));
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1105
    public override void update_brightness()
    {
        base.update_brightness();
        Waiting.NoSleep(set_light(dark_light_range, dark_light_power));
    }
}

// tg: code/game/objects/items/devices/flashlight.dm:1130  (gaps DV2: the space furnace, a lamp whose pyroclastic core blows a bubble
// of breathable air, the space protection field, for fifteen minutes)
public partial class obj_item_flashlight_lamp_space_bubble : obj_item_flashlight_lamp
{
    private const int SLOWDOWN_ON = 1; // tg: flashlight.dm:1129

    // tg: code/game/objects/items/devices/flashlight.dm:1160
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE)));
        update_appearance(UPDATE_DESC);
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1166
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(space_bubble))
            QDEL_NULL(ref space_bubble);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1171
    public override void init_slapcrafting()
    {
        return;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1174
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(light_on))
            worn_icon_state = Dm.text("[]-on", Dm.initial(this, (obj_item_flashlight_lamp_space_bubble p) => p.worn_icon_state));
        else
            worn_icon_state = Dm.initial(this, (obj_item_flashlight_lamp_space_bubble p) => p.worn_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1181
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (Dm.truthy(installed_pyro_core))
        {
            desc = Dm.initial(this, (obj_item_flashlight_lamp_space_bubble p) => p.desc);
            return dot;
        }
        desc = Dm.initial(this, (obj_item_flashlight_lamp_space_bubble p) => p.desc) + I18n.T(" Requires a pyroclastic anomaly core to function.");
        return dot;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1188
    public override object get_temperature()
    {
        return Dm.num(light_on) * heat;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1191
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(light_on) && tool is obj_item_cigarette cigarette)
        {
            if (!Dm.truthy(await cigarette.attempt_light(user, this, I18n.T("[] lights up \\the [] using the burning coming out of the []. Damn.", user, tool, this))))
                return NONE;
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is not obj_item_assembly_signaler_anomaly_pyro || Dm.truthy(installed_pyro_core))
            return NONE;
        user.balloon_alert(user, I18n.T("core inserted"));
        qdel(tool);
        installed_pyro_core = TRUE;
        playsound(this, Dm.resource("sound/machines/crate/crate_open.ogg"), 50, FALSE);
        update_appearance(UPDATE_DESC);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/flashlight.dm:1206
    public override async DmTask<object> toggle_light(mob user = null)
    {
        if (!Dm.truthy(installed_pyro_core))
        {
            user.balloon_alert(user, I18n.T("core missing!"));
            return FALSE;
        }
        datum_gas_mixture environment = loc != null ? await loc.return_air() as datum_gas_mixture : null;
        float affected_pressure = Dm.num(environment.return_pressure());
        if (!Dm.truthy(light_on) && (affected_pressure < ONE_ATMOSPHERE - 1))
        {
            user.balloon_alert(user, I18n.T("[] pressure!", affected_pressure < HAZARD_LOW_PRESSURE ? I18n.T("no") : I18n.T("low")));
            return FALSE;
        }
        object dot = await base.toggle_light(user);
        if (Dm.truthy(light_on))
        {
            if (space_bubble is datum_proximity_monitor_advanced_bubble_space_protection)
                QDEL_NULL(ref space_bubble);
            bubble_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(start_bubble_close), I18n.T("dies down...")), bubble_duration, Dm.bitor(Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/game/objects/items/devices/flashlight.dm", line: 1220);
            space_bubble = Dm.@new<datum_proximity_monitor_advanced_bubble_space_protection>(this, 4, FALSE, this);
            slowdown = SLOWDOWN_ON;
            drag_slowdown = SLOWDOWN_ON;
            await user.update_equipment_speed_mods();
            return dot;
        }
        await close_bubble(user);
        return dot;
    }

    ///Uses an arg for special flavortext to be balloon alerted to everyone, then gives an extra 5 seconds before killing the bubble.
    // tg: code/game/objects/items/devices/flashlight.dm:1228
    public virtual void start_bubble_close(object special_flavortext = null)
    {
        if (Dm.truthy(special_flavortext))
            balloon_alert_to_viewers(special_flavortext);
        mob_living potential_mob = recursive_loc_check(this, typeof(mob_living)) as mob_living; // recursive_loc_check(src, /mob/living) || null
        _addtimer(Dm.@new<datum_callback>(this, nameof(toggle_light), potential_mob), 5 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_DELETE_ME), file: "code/game/objects/items/devices/flashlight.dm", line: 1232);
    }

    ///Closes the bubble and cleans up after itself. Optional 'user' arg for the mob turning us off.
    // tg: code/game/objects/items/devices/flashlight.dm:1235
    public virtual async DmTask close_bubble(mob user = null)
    {
        QDEL_NULL(ref space_bubble);
        if (Dm.truthy(bubble_timer))
            deltimer(bubble_timer);
        slowdown = Dm.initial(this, (obj_item_flashlight_lamp_space_bubble p) => p.slowdown);
        drag_slowdown = Dm.initial(this, (obj_item_flashlight_lamp_space_bubble p) => p.drag_slowdown);
        if (Dm.truthy(user))
            await user.update_equipment_speed_mods();
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/devices/flashlight.dm:61
    internal DmList obj_item_flashlight__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_flashlight_eyes));
}
