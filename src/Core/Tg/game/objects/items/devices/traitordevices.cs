// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/traitordevices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the traitor devices: the mind batterer, the radioactive microlaser with its window, the
// stealth belts' action, the radio jammer and its makeshift copy (radios near an active jammer fail, the disruptor wave turns
// radios off), the penbang, the flash camera and the dagger boots; is_within_radio_jammer_range was M9 slice D's. G4d: the
// toolbox sentry turret (`/obj/machinery/porta_turret/syndicate/toolbox`). The jammer's screentip is a live-partial row; the
// bomber logs are rows.

/*

Miscellaneous traitor devices

BATTERER

RADIOACTIVE MICROLASER

*/

/*

The Batterer, like a flashbang but 50% chance to knock people over. Can be either very
effective or pretty fucking useless.

*/
// tg: code/game/objects/items/devices/traitordevices.dm:18
public partial class obj_item_batterer : obj_item
{
    // tg: code/game/objects/items/devices/traitordevices.dm:36
    [DmParams("user", "flag", "emp")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (user == null)
            return null;

        if (times_used >= max_uses)
        {
            to_chat(user, span_danger(I18n.T("The mind batterer has been burnt out!")));
            return null;
        }

        log_combat(user, null, "knocked down people in the area", this);

        foreach (mob_living_carbon_human M in Dm.each<mob_living_carbon_human>(urange(10, user, 1)))
        {
            if (prob(50))
            {
                await M.Paralyze(rand(200, 400));
                to_chat(M, span_userdanger(I18n.T("You feel a tremendous, paralyzing wave flood your mind.")));
            }
            else
            {
                to_chat(M, span_userdanger(I18n.T("You feel a sudden, electric jolt travel through your head.")));
            }
        }

        playsound(this.loc, Dm.resource("sound/misc/interference.ogg"), 50, TRUE);
        to_chat(user, span_notice(I18n.T("You trigger [].", this)));
        times_used += 1;
        if (times_used >= max_uses)
            icon_state = "battererburnt";
        return null;
    }
}

/*
		The radioactive microlaser, a device disguised as a health analyzer used to irradiate people.

		The strength of the radiation is determined by the 'intensity' setting, while the delay between
	the scan and the irradiation kicking in is determined by the wavelength.

		Each scan will cause the microlaser to have a brief cooldown period. Higher intensity will increase
	the cooldown, while higher wavelength will decrease it.

		Wavelength is also slightly increased by the intensity as well.
*/
// tg: code/game/objects/items/devices/traitordevices.dm:72
public partial class obj_item_healthanalyzer_rad_laser : obj_item_healthanalyzer
{
    // tg: code/game/objects/items/devices/traitordevices.dm:79
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = null;
        if (!Dm.truthy(stealth) || !Dm.truthy(irradiate))
            dot = await base.interact_with_atom(interacting_with, user, modifiers);

        if (!ishuman(interacting_with) || !Dm.truthy(irradiate))
            return dot;

        mob_living_carbon_human human_target = (mob_living_carbon_human)interacting_with;
        if (human_target != null && !Dm.truthy(used) && Dm.truthy(SSradiation.wearing_rad_protected_clothing(human_target))) //intentionally not checking for TRAIT_RADIMMUNE here so that tatortot can still fuck up and waste their cooldown.
        {
            to_chat(user, span_warning(I18n.T("[]'s clothing is fully protecting [] from irradiation!", interacting_with, interacting_with.p_them())));
            return Dm.bitor(dot, ITEM_INTERACT_BLOCKING);
        }

