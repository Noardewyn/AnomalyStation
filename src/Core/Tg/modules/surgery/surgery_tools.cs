// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/surgery_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J5 (D298): the surgical tools as items, with their behaviours, sounds, examines and modes; gaps P1 (D298): the drapes' surgery aid,
// the cyborg's surgical processor (loading disks and the operating computer's operations) and the cruel scalpel's bane; the whole file.

// tg: code/modules/surgery/surgery_tools.dm:1
public partial class obj_item_retractor : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:20
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;
}

// tg: code/modules/surgery/surgery_tools.dm:32
public partial class obj_item_hemostat : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:53
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;
}

// tg: code/modules/surgery/surgery_tools.dm:65
public partial class obj_item_cautery : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:87
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;

    // tg: code/modules/surgery/surgery_tools.dm:90
    public override async DmTask<object> ignition_effect(atom ignitable_atom = null, mob user = null)
        => span_rose(I18n.T("[] touches the end of [] to \\the [], igniting it with a puff of smoke.", user, this, ignitable_atom));
}

// tg: code/modules/surgery/surgery_tools.dm:102
public partial class obj_item_cautery_advanced : obj_item_cautery
{
    // tg: code/modules/surgery/surgery_tools.dm:120
    public override object get_all_tool_behaviours() => Dm.list(TOOL_CAUTERY, TOOL_DRILL);

    // tg: code/modules/surgery/surgery_tools.dm:123
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", force),
            Dm.named("throwforce_on", throwforce),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", w_class),
            Dm.named("clumsy_check", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Toggles between drill and cautery and gives feedback to the user.
     */
    // tg: code/modules/surgery/surgery_tools.dm:140
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (Dm.truthy(active))
        {
            tool_behaviour = TOOL_DRILL;
            set_light_color(LIGHT_COLOR_BLUE);
        }
        else
        {
            tool_behaviour = TOOL_CAUTERY;
            set_light_color(LIGHT_COLOR_ORANGE);
        }

        balloon_alert(user, I18n.T("lenses set to []", Dm.truthy(active) ? "drill" : "mend"));
        playsound(user != null ? user : this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/surgery/surgery_tools.dm:154
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("It's set to [] mode.", Dm.eq(tool_behaviour, TOOL_CAUTERY) ? "mending" : "drilling")));
        return dot;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:158
public partial class obj_item_surgicaldrill : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:188
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;

    // tg: code/modules/surgery/surgery_tools.dm:191
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] rams [] into [] chest! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        _addtimer(Dm.@new<datum_callback>(user, nameof(mob_living_carbon.gib), DROP_ALL_REMAINS), 2.5f * SECONDS, file: "code/modules/surgery/surgery_tools.dm", line: 193);
        user.SpinAnimation(3, 10);
        playsound(user, Dm.resource("sound/machines/juicer.ogg"), 20, TRUE);
        return MANUAL_SUICIDE;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:208
public partial class obj_item_scalpel : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:240
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 8 * SECONDS * Dm.num(toolspeed)),
            Dm.named("effectiveness", 100),
            Dm.named("bonus_modifier", 0)));
        Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:252
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;

    // tg: code/modules/surgery/surgery_tools.dm:255
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is slitting [] [] with []! It looks like [] trying to commit suicide!", user, user.p_their(), pick("wrists", "throat", "stomach"), this, user.p_theyre())));
        return BRUTELOSS;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:268
public partial class obj_item_circular_saw : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:297
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 4 * SECONDS * Dm.num(toolspeed)),
            Dm.named("effectiveness", 100),
            Dm.named("bonus_modifier", 5),
            Dm.named("butcher_sound", Dm.resource("sound/items/weapons/circsawhit.ogg"))));
        //saws are very accurate and fast at butchering
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", Dm.list(typeof(datum_crafting_recipe_chainsaw)))));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:313
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;
}

// tg: code/modules/surgery/surgery_tools.dm:327
public partial class obj_item_surgical_drapes : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:344
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_surgery_aid), name));
        return dot;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:353
