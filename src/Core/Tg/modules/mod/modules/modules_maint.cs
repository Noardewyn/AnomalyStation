// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_maint.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole (the MOD electronic stamp came with gaps P6, D298). The maintenance MOD modules: the springlock
// (faster sealing; water or touching reagents snap it shut on its wearer), the rave visor (a rainbow screen and the jukebox's music),
// the tanner (a spray tan, now and then a dose of radiation), the balloon blower, the paper dispenser (sometimes too crisp), the
// stamper and its electronic stamp, and the atrocinator (flipped gravity: the wearer falls upward, and off a planet into the sky).
// Left with a row (not-translated.md 222): the springlock's achievement (:89, the achievements, D332). The var blocks are generated
// (D111).

///Springlock Mechanism - allows your modsuit to activate faster, but reagents are very dangerous.
// tg: code/modules/mod/modules/modules_maint.dm:4
public partial class obj_item_mod_module_springlock : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_maint.dm:14  var/static/list/gas_connections
    public static DmList gas_connections => TgState.Current.obj_item_mod_module_springlock__gas_connections ??= Dm.list(
        Dm.kv(COMSIG_TURF_EXPOSE, nameof(on_wearer_exposed_gas)));

    // tg: code/modules/mod/modules/modules_maint.dm:19
    public override async DmTask on_install()
    {
        await base.on_install();
        mod.activation_step_time *= step_change;
    }

    // tg: code/modules/mod/modules/modules_maint.dm:23
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        mod.activation_step_time /= step_change;
    }

    // tg: code/modules/mod/modules/modules_maint.dm:27
    public override async DmTask on_part_activation()
    {
        RegisterSignal(mod.wearer, COMSIG_ATOM_EXPOSE_REAGENTS, nameof(on_wearer_exposed));
        await AddComponent(typeof(datum_component_connect_loc_behalf), mod.wearer, gas_connections);
    }

    // tg: code/modules/mod/modules/modules_maint.dm:31
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_ATOM_EXPOSE_REAGENTS);
        qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
    }

    ///Registers the signal COMSIG_MOD_ACTIVATE and calls the proc snap_shut() after a timer
    // tg: code/modules/mod/modules/modules_maint.dm:36
    public virtual void snap_signal()
    {
        if (Dm.truthy(set_off) || Dm.eq(mod.wearer.stat, DEAD))
            return;

        float found_part = FALSE;
        foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts()))
        {
            // Don't snap if no parts besides the MOD itself are active
            if (part.loc != mod && Dm.truthy((mod.get_part_datum(part) as datum_mod_part)?.@sealed))
            {
                found_part = TRUE;
                break;
            }
        }

        if (!Dm.truthy(found_part))
            return;

        to_chat(mod.wearer, span_danger(I18n.T("[] makes an ominous click sound...", this)));
        playsound(this, Dm.resource("sound/items/modsuit/springlock.ogg"), 75, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(snap_shut)), rand(3 * SECONDS, 5 * SECONDS), file: "code/modules/mod/modules/modules_maint.dm", line: 52);
        RegisterSignals(mod, Dm.list(COMSIG_MOD_ACTIVATE, COMSIG_MOD_PART_RETRACTING), nameof(on_activate_spring_block));
        set_off = TRUE;
    }

    ///Calls snap_signal() when exposed to a reagent via VAPOR, PATCH or TOUCH
    // tg: code/modules/mod/modules/modules_maint.dm:57
    [SignalHandler]
    public virtual void on_wearer_exposed(atom source = null, DmList reagents = null, datum_reagents source_reagents = null, object methods = null, object show_message = null)
    {
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(VAPOR, PATCH), TOUCH))))
            return; //remove non-touch reagent exposure
        snap_signal();
    }

    ///Calls snap_signal() when exposed to water vapor
    // tg: code/modules/mod/modules/modules_maint.dm:65
    [SignalHandler]
    public virtual void on_wearer_exposed_gas()
    {
        turf wearer_turf = get_turf(this);
        datum_gas_mixture air = Waiting.NoSleep(wearer_turf.return_air()) as datum_gas_mixture;
        if (Dm.num(air.moles[typeof(datum_gas_water_vapor)]) < 5)
            return; //return if there aren't more than 5 Moles of Water Vapor in the air
        snap_signal();
    }

    ///Signal fired when wearer attempts to activate/deactivate suits
    // tg: code/modules/mod/modules/modules_maint.dm:75
    [SignalHandler]
    public virtual object on_activate_spring_block(datum source = null, object user = null)
    {
        balloon_alert(user as mob, I18n.T("springlocks aren't responding...?"));
        return MOD_CANCEL_ACTIVATE;
    }

    ///Delayed death proc of the suit after the wearer is exposed to reagents
    // tg: code/modules/mod/modules/modules_maint.dm:81
    public virtual async DmTask snap_shut()
    {
        UnregisterSignal(mod, Dm.list(COMSIG_MOD_ACTIVATE, COMSIG_MOD_PART_RETRACTING));
        if (mod.wearer == null) //while there is a guaranteed user when on_wearer_exposed() fires, that isn't the same case for this proc
            return;
        mod.wearer.visible_message(I18n.T("[] inside []'s [] snaps shut, mutilating the user inside!", this, mod.wearer, mod.name), span_userdanger(I18n.T("*SNAP*")));
        await mod.wearer.emote("scream");
        playsound(mod.wearer, Dm.resource("sound/effects/snap.ogg"), 75, TRUE, frequency: 0.5f);
        playsound(mod.wearer, Dm.resource("sound/effects/splat.ogg"), 50, TRUE, frequency: 0.5f);
        // mod.wearer.client?.give_award(/datum/award/achievement/misc/springlock, mod.wearer): the achievements (D332)
        if (mod.wearer.client != null)
            Dm.Deferred("code/modules/mod/modules/modules_maint.dm:89", "snap_shut: client.give_award(/datum/award/achievement/misc/springlock) (the achievements, D332)");

        obj_item_bodypart chest = mod.wearer.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
        if (chest != null)
            await chest.receive_damage(200, forced: TRUE, sharpness: SHARP_POINTY); // Chest always gets hit, from the back piece you're wearing
        foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts()))
        {
            if (part.loc == mod || !Dm.truthy((mod.get_part_datum(part) as datum_mod_part)?.@sealed))
                continue;

            foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(mod.wearer.get_damageable_bodyparts()))
            {
                if (Dm.truthy(Dm.bitand(part.body_parts_covered, bodypart.body_part))) // can hit chest again
                    await bodypart.receive_damage(100, forced: TRUE, sharpness: SHARP_POINTY); //boggers, bogchamp, etc
            }
        }

        if (!HAS_TRAIT(mod.wearer, TRAIT_NODEATH))
        {
            mod.wearer.investigate_log(Dm.text("has been killed by [].", this), INVESTIGATE_DEATHS);
            await mod.wearer.death(); //just in case, for some reason, they're still alive
        }
        flash_color(mod.wearer, flash_color: "#FF0000", flash_time: 10 * SECONDS);
        set_off = FALSE;
    }
}