        if (!Dm.truthy(used))
        {
            log_combat(user, interacting_with, "irradiated", this);
            object cooldown = get_cooldown();
            used = TRUE;
            icon_state = "health1";
            _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown_varset_used)), Dm.num(cooldown), file: "code/game/objects/items/devices/traitordevices.dm", line: 95); // VARSET_CALLBACK(src, used, FALSE)
            _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown_varset_icon_state)), Dm.num(cooldown), file: "code/game/objects/items/devices/traitordevices.dm", line: 96); // VARSET_CALLBACK(src, icon_state, "health")
            to_chat(user, span_warning(I18n.T("Successfully irradiated [].", interacting_with)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(radiation_aftereffect), interacting_with, intensity), (intensity + wavelength * 4) * 5, file: "code/game/objects/items/devices/traitordevices.dm", line: 98);
            return Dm.bitor(dot, ITEM_INTERACT_SUCCESS);
        }

        to_chat(user, span_warning(I18n.T("The radioactive microlaser is still recharging.")));
        return Dm.bitor(dot, ITEM_INTERACT_BLOCKING);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:95  VARSET_CALLBACK(src, used, FALSE) (___callbackvarset is not declared: GAP(lead))
    public virtual void cooldown_varset_used() => used = FALSE;

    // tg: code/game/objects/items/devices/traitordevices.dm:96  VARSET_CALLBACK(src, icon_state, "health")
    public virtual void cooldown_varset_icon_state() => icon_state = "health";

    // tg: code/game/objects/items/devices/traitordevices.dm:105
    public virtual async DmTask radiation_aftereffect(mob_living M = null, object passed_intensity = null)
    {
        if (QDELETED(M) || !ishuman(M) || HAS_TRAIT(M, TRAIT_RADIMMUNE))
            return;

        if (Dm.num(passed_intensity) >= 5)
            await M.apply_effect(round(Dm.num(passed_intensity) / 0.075f), EFFECT_UNCONSCIOUS); //to save you some math, this is a round(intensity * (4/3)) second long knockout
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:112
    public virtual object get_cooldown()
    {
        return round(max(10, stealth * 30 + intensity * 5 - wavelength / 4));
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:115
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await interact(user);
        return null;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:118
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        await ui_interact(user);
        return null;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:121
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:124
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RadioactiveMicrolaser");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:130
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["irradiate"] = irradiate;
        data["stealth"] = stealth;
        data["scanmode"] = scanmode;
        data["intensity"] = intensity;
        data["wavelength"] = wavelength;
        data["on_cooldown"] = used;
        data["cooldown"] = DisplayTimeText(get_cooldown());
        return data;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:141
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "irradiate"))
        {
            irradiate = Dm.truthy(irradiate) ? FALSE : TRUE;
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "stealth"))
        {
            stealth = Dm.truthy(stealth) ? FALSE : TRUE;
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "scanmode"))
        {
            scanmode = Dm.truthy(scanmode) ? FALSE : TRUE;
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "radintensity"))
        {
            object target = @params["target"];
            float? adjust = text2num(@params["adjust"]);
            if (Dm.eq(target, "min"))
            {
                target = 1;
                dot = TRUE;
            }
            else if (Dm.eq(target, "max"))
            {
                target = 20;
                dot = TRUE;
            }
            else if (Dm.truthy(adjust))
            {
                target = intensity + adjust.Value;
                dot = TRUE;
            }
            else if (text2num(target) != null)
            {
                target = text2num(target);
                dot = TRUE;
            }

            if (Dm.truthy(dot))
            {
                target = round(Dm.num(target));
                intensity = clamp(Dm.num(target), 1, 20);
            }
        }
        else if (Dm.eq(sw__1, "radwavelength"))
        {
            object target = @params["target"];
            float? adjust = text2num(@params["adjust"]);
            if (Dm.eq(target, "min"))
            {
                target = 0;
                dot = TRUE;
            }
            else if (Dm.eq(target, "max"))
            {
                target = 120;
                dot = TRUE;
            }
            else if (Dm.truthy(adjust))
            {
                target = wavelength + adjust.Value;
                dot = TRUE;
            }
            else if (text2num(target) != null)
            {
                target = text2num(target);
                dot = TRUE;
            }

            if (Dm.truthy(dot))
            {
                target = round(Dm.num(target));
                wavelength = clamp(Dm.num(target), 0, 120);
            }
        }
        return dot;
    }
}