public partial class obj_item_surgical_processor : obj_item //allows medical cyborgs to scan and initiate advanced surgeries
{
    // tg: code/modules/surgery/surgery_tools.dm:363
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_surgery_aid), Dm.initial(typeof(obj_item_surgical_drapes), (obj_item o) => o.name))); // i guess it's a drape dispenser
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:367
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Equip the processor in one of your active modules to access downloaded advanced surgeries.")));
        Dm.append(ref dot, span_boldnotice(I18n.T("Advanced surgeries available:")));
        //list of downloaded surgeries' names
        DmList surgeries_names = Dm.list();
        foreach (datum_surgery_operation downloaded_surgery in Dm.each_cast<datum_surgery_operation>(GLOB.operations.get_instances_from(loaded_surgeries)))
            Dm.append(ref surgeries_names, Dm.text("[]", capitalize(downloaded_surgery.name)));
        Dm.append(ref dot, span_notice(Dm.text("[]", english_list(surgeries_names))));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:377
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
        {
            UnregisterSignal(user, COMSIG_LIVING_OPERATING_ON);
            return;
        }
        RegisterSignal(user, COMSIG_LIVING_OPERATING_ON, nameof(check_surgery), @override: TRUE);
    }

    // tg: code/modules/surgery/surgery_tools.dm:384
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_LIVING_OPERATING_ON);
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:388
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom design_holder = interacting_with;
        if (!istype(design_holder, typeof(obj_item_disk_surgery)) && !istype(design_holder, typeof(obj_machinery_computer_operating)))
            return NONE;
        balloon_alert(user, I18n.T("copying designs..."));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 25, TRUE);
        if (Dm.truthy(await do_after(user, 1 * SECONDS, target: design_holder)))
        {
            if (istype(design_holder, typeof(obj_item_disk_surgery)))
            {
                obj_item_disk_surgery surgery_disk = (obj_item_disk_surgery)design_holder;
                Dm.combine(ref loaded_surgeries, surgery_disk.surgeries);
            }
            else
            {
                obj_machinery_computer_operating surgery_computer = (obj_machinery_computer_operating)design_holder;
                Dm.combine(ref loaded_surgeries, surgery_computer.advanced_surgeries);
            }
            playsound(this, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 25, TRUE);
            downloaded = TRUE;
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/surgery/surgery_tools.dm:406
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(downloaded))
            Dm.append(ref dot, mutable_appearance(this.icon, "+downloaded"));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:411
    [SignalHandler]
    public virtual void check_surgery(datum source = null, atom_movable operating_on = null, DmList operations = null)
    {
        Dm.combine(ref operations, loaded_surgeries);
    }
}

// tg: code/modules/surgery/surgery_tools.dm:416
public partial class obj_item_scalpel_advanced : obj_item_scalpel
{
    // tg: code/modules/surgery/surgery_tools.dm:436
    public override object get_all_tool_behaviours() => Dm.list(TOOL_SAW, TOOL_SCALPEL);

    // tg: code/modules/surgery/surgery_tools.dm:439
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", Dm.num(force) + 1),
            Dm.named("throwforce_on", throwforce),
            Dm.named("throw_speed_on", throw_speed),
            Dm.named("sharpness_on", sharpness),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", w_class),
            Dm.named("clumsy_check", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Toggles between saw and scalpel and updates the light / gives feedback to the user.
     */
    // tg: code/modules/surgery/surgery_tools.dm:458
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (Dm.truthy(active))
        {
            tool_behaviour = TOOL_SAW;
            set_light_color(LIGHT_COLOR_ORANGE);
        }
        else
        {
            tool_behaviour = TOOL_SCALPEL;
            set_light_color(LIGHT_COLOR_BLUE);
        }

        balloon_alert(user, I18n.T("[] bone-cutting mode", Dm.truthy(active) ? "enabled" : "disabled"));
        playsound(user != null ? user : this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/surgery/surgery_tools.dm:472
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("It's set to [] mode.", Dm.eq(tool_behaviour, TOOL_SCALPEL) ? "scalpel" : "saw")));
        return dot;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:476
public partial class obj_item_retractor_advanced : obj_item_retractor
{
    // tg: code/modules/surgery/surgery_tools.dm:490
    public override object get_all_tool_behaviours() => Dm.list(TOOL_HEMOSTAT, TOOL_RETRACTOR);