///Rave Visor - Gives you a rainbow visor and plays jukebox music to you.
// tg: code/modules/mod/modules/modules_maint.dm:107
public partial class obj_item_mod_module_visor_rave : obj_item_mod_module_visor
{
    /// A list of the colors the module can take.
    // tg: code/modules/mod/modules/modules_maint.dm:118  var/static/list/rainbow_order
    public static DmList rainbow_order => TgState.Current.obj_item_mod_module_visor_rave__rainbow_order ??= Dm.list(
        Dm.list(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0),
        Dm.list(1, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0),
        Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0),
        Dm.list(0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0),
        Dm.list(0, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0),
        Dm.list(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0));

    // tg: code/modules/mod/modules/modules_maint.dm:129
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        music_player = Dm.@new<datum_jukebox_single_mob>(this);
        music_player.sound_loops = TRUE;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_maint.dm:134
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref music_player);
        QDEL_NULL(ref rave_screen);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/modules_maint.dm:139
    public override async DmTask on_activation(mob activator = null)
    {
        rave_screen = mod.wearer.add_client_colour(typeof(datum_client_colour_rave), REF(this)) as datum_client_colour;
        rave_screen.update_color(rainbow_order[rave_number]);
        music_player.start_music(mod.wearer);
    }

    // tg: code/modules/mod/modules/modules_maint.dm:144
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        deleting ??= FALSE;
        QDEL_NULL(ref rave_screen);
        if (music_player.active_song_sound == null)
            return;

        music_player.unlisten_all();
        if (Dm.truthy(deleting))
            return;
        Dm.output_to(mod.wearer, sound(Dm.resource("sound/machines/terminal/terminal_off.ogg"), volume: 50, channel: CHANNEL_JUKEBOX)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
    }

    // tg: code/modules/mod/modules/modules_maint.dm:154
    public override object generate_worn_overlay(obj_item source = null, mutable_appearance standing = null)
    {
        object dot = base.generate_worn_overlay(source, standing);
        if (!Dm.truthy(dot))
            return dot;

        mutable_appearance visor_overlay = mod.get_visor_overlay(standing) as mutable_appearance;
        visor_overlay.appearance_flags = Dm.bitor(visor_overlay.appearance_flags, RESET_COLOR);
        if (music_player.active_song_sound != null)
            visor_overlay.color = rainbow_order[rave_number];
        Dm.append(ref dot, visor_overlay);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_maint.dm:165
    public override async DmTask on_active_process(object seconds_per_tick = null)
    {
        rave_number++;
        if (rave_number > length(rainbow_order))
            rave_number = 1;
        await update_clothing_slots();
        rave_screen.update_color(rainbow_order[rave_number]);
    }

    // tg: code/modules/mod/modules/modules_maint.dm:172
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        if (Dm.truthy(length(music_player.songs)))
            dot["selection"] = add_ui_configuration(I18n.T("Song"), "list", music_player.selection.song_name, music_player.songs);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_maint.dm:177
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        switch (key)
        {
            case "selection":
                if (music_player.active_song_sound != null)
                    return;

                datum_track new_song = music_player.songs[value] as datum_track;
                if (QDELETED(this) || !istype(new_song, typeof(datum_track)))
                    return;

                music_player.selection = new_song;
                break;
        }
    }
}