// tg: code/game/objects/items/devices/traitordevices.dm:203
public partial class datum_action_item_action_stealth_mode : datum_action_item_action
{
    // tg: code/game/objects/items/devices/traitordevices.dm:223
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return stealth_engaged;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:226
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        START_PROCESSING(SSobj, this);
        await build_all_button_icons(UPDATE_BUTTON_STATUS);
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:231
    public override async DmTask Remove(mob remove_from = null)
    {
        if (owner != null && Dm.truthy(stealth_engaged))
            await stealth_off();
        STOP_PROCESSING(SSobj, this);
        await base.Remove(remove_from);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:237
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (Dm.truthy(stealth_engaged))
            await stealth_off();
        else
            await stealth_on();
        return TRUE;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:244
    public virtual async DmTask stealth_on()
    {
        animate(owner, Dm.named("alpha", get_alpha()), Dm.named("time", 0.5f * SECONDS));
        apply_wibbly_filters(owner);
        stealth_engaged = TRUE;
        await build_all_button_icons(Dm.bitor(UPDATE_BUTTON_STATUS, UPDATE_BUTTON_BACKGROUND));
        owner.balloon_alert(owner, I18n.T("stealth mode engaged"));
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:251
    public virtual async DmTask stealth_off()
    {
        owner.alpha = Dm.num(Dm.initial(owner, nameof(atom.alpha)));
        remove_wibbly_filters(owner);
        stealth_engaged = FALSE;
        await build_all_button_icons(Dm.bitor(UPDATE_BUTTON_STATUS, UPDATE_BUTTON_BACKGROUND));
        owner.balloon_alert(owner, I18n.T("stealth mode disengaged"));
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:258
    public virtual object get_alpha()
    {
        return clamp(255 - 255 * charge / max_charge, min_alpha, 255);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:261
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(Dm.num(seconds_per_tick)), null); // set waitfor = FALSE, inherited from /datum/process (waiting-model.md 5.4)
    }

    private async DmTask<object> process__body(float seconds_per_tick)
    {
        if (!Dm.truthy(stealth_engaged))
        {
            // Recharge over time
            charge = min(max_charge, charge + max_charge * 0.04f * seconds_per_tick);
            await build_all_button_icons(UPDATE_BUTTON_STATUS);
            return null;
        }

        if (charge <= 0)
        {
            await stealth_off();
            return null;
        }

        turf our_turf = get_turf(owner);
        if (our_turf != null && Dm.truthy(our_turf.check_lumcount_above(0.3f)))
        {
            // Decay charge while invisible+ in the light
            charge = max(0, charge - max_charge * 0.05f * seconds_per_tick);
            await build_all_button_icons(UPDATE_BUTTON_STATUS);
        }
        else if (Dm.truthy(recharge_while_active))
        {
            // Return charage while invisible + in the darkness + recharge_while_active
            charge = min(max_charge, charge + max_charge * 0.1f * seconds_per_tick);
            await build_all_button_icons(UPDATE_BUTTON_STATUS);
        }

        animate(owner, Dm.named("alpha", get_alpha()), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_PARALLEL));
        return null;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:285
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        await base.update_button_status(current_button, force);
        current_button.maptext_x = 9;
        current_button.maptext = MAPTEXT_TINY_UNICODE(Dm.text("[]%", round(charge / max_charge * 100, 0.01f)));
    }
}

public partial class GlobalProcs
{
    /// Checks if a given atom is in range of a radio jammer, returns TRUE if it is.
    // tg: code/game/objects/items/devices/traitordevices.dm:317
    public static bool is_within_radio_jammer_range(atom source = null)
    {
        foreach (obj_item_jammer jammer in Dm.each_cast<obj_item_jammer>(GLOB.active_jammers))
        {
            if (IN_GIVEN_RANGE(source, jammer, Dm.num(jammer.range)))
                return true;
        }
        return false;
    }
}

// tg: code/game/objects/items/devices/traitordevices.dm:323
public partial class obj_item_jammer : obj_item
{
    // tg: code/game/objects/items/devices/traitordevices.dm:340
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:349
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!(jam_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, jam_cooldown)
        {
            user.balloon_alert(user, I18n.T("on cooldown!"));
            return dot;
        }

        user.balloon_alert(user, I18n.T("disruptor wave released!"));
        to_chat(user, span_notice(I18n.T("You release a disruptor wave, disabling all nearby radio devices.")));
        foreach (atom potential_owner in Dm.each<atom>(view(disruptor_range, user)))
            await disable_radios_on(potential_owner, ignore_syndie: TRUE);
        jam_cooldown = world.time + jam_cooldown_duration; // COOLDOWN_START(src, jam_cooldown, jam_cooldown_duration)
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:361
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        to_chat(user, span_notice(I18n.T("You [] [].", Dm.truthy(active) ? I18n.T("deactivate") : I18n.T("activate"), this)));
        user.balloon_alert(user, I18n.T("[] the jammer", Dm.truthy(active) ? I18n.T("deactivated") : I18n.T("activated")));
        active = Dm.truthy(active) ? FALSE : TRUE;
        if (Dm.truthy(active))
        {
            if (!Dm.@in(this, GLOB.active_jammers))
                GLOB.active_jammers.Add(this); // GLOB.active_jammers |= src
        }
        else
        {
            GLOB.active_jammers.Remove(this);
        }
        update_appearance();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:375
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);

        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        if (!Dm.@in(interacting_with, view(disruptor_range, user)))
        {
            user.balloon_alert(user, I18n.T("out of reach!"));
            return dot;
        }