    // tg: code/modules/surgery/surgery_tools.dm:493
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", force),
            Dm.named("throwforce_on", throwforce),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", w_class),
            Dm.named("clumsy_check", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Toggles between retractor and hemostat and gives feedback to the user.
     */
    // tg: code/modules/surgery/surgery_tools.dm:510
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        tool_behaviour = Dm.truthy(active) ? TOOL_HEMOSTAT : TOOL_RETRACTOR;
        balloon_alert(user, I18n.T("gears set to []", Dm.truthy(active) ? "clamp" : "retract"));
        playsound(user != null ? user : this, Dm.resource("sound/items/tools/change_drill.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/surgery/surgery_tools.dm:518
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("It resembles a [].", Dm.eq(tool_behaviour, TOOL_RETRACTOR) ? "retractor" : "hemostat")));
        return dot;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:522
public partial class obj_item_shears : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:542
    public override async DmTask<object> attack__proc(mob_living amputee = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!iscarbon(amputee) || Dm.truthy(user.combat_mode))
            return await base.attack__proc(amputee, user, modifiers, attack_modifiers, __extra);

        if (Dm.eq(user.zone_selected, BODY_ZONE_CHEST))
            return await base.attack__proc(amputee, user, modifiers, attack_modifiers, __extra);

        mob_living_carbon patient = amputee as mob_living_carbon;

        if (HAS_TRAIT(patient, TRAIT_NODISMEMBER))
        {
            to_chat(user, span_warning(I18n.T("The patient's limbs look too sturdy to amputate.")));
            return null;
        }

        object candidate_name = null;
        obj_item_organ tail_snip_candidate = null;
        obj_item_bodypart limb_snip_candidate = null;

        if (Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_GROIN))
        {
            tail_snip_candidate = patient.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ;
            if (tail_snip_candidate == null)
            {
                to_chat(user, span_warning(I18n.T("[] does not have a tail.", patient)));
                return null;
            }
            candidate_name = tail_snip_candidate.name;
        }
        else
        {
            limb_snip_candidate = patient.get_bodypart(check_zone(user.zone_selected) as string) as obj_item_bodypart;
            if (limb_snip_candidate == null)
            {
                to_chat(user, span_warning(I18n.T("[] is already missing that limb, what more do you want?", patient)));
                return null;
            }
            candidate_name = limb_snip_candidate.name;
        }

        float amputation_speed_mod = 1;

        patient.visible_message(span_danger(I18n.T("[] begins to secure [] around []'s [].", user, this, patient, candidate_name)), span_userdanger(I18n.T("[] begins to secure [] around your []!", user, this, candidate_name)));
        playsound(get_turf(patient), Dm.resource("sound/items/tools/ratchet.ogg"), 20, TRUE);
        if (Dm.truthy(IS_UNCONSCIOUS(patient)) || HAS_TRAIT(patient, TRAIT_INCAPACITATED)) //if you're incapacitated (due to paralysis, a stun, being in staminacrit, etc.), critted, unconscious, or dead, it's much easier to properly line up a snip
            amputation_speed_mod *= 0.5f;
        if (!Dm.eq(patient.stat, DEAD) && Dm.truthy(patient.has_status_effect(typeof(datum_status_effect_jitter)))) //jittering will make it harder to secure the shears, even if you can't otherwise move
            amputation_speed_mod *= 1.5f; //15*0.5*1.5=11.25, so staminacritting someone who's jittering (from, say, a stun baton) won't give you enough time to snip their head off, but staminacritting someone who isn't jittering will
        if (HAS_MIND_TRAIT(user, TRAIT_MORBID))
            amputation_speed_mod *= 0.7f; //its morbin time