///Tanner - Tans you with spraytan.
// tg: code/modules/mod/modules/modules_maint.dm:190
public partial class obj_item_mod_module_tanner : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_maint.dm:202
    public override async DmTask<object> on_use(mob activator = null)
    {
        playsound(this, Dm.resource("sound/machines/microwave/microwave-end.ogg"), 50, TRUE);
        datum_reagents holder = Dm.@new<datum_reagents>();
        await holder.add_reagent(typeof(datum_reagent_spraytan), 10);
        await holder.trans_to(mod.wearer, 10, methods: VAPOR);
        if (prob(5))
            SSradiation.irradiate(mod.wearer);
        await drain_power(use_energy_cost);
        return null;
    }
}

///Balloon Blower - Blows a balloon.
// tg: code/modules/mod/modules/modules_maint.dm:212
public partial class obj_item_mod_module_balloon : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_maint.dm:226
    public override async DmTask<object> on_use(mob activator = null)
    {
        if (!Dm.truthy(await do_after(mod.wearer, blowing_time, target: mod)))
            return FALSE;
        await mod.wearer.adjust_oxy_loss(oxygen_damage);
        playsound(this, Dm.resource("sound/items/modsuit/inflate_bloon.ogg"), 50, TRUE);
        obj_item balloon = Dm.@new(balloon_path, get_turf(this)) as obj_item;
        await mod.wearer.put_in_hands(balloon);
        await drain_power(use_energy_cost);
        return null;
    }
}

///Paper Dispenser - Dispenses (sometimes burning) paper sheets.
// tg: code/modules/mod/modules/modules_maint.dm:236
public partial class obj_item_mod_module_paper_dispenser : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_maint.dm:250
    public override async DmTask<object> on_use(mob activator = null)
    {
        if (!Dm.truthy(await do_after(mod.wearer, 1 * SECONDS, target: mod)))
            return FALSE;

        obj_item_paper crisp_paper = Dm.@new<obj_item_paper>(get_turf(this));
        crisp_paper.desc = I18n.T("It's crisp and warm to the touch. Must be fresh.");

        obj_structure_table nearby_table = locate<obj_structure_table>(range(1, mod.wearer));
        playsound(get_turf(this), Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        balloon_alert(mod.wearer, I18n.T("dispensed paper[]", nearby_table != null ? I18n.T(" onto table") : ""));

        await mod.wearer.put_in_hands(crisp_paper);
        if (nearby_table != null)
            await mod.wearer.transferItemToLoc(crisp_paper, nearby_table.drop_location(), silent: FALSE);

        // Up to a 30% chance to set the sheet on fire, +2% per sheet made
        if (prob(min(num_sheets_dispensed * 2, 30)))
        {
            if (Dm.@in(crisp_paper, mod.wearer.held_items))
                await mod.wearer.dropItemToGround(crisp_paper, force: TRUE);
            crisp_paper.balloon_alert(mod.wearer, I18n.T("PC LOAD LETTER!"));
            crisp_paper.visible_message(span_warning(I18n.T("[] bursts into flames, it's too crisp!", crisp_paper)));
            await crisp_paper.fire_act(1000, 100);
        }

        await drain_power(use_energy_cost);
        num_sheets_dispensed++;
        return null;
    }
}