        interacting_with.balloon_alert(user, I18n.T("radio disrupted!"));
        to_chat(user, span_notice(I18n.T("You release a directed disruptor wave, disabling all radio devices on [].", interacting_with)));
        await disable_radios_on(interacting_with);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:391
    public virtual async DmTask disable_radios_on(atom target = null, float? ignore_syndie = null)
    {
        ignore_syndie ??= FALSE;
        DmList contents_and_target = (target.get_all_contents() as DmList).Copy();
        contents_and_target.Add(target);
        foreach (obj_item_radio radio in Dm.each<obj_item_radio>(contents_and_target))
        {
            if (Dm.truthy(ignore_syndie) && Dm.truthy(Dm.bitand(radio.special_channels, RADIO_SPECIAL_SYNDIE)))
                continue;
            radio.set_broadcasting(FALSE, actual_setting: FALSE);
        }
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:397
    public override object Destroy(float? force = null)
    {
        GLOB.active_jammers.Remove(this);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/devices/traitordevices.dm:401
public partial class obj_item_jammer_makeshift : obj_item_jammer
{
    // tg: code/game/objects/items/devices/traitordevices.dm:408
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/devices/traitordevices.dm:413
public partial class obj_machinery_porta_turret_syndicate_toolbox : obj_machinery_porta_turret_syndicate
{
    // tg: code/game/objects/items/devices/traitordevices.dm:426
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(underlays, image(icon: icon, icon_state: Dm.text("[]_frame", base_icon_state)));
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:430
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(faction_check_atom(user)))
        {
            Dm.append(dot, span_notice(I18n.T("You can repair it by <b>left-clicking</b> with a combat wrench.")));
            Dm.append(dot, span_notice(I18n.T("You can fold it by <b>right-clicking</b> with a combat wrench.")));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:436
    public override async DmTask<object> target(atom_movable target = null)
    {
        if (!Dm.truthy(target))
            return null;

        if (Dm.truthy(await shootAt(target)))
            await setDir(get_dir(@base, target));

        return TRUE;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:445
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_wrench_combat)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(tool.toolspeed)) // This is a check for the laser wrench being off, I think
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(user.combat_mode))
        {
            balloon_alert(user, I18n.T("deconstructing..."));
            if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 20)))
                return ITEM_INTERACT_BLOCKING;

            await deconstruct(TRUE);
            tool.play_tool_sound(this, 50);
            balloon_alert(user, I18n.T("deconstructed!"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.eq(atom_integrity, max_integrity))
        {
            balloon_alert(user, I18n.T("already repaired!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("repairing..."));
        object dot = ITEM_INTERACT_BLOCKING; // I'm doing this such that at least one successful repair considers the interaction a success
        while (!Dm.eq(atom_integrity, max_integrity))
        {
            if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 20)))
                return dot;

            await repair_damage(10);
            dot = ITEM_INTERACT_SUCCESS;
        }

        balloon_alert(user, I18n.T("repaired!"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:479
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(disassembled))
        {
            atom_movable old_toolbox = toolbox;
            toolbox = null;
            await old_toolbox.forceMove(drop_location() as atom);
        }
        else
            Dm.@new<obj_effect_gibspawner_robot>(drop_location());

        return await base.on_deconstruction(disassembled);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:490
    public override object Destroy(float? force = null)
    {
        toolbox = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:494
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (Dm.eq(gone, toolbox))
        {
            toolbox = null;
            qdel(this);
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:500
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(faction_check_atom(user)))
            return await base.ui_status(user, state);

        return UI_CLOSE;
    }
}

/// Flashbang disguised as a pen
// tg: code/game/objects/items/devices/traitordevices.dm:509
public partial class obj_item_pen_penbang : obj_item_pen
{
    // tg: code/game/objects/items/devices/traitordevices.dm:513
    public override object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        object dot = base.on_transform(source, user, active);
        float det_time = 1 * SECONDS + 4 * SECONDS * (Dm.num(degrees) / 90);
        if (user != null)
        {
            to_chat(user, span_warning(I18n.T("You prime the penbang! []!", capitalize(DisplayTimeText(det_time)))));
            log_bomber(user, "has primed a", this, "(penbang) for detonation");
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate), user), det_time, file: "code/game/objects/items/devices/traitordevices.dm", line: 519);
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:521
    public virtual async DmTask detonate(mob user = null)
    {
        obj_item_grenade_flashbang bang = Dm.@new<obj_item_grenade_flashbang>(get_turf(this));
        await bang.detonate();
        qdel(this);
    }
}