        if (Dm.truthy(await do_after(user, Dm.num(toolspeed) * 15 * SECONDS * amputation_speed_mod, patient)))
        {
            playsound(get_turf(patient), Dm.resource("sound/items/weapons/bladeslice.ogg"), 250, TRUE);
            if (Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_GROIN)) //OwO
            {
                await tail_snip_candidate.Remove(patient);
                await tail_snip_candidate.forceMove(get_turf(patient));
            }
            else
                await limb_snip_candidate.dismember();
            user.visible_message(span_danger(I18n.T("[] violently slams shut, amputating []'s [].", this, patient, candidate_name)), span_notice(I18n.T("You amputate []'s [] with [].", patient, candidate_name, this)));
            user.log_message(Dm.text("[] has amputated []'s [] with []", user, patient, candidate_name, this), LOG_GAME);
            patient.log_message(Dm.text("[]'s [] has been amputated by [] with []", patient, candidate_name, user, this), LOG_GAME);
        }

        if (HAS_MIND_TRAIT(user, TRAIT_MORBID)) //Freak
            await user.add_mood_event("morbid_dismemberment", typeof(datum_mood_event_morbid_dismemberment));
        return null;
    }

    // tg: code/modules/surgery/surgery_tools.dm:598
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is pinching []self with \\the []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        float timer = 1 * SECONDS;
        foreach (obj_item_bodypart thing in Dm.each<obj_item_bodypart>((user as mob_living_carbon)?.get_bodyparts() as DmList))
        {
            if (Dm.eq(thing.body_part, CHEST))
                continue;
            _addtimer(Dm.@new<datum_callback>(thing, nameof(obj_item_bodypart.dismember)), timer, file: "code/modules/surgery/surgery_tools.dm", line: 603);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(playsound), user, Dm.resource("sound/items/weapons/bladeslice.ogg"), 70), timer, file: "code/modules/surgery/surgery_tools.dm", line: 604);
            timer += 1 * SECONDS;
        }
        await sleep(timer);
        return BRUTELOSS;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:610
public partial class obj_item_bonesetter : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:629
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;
}

// tg: code/modules/surgery/surgery_tools.dm:637
public partial class obj_item_blood_filter : obj_item
{
    // tg: code/modules/surgery/surgery_tools.dm:659
    public override object get_surgery_tool_overlay(object tray_extended = null) => surgical_tray_overlay;

    // tg: code/modules/surgery/surgery_tools.dm:662
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BloodFilter", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/surgery/surgery_tools.dm:668
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        DmList names = Dm.list();
        dot["whitelist"] = names;
        foreach (object key in Dm.each(whitelist))
            names.Add(whitelist[key]);
        return dot;
    }

    // tg: code/modules/surgery/surgery_tools.dm:675
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        dot = TRUE;
        switch (action)
        {
            case "add":
                object selected_reagent = await tgui_input_list(usr, I18n.T("Select reagent to filter"), I18n.T("Whitelist reagent"), GLOB.name2reagent);
                if (!Dm.truthy(selected_reagent))
                    return FALSE;

                object chem_id = GLOB.name2reagent[selected_reagent];
                if (!Dm.truthy(chem_id))
                    return FALSE;

                if (!Dm.@in(chem_id, whitelist))
                    whitelist[chem_id] = selected_reagent;
                break;
            case "remove":
                object chem_name = @params["reagent"];
                object removed_id = get_chem_id(chem_name);
                whitelist.Remove(removed_id);
                break;
        }
        return dot;
    }
}

// tg: code/modules/surgery/surgery_tools.dm:732
public partial class obj_item_scalpel_cruel : obj_item_scalpel
{
    // tg: code/modules/surgery/surgery_tools.dm:739
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_UNDEAD), Dm.named("damage_multiplier", 2))); //Just in case one of the tennants get uppity
        return dot;
    }
}