// tg: code/modules/mod/modules/modules_maint.dm:291
public partial class obj_item_stamp_mod : obj_item_stamp
{
    // tg: code/modules/mod/modules/modules_maint.dm:296
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        DmList choices = Dm.list();
        DmList icon_states = Dm.list();
        icon_states["Granted"] = "stamp-ok";
        icon_states["Denied"] = "stamp-deny";
        icon_states["Void"] = "stamp-void";
        foreach (object possible_icon_state in Dm.each(icon_states))
        {
            if (!Dm.eq(this.icon_state, icon_states[possible_icon_state]))
                choices[possible_icon_state] = image(this.icon, icon_states[possible_icon_state]);
        }
        object chosen_icon_state = await show_radial_menu(user, user, choices, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), this, user), require_near: TRUE);
        if (Dm.truthy(chosen_icon_state))
        {
            playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE, -3);
            this.icon_state = icon_states[chosen_icon_state] as string;
        }
        return null;
    }

    // tg: code/modules/mod/modules/modules_maint.dm:311
    public virtual object check_menu(datum target = null, mob user = null)
    {
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(user.is_holding(target as obj_item)))
            return FALSE;
        return TRUE;
    }
}

///Atrocinator - Flips your gravity.
// tg: code/modules/mod/modules/modules_maint.dm:317
public partial class obj_item_mod_module_atrocinator : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_maint.dm:400 (file-local, #undef at :411)
    private const float FLY_TIME = 5 * SECONDS;

    // tg: code/modules/mod/modules/modules_maint.dm:332
    public override async DmTask on_activation(mob activator = null)
    {
        // Auto-unbuckle anyone being carried to avoid lag issues
        if (Dm.truthy(length(mod.wearer.buckled_mobs)))
        {
            mod.wearer.visible_message(I18n.T("As [] flips, [] flies off of [] back!", mod.wearer, mod.wearer.buckled_mobs[1], mod.wearer.p_their()));
            mod.wearer.unbuckle_all_mobs();
        }

        playsound(this, Dm.resource("sound/effects/curse/curseattack.ogg"), 50);
        await mod.wearer.AddElement(typeof(datum_element_forced_gravity), NEGATIVE_GRAVITY);
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(check_upstairs));
        RegisterSignal(mod.wearer, COMSIG_MOB_SAY, nameof(on_talk));
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_PREBUCKLE, nameof(on_someone_buckled));
        ADD_TRAIT(mod.wearer, TRAIT_SILENT_FOOTSTEPS, REF(this));
        ADD_TRAIT(mod.wearer, TRAIT_PASSTABLE, REF(this));
        check_upstairs(); //todo at some point flip your screen around
    }

    // tg: code/modules/mod/modules/modules_maint.dm:347
    public override async DmTask<object> deactivate(mob activator = null, float? display_message = null, float? deleting = null)
    {
        display_message ??= TRUE;
        deleting ??= FALSE;
        if (Dm.truthy(you_fucked_up) && !Dm.truthy(deleting))
        {
            to_chat(activator, span_danger(I18n.T("It's too late.")));
            return FALSE;
        }
        return await base.deactivate(activator, display_message, deleting);
    }

    // tg: code/modules/mod/modules/modules_maint.dm:353
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        deleting ??= FALSE;
        if (!Dm.truthy(deleting))
            playsound(this, Dm.resource("sound/effects/curse/curseattack.ogg"), 50);
        mod.wearer.RemoveElement(typeof(datum_element_forced_gravity), NEGATIVE_GRAVITY); // tg: qdel(RemoveElement(...)), and RemoveElement returns nothing
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(mod.wearer, COMSIG_MOB_SAY);
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_PREBUCKLE);
        step_count = 0;
        REMOVE_TRAIT(mod.wearer, TRAIT_SILENT_FOOTSTEPS, REF(this));
        REMOVE_TRAIT(mod.wearer, TRAIT_PASSTABLE, REF(this));
        turf_open_openspace current_turf = get_turf(mod.wearer) as turf_open_openspace;
        if (istype(current_turf, typeof(turf_open_openspace)))
            await current_turf.zFall(mod.wearer, falling_from_move: TRUE);
    }

    // tg: code/modules/mod/modules/modules_maint.dm:367
    [SignalHandler]
    public virtual void check_upstairs(atom_movable source = null, atom oldloc = null, object direction = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        if (Dm.truthy(you_fucked_up) || Dm.num(mod.wearer.has_gravity()) > NEGATIVE_GRAVITY)
            return;

        // Prevent infinite loops when being fireman carried - Stack trace if it does
        if (mod.wearer.buckled != null)
        {
            stack_trace("Atrocinator user is buckled despite protections - this shouldn't happen!", "code/modules/mod/modules/modules_maint.dm", 373);
            return;
        }

        // Prevent infinite loops when carrying someone - Stack trace if it does
        if (Dm.truthy(length(mod.wearer.buckled_mobs)))
        {
            stack_trace("Atrocinator user is carrying someone despite protections - this shouldn't happen!", "code/modules/mod/modules/modules_maint.dm", 378);
            return;
        }

        turf_open current_turf = get_turf(mod.wearer) as turf_open;
        turf turf_above = get_step_multiz(mod.wearer, UP) as turf; // tg's typed var/turf/open/openspace holds whatever turf is above
        if (current_turf != null && istype(turf_above, typeof(turf_open_openspace)))
        {
            Waiting.NoSleep(current_turf.zFall(mod.wearer));
            return;
        }

        else if (turf_above == null && istype(current_turf, typeof(turf_open)) && Dm.truthy(current_turf.planetary_atmos)) //nothing holding you down
        {
            Waiting.Detach(fly_away()); // INVOKE_ASYNC(src, PROC_REF(fly_away))
            return;
        }

        // GAP(lead): SSlag_switch (controllers/subsystem/lag_switch.dm, the lag switch measures of admins) is not declared;
        // its measures are off (as footsteps.cs), so only `forced` stops the step sound here
        if (Dm.truthy(forced))
            return;

        if (!Dm.truthy(step_count % 2))
            playsound(current_turf, Dm.resource("sound/items/modsuit/atrocinator_step.ogg"), 50);
        step_count++;
    }

    // tg: code/modules/mod/modules/modules_maint.dm:402
    public virtual async DmTask fly_away()
    {
        you_fucked_up = TRUE;
        playsound(this, Dm.resource("sound/effects/whirthunk.ogg"), 75);
        to_chat(mod.wearer, span_userdanger(I18n.T("That was stupid.")));
        investigate_log(Dm.text("has flown off into space due to the [].", this), INVESTIGATE_DEATHS);
        await mod.wearer.Stun(FLY_TIME, ignore_canstun: TRUE);
        animate(mod.wearer, Dm.named("time", FLY_TIME), Dm.named("pixel_z", 256), Dm.named("alpha", 0));
        // QDEL_IN(mod.wearer, FLY_TIME)
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), FLY_TIME > GC_FILTER_QUEUE ? WEAKREF(mod.wearer) : mod.wearer), FLY_TIME, file: "code/modules/mod/modules/modules_maint.dm", line: 409);
    }

    // tg: code/modules/mod/modules/modules_maint.dm:413
    [SignalHandler]
    public virtual void on_talk(datum source = null, DmList speech_args = null)
    {
        Dm.combine_at(speech_args, SPEECH_SPANS, "upside_down");
    }

    /// Prevent someone from being buckled to the wearer while atrocinator is active
    // tg: code/modules/mod/modules/modules_maint.dm:418
    [SignalHandler]
    public virtual object on_someone_buckled(datum source = null, mob_living buckled_mob = null, mob_living buckler = null)
    {
        balloon_alert(buckler, buckler == mod.wearer ? I18n.T("you're upside down!") : I18n.T("they're upside down!"));
        return COMPONENT_BLOCK_BUCKLE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mod/modules/modules_maint.dm:14  var/static/list/gas_connections
    internal DmList obj_item_mod_module_springlock__gas_connections;
    // tg: code/modules/mod/modules/modules_maint.dm:118  var/static/list/rainbow_order
    internal DmList obj_item_mod_module_visor_rave__rainbow_order;
}