/// A camera disguised as a flash
// tg: code/game/objects/items/devices/traitordevices.dm:527
public partial class obj_item_camera_flash : obj_item_camera
{
    // tg: code/game/objects/items/devices/traitordevices.dm:531
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        internal_flash = Dm.@new<obj_item_assembly_flash_handheld>(this);
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:535
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref internal_flash);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:539
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        // i guess this is a normal camera now. shouldn't happen, though
        if (gone == internal_flash)
            internal_flash = null;
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:545
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (isliving(interacting_with))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:551
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        return internal_flash != null ? await internal_flash.attack__proc(target_mob, user) : null;
    }
}

/// Jackboots with a dagger embedded into them - changes your kicks to be stab attacks, potetnially causing bleeding
// tg: code/game/objects/items/devices/traitordevices.dm:555
public partial class obj_item_clothing_shoes_jackboots_dagger : obj_item_clothing_shoes_jackboots
{
    // tg: code/game/objects/items/devices/traitordevices.dm:559
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);

        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_FEET)) || !(user is mob_living living_user))
            return;
        modified_bodyparts.Add(living_user.get_bodypart(BODY_ZONE_L_LEG));
        modified_bodyparts.Add(living_user.get_bodypart(BODY_ZONE_R_LEG));
        foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(modified_bodyparts))
        {
            bodypart.unarmed_sharpness = Dm.num(Dm.bitor(bodypart.unarmed_sharpness, SHARP_EDGED));
            bodypart.unarmed_attack_effect = ATTACK_EFFECT_SLASH;
            RegisterSignals(bodypart, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_QDELETING), nameof(clear_modification));
        }
        RegisterSignal(living_user, COMSIG_CARBON_POST_ATTACH_LIMB, nameof(modify_legs));
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:572
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_CARBON_POST_ATTACH_LIMB);
        foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(modified_bodyparts.Copy()))
            clear_modification(bodypart);
        return dot;
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:578
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        await base.handle_deconstruct(disassembled);
        Dm.@new<obj_item_switchblade_extended>(drop_location());
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:582
    [SignalHandler]
    public virtual void clear_modification(obj_item_bodypart bodypart = null, params object[] __extra)
    {
        UnregisterSignal(bodypart, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_QDELETING));
        bodypart.unarmed_sharpness = Dm.num(Dm.initial(bodypart, nameof(obj_item_bodypart.unarmed_sharpness)));
        bodypart.unarmed_attack_effect = Dm.initial(bodypart, nameof(obj_item_bodypart.unarmed_attack_effect)) as string;
        modified_bodyparts.Remove(bodypart);
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:590
    [SignalHandler]
    public virtual void modify_legs(datum source = null, obj_item_bodypart bodypart = null, params object[] __extra)
    {
        if (Dm.@in(bodypart, modified_bodyparts))
            return;
        if (!istype(bodypart, typeof(obj_item_bodypart_leg)))
            return;

        modified_bodyparts.Add(bodypart);
        bodypart.unarmed_sharpness = Dm.num(Dm.bitor(bodypart.unarmed_sharpness, SHARP_EDGED));
        RegisterSignal(bodypart, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_QDELETING), nameof(clear_modification));
    }

    // tg: code/game/objects/items/devices/traitordevices.dm:602
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (Dm.truthy(user.is_holding(this)))
            dot.Add(span_notice(I18n.T("Upon closer inspection, you notice a dagger embedded into the sole.")));
        return dot;
    }
}
