// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/game.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only try_move_adjacent (gaps TP1: the hand teleporter), get_area_name (M4b slice 2: the air vents and scrubbers are named after their area, vent_pump.dm:315; its
// format_text branch with M7 slice 3: an APC's name) and the
// image helpers and overlay flicks of :83-150 (M4v slice 3: add_image_to_client(s), remove_image_from_client(s),
// flick_overlay_global, /atom/flick_overlay, /atom/movable/flick_visual and /atom/flick_overlay_view with the /area override).
// M8 slice B: send_tip_of_the_round. M8 slice C: lavaland_equipment_pressure_check. Syndicate A1: considered_alive, considered_exiled, considered_afk. Gaps E1b: power_fail. DV580: screen_text (the dev auto-respawn's countdown). The other game helpers are translated with the code that uses them.

public static partial class GlobalProcs
{
    /**
     * Checks if the target is in the given location (a typepath or an atom), at any depth of its locs; returns that location or null.
     */
    // tg: code/__HELPERS/game.dm:214  (silicons SI6: the pAI's holder)
    public static atom recursive_loc_check(atom_movable target = null, object type = null)
    {
        atom atom_to_find = null;
        if (ispath(type))
        {
            atom_to_find = target;
            if (istype(atom_to_find, type as Type))
                return atom_to_find;
            while (!istype(atom_to_find, type as Type))
            {
                if (atom_to_find.loc == null)
                    return null;
                atom_to_find = atom_to_find.loc;
            }
        }
        else if (isatom(type))
        {
            atom_to_find = target;
            if (atom_to_find == type)
                return atom_to_find;
            while (atom_to_find != type)
            {
                if (atom_to_find.loc == null)
                    return null;
                atom_to_find = atom_to_find.loc;
            }
        }
        return atom_to_find;
    }

    // tg: code/__HELPERS/game.dm:23
    public static mob get_mob_by_key(object key = null)
    {
        object ckey = Dm.ckey(key as string);
        foreach (mob player_mob in Dm.each_cast<mob>(GLOB.player_list))
        {
            if (Dm.eq(player_mob.ckey, ckey))
                return player_mob;
        }
        return null;
    }

    // tg: code/__HELPERS/game.dm:12  (gaps TP1: the hand teleporter's portal set beside its user)
    public static async DmTask<object> try_move_adjacent(atom_movable atom_to_move = null, object trydir = null)
    {
        var atom_turf = get_turf(atom_to_move) as turf;
        if (Dm.truthy(trydir))
        {
            if (Dm.truthy(await atom_to_move.Move(get_step(atom_turf, Dm.num(trydir)), null)))
                return TRUE;
        }
        foreach (object direction in Dm.each(Dm.sub(GLOB.cardinals, trydir)))
        {
            if (Dm.truthy(await atom_to_move.Move(get_step(atom_turf, Dm.num(direction)), null)))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/__HELPERS/game.dm:5
    public static object get_area_name(atom checked_atom = null, float? format_text = null)
    {
        format_text ??= FALSE;
        area checked_area = isarea(checked_atom) ? checked_atom as area : get_area(checked_atom) as area;
        if (checked_area == null)
            return null;
        if (Dm.truthy(format_text))
            return GlobalProcs.format_text(checked_area.name);
        return checked_area.name;
    }
}

public static partial class GlobalProcs
{
    /// Adds an image to a client's `.images`. Useful as a callback.
    // tg: code/__HELPERS/game.dm:83
    public static void add_image_to_client(image image_to_remove = null, client add_to = null)
    {
        if (add_to != null)
            Dm.append(add_to.images, image_to_remove);
    }

    /// Like add_image_to_client, but will add the image from a list of clients
    // tg: code/__HELPERS/game.dm:87
    public static void add_image_to_clients(image image_to_remove = null, DmList show_to = null)
    {
        foreach (client add_to in Dm.each<client>(show_to))
            Dm.append(add_to.images, image_to_remove);
    }

    /// Removes an image from a client's `.images`. Useful as a callback.
    // tg: code/__HELPERS/game.dm:92
    public static void remove_image_from_client(image image_to_remove = null, client remove_from = null)
    {
        if (remove_from != null)
            Dm.remove(remove_from.images, image_to_remove);
    }

    /// Like remove_image_from_client, but will remove the image from a list of clients
    // tg: code/__HELPERS/game.dm:96
    public static void remove_image_from_clients(object image_to_remove = null, DmList hide_from = null) // object: an image or a list of images (client.images -= list, t_scanner.dm:62)
    {
        foreach (client remove_from in Dm.each<client>(hide_from))
            Dm.remove(remove_from.images, image_to_remove);
    }

    /// Add an image to a list of clients and calls a proc to remove it after a duration
    // tg: code/__HELPERS/game.dm:101
    public static void flick_overlay_global(object image_to_show = null, DmList show_to = null, object duration = null) // object: an image or a list of images (client.images += list, t_scanner.dm:62)
    {
        if (!Dm.truthy(show_to) || !Dm.truthy(length(show_to)) || !Dm.truthy(image_to_show))
            return;
        foreach (client add_to in Dm.each<client>(show_to))
            Dm.append(add_to.images, image_to_show);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.remove_image_from_clients), image_to_show, show_to), Dm.num(duration), TIMER_CLIENT_TIME, file: "code/__HELPERS/game.dm", line: 106);
    }
}

public static partial class GlobalProcs
{
    // DEVIATION(DV91): D173, port-only. The T-ray scanner and the scanner goggles' T-ray, pipe connection and thermal modes refresh
    // their images every SSobj fire (2 s) and tg shows each set for less (0.8 s, 1.5 s, 1 s), so the view blinks. Here a refresh
    // of a source ("t_ray", ...) takes the client's previous set of that source off in the same tick it shows the new one (no
    // gap, never two sets), and the new set stays for max(duration, SSobj.wait + 0.5 s) to bridge to the next refresh; when the
    // refreshes stop the last set goes by its own timer. An empty set only clears the source. Cached images shown again (the
    // pipe arrows) are safe: an expiry removes only the set it was made for, and only while that set is still the source's.
    public static void steady_flick_overlay(client C, string source, DmList images, float duration)
    {
        if (C.dm_steady_images.TryGetValue(source, out DmList previous))
        {
            C.dm_steady_images.Remove(source);
            remove_image_from_clients(previous, Dm.list(C));
        }
        if (!Dm.truthy(length(images)))
            return;
        C.dm_steady_images[source] = images;
        foreach (object I in Dm.each(images))
            Dm.append(C.images, I);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(steady_image_expire), C, source, images), Math.Max(duration, SSobj.wait + 5), TIMER_CLIENT_TIME, file: "code/__HELPERS/game.dm", line: 106);
    }

    public static void steady_image_expire(client C, string source, DmList images)
    {
        if (C == null || !C.dm_steady_images.TryGetValue(source, out DmList current) || !ReferenceEquals(current, images))
            return;
        C.dm_steady_images.Remove(source);
        remove_image_from_clients(images, Dm.list(C));
    }
}

public partial class client : datum
{
    // DEVIATION(DV91): port-only. The image set each source of steady_flick_overlay shows this client now.
    [NotHashed]
    public readonly Dictionary<string, DmList> dm_steady_images = new();
}

public partial class atom : datum
{
    ///Flicks a certain overlay onto an atom, handling icon_state strings
    // tg: code/__HELPERS/game.dm:109
    public virtual void flick_overlay(object image_to_show = null, DmList show_to = null, object duration = null, object layer = null)
    {
        image passed_image = istext(image_to_show)
            ? image(icon, this, image_to_show, layer)
            : image_to_show as image;

        flick_overlay_global(passed_image, show_to, duration);
    }

    /// Takes the passed in MA/icon_state, mirrors it onto ourselves, and displays that in world for duration seconds
    /// Returns the displayed object, you can animate it and all, but you don't own it, we'll delete it after the duration
    // tg: code/__HELPERS/game.dm:124
    // display: object (decided, M4v slice 3, rules 3.4: an icon_state text, buttons.dm:303, or an image or mutable
    // appearance, items.dm:1536, told apart by istext at :129). The local passed_appearance holds an /image as well (tg's
    // callers pass image() results; the shim's mutable_appearance is an image), so it is typed image.
    public virtual object flick_overlay_view(object display = null, object duration = null)
    {
        if (!Dm.truthy(display))
            return null;

        image passed_appearance = istext(display)
            ? mutable_appearance(icon, (string)display, layer) as image
            : display as image;

        // If you don't give it a layer, we assume you want it to layer on top of this atom
        // Because this is vis_contents, we need to set the layer manually (you can just set it as you want on return if this is a problem)
        if (passed_appearance.layer == FLOAT_LAYER)
            passed_appearance.layer = layer + 0.1f;
        // This is faster then pooling. I promise
        atom_movable_flick_visual visual = Dm.@new<atom_movable_flick_visual>();
        visual.appearance = passed_appearance;
        visual.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        // I hate /area
        // (tg types the holder as /atom/movable to reach vis_contents on a turf; in the port vis_contents is /atom's.)
        atom lies_to_children = this;
        Dm.append(lies_to_children.vis_contents, visual);
        // QDEL_IN_CLIENT_TIME(visual, duration) (__DEFINES/qdel.dm:62), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), visual), Dm.num(duration), Dm.bitor(TIMER_STOPPABLE, TIMER_CLIENT_TIME), file: "code/__HELPERS/game.dm", line: 144);
        return visual;
    }
}

// tg: code/__HELPERS/game.dm:151 (syndicate package A3, D316: Dynamic's population counts)
public static partial class GlobalProcs
{
    ///Get active players who are playing in the round
    // tg: code/__HELPERS/game.dm:151
    public static object get_active_player_list(float? alive_check = null, float? afk_check = null, float? human_check = null)
    {
        alive_check ??= FALSE;
        afk_check ??= FALSE;
        human_check ??= FALSE;
        DmList active_players = Dm.list();
        foreach (mob player_mob in Dm.each_cast<mob>(GLOB.player_list))
        {
            if (player_mob?.client == null)
                continue;
            if (Dm.truthy(alive_check) && Dm.eq(player_mob.stat, DEAD))
                continue;
            if (Dm.truthy(afk_check) && Dm.truthy(player_mob.client.is_afk()))
                continue;
            if (Dm.truthy(human_check) && !ishuman(player_mob))
                continue;
            if (isnewplayer(player_mob)) // exclude people in the lobby
                continue;
            if (isobserver(player_mob)) // Ghosts are fine if they were playing once (didn't start as observers)
            {
                mob_dead_observer ghost_player = player_mob as mob_dead_observer;
                if (Dm.truthy(ghost_player.started_as_observer)) // Exclude people who started as observers
                    continue;
            }
            Dm.append(ref active_players, player_mob);
        }
        return active_players;
    }

    ///Counts active players who are playing in the round
    // tg: code/__HELPERS/game.dm:172
    public static object get_active_player_count(float? alive_check = null, float? afk_check = null, float? human_check = null)
    {
        alive_check ??= FALSE;
        afk_check ??= FALSE;
        human_check ??= FALSE;
        return Dm.length(get_active_player_list(alive_check, afk_check, human_check));
    }
}

public partial class area : atom
{
    // tg: code/__HELPERS/game.dm:147
    public override object flick_overlay_view(object display = null, object duration = null)
    {
        return null;
    }
}

// tg: code/__HELPERS/game.dm:249 (M8 slice C: the survival medipens), :328 (M8 slice B: mannitol's overdose sends a chemistry tip)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/game.dm:249
    public static object lavaland_equipment_pressure_check(object turf_to_check = null)
    {
        object dot = FALSE;
        if (turf_to_check is not turf T)
            return dot;
        if (Waiting.NoSleep(T.return_air()) is not datum_gas_mixture environment)
            return dot;
        float pressure = Dm.num(environment.return_pressure());
        if (pressure <= LAVALAND_EQUIPMENT_EFFECT_PRESSURE)
            dot = TRUE;
        return dot;
    }

    // tg: code/__HELPERS/game.dm:328
    public static void send_tip_of_the_round(object target = null, object selected_tip = null, object source = null)
    {
        source ??= "Tip of the round";
        object message;
        if (Dm.truthy(selected_tip))
        {
            message = selected_tip;
        }
        else
        {
            DmList randomtips = world.file2list("strings/tips.txt") as DmList;
            DmList memetips = world.file2list("strings/sillytips.txt") as DmList;
            message = null;
            if (Dm.truthy(length(randomtips)) && prob(95))
                message = pick(randomtips);
            else if (Dm.truthy(length(memetips)))
                message = pick(memetips);
        }

        if (!Dm.truthy(message))
            return;
        if (!Dm.eq(copytext(message, 1, 2), "@")) // message[1]: the text's first character
            message = html_encode(message);
        else
            message = copytext(message, 2);
        to_chat(target, custom_boxed_message("purple_box", span_purple(Dm.text("<b>[]: </b>[]", source, message))), type: MESSAGE_TYPE_INFO);
    }
}

// tg: code/__HELPERS/game.dm:239 (interleaved types, rules 1.9; M10 slice F: the late join's arrival)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/game.dm:239
    public static async DmTask announce_arrival(mob_living_carbon_human character = null, object rank = null, object announce_to_ghosts = null)
    {
        announce_to_ghosts ??= TRUE;
        if (!Dm.truthy(SSticker.IsRoundInProgress()) || QDELETED(character))
            return;
        if (Dm.truthy(announce_to_ghosts))
        {
            area player_area = get_area(character) as area;
            deadchat_broadcast(span_game(I18n.T(" has arrived at the station at [].", span_name(Dm.str(player_area.name)))), span_game(Dm.text("[] ([])", span_name(Dm.str(character.real_name)), rank)), follow_target: character, message_type: DEADCHAT_ARRIVALRATTLE);
        }
        if (character.mind != null && Dm.truthy(Dm.bitand(character.mind.assigned_role.job_flags, JOB_ANNOUNCE_ARRIVAL)))
            await aas_config_announce(typeof(datum_aas_config_entry_arrival), Dm.list(Dm.kv("PERSON", character.real_name), Dm.kv("RANK", rank)));
    }
}

// tg: code/__HELPERS/game.dm:39 (syndicate package A1, D316: the objectives' checks)
public static partial class GlobalProcs
{
    /**
     * Checks if the mind of a player is considered alive.
     *
     * Returns TRUE if they're alive, FALSE otherwise
     */
    // tg: code/__HELPERS/game.dm:39
    public static object considered_alive(datum_mind player_mind = null, float? enforce_human = null)
    {
        enforce_human ??= TRUE;
        if (player_mind?.current != null)
        {
            if (Dm.truthy(enforce_human))
            {
                mob player_mob = player_mind.current;

                if (player_mob.stat == DEAD)
                    return FALSE;
                if (HAS_TRAIT(player_mob, TRAIT_NEVER_CONSIDERED_ALIVE))
                    return FALSE;
                return TRUE;
            }

            else if (isliving(player_mind.current))
                return Dm.num(player_mind.current.stat != DEAD);
        }

        return FALSE;
    }

    /**
     * Exiled check
     *
     * Checks if the current body of the mind has an exile implant and is currently in
     * an away mission. Returns FALSE if any of those conditions aren't met.
     */
    // tg: code/__HELPERS/game.dm:61
    public static object considered_exiled(datum_mind player_mind = null)
    {
        if (!ishuman(player_mind?.current))
            return FALSE;
        foreach (obj_item_implant implant_check in Dm.each<obj_item_implant>(((mob_living)player_mind.current).implants))
        {
            // tg's quirk: istype(implant_check, /obj/item/implant/exile && player_mind.current.onAwayMission()) tests against the &&'s value,
            // a number, so it is never true
            object and__1 = player_mind.current.onAwayMission();
            if (istype(implant_check, and__1 as Type))
                return TRUE;
        }
        return null;
    }

    ///Return an object with a new maptext (not currently in use)
    // tg: code/__HELPERS/game.dm:73
    public static atom_movable screen_text(atom_movable object_to_change = null, object maptext = null, object screen_loc = null, float? maptext_height = null, float? maptext_width = null)
    {
        maptext ??= "";
        screen_loc ??= "CENTER-7,CENTER-7";
        maptext_height ??= 480;
        maptext_width ??= 480;
        if (!istype(object_to_change, typeof(atom_movable)))
            object_to_change = Dm.@new<atom_movable_screen_text>();
        object_to_change.maptext = MAPTEXT(maptext);
        object_to_change.maptext_height = maptext_height.Value;
        object_to_change.maptext_width = maptext_width.Value;
        object_to_change.screen_loc = Dm.str(screen_loc);
        return object_to_change;
    }

    ///Checks if a player is considered AFK
    // tg: code/__HELPERS/game.dm:69
    public static object considered_afk(datum_mind player_mind = null)
    {
        return Dm.num(player_mind == null || player_mind.current == null || player_mind.current.client == null || Dm.truthy(player_mind.current.client.is_afk()));
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/game.dm:191
    public static void send_to_playing_players(object thing = null)
    {
        foreach (object player_mob in Dm.each(GLOB.player_list))
        {
            if (player_mob != null && !isnewplayer(player_mob))
                to_chat(player_mob, thing);
        }
    }
}

// tg: code/__HELPERS/game.dm:261 (syndicate X1a: the contractor's extraction pod)
public static partial class GlobalProcs
{
    ///Returns a random turf in a range of a center that is not obstructed, optionally inside a specific area.
    // tg: code/__HELPERS/game.dm:261
    public static async DmTask<object> find_obstruction_free_location(object range = null, atom center = null, area specific_area = null)
    {
        DmList possible_loc = Dm.list();

        foreach (turf found_turf in Dm.each<turf>(RANGE_TURFS(range, center)))
        {
            // We check if both the turf is a floor, and that it's actually in the area.
            // We also want a location that's clear of any obstructions.
            if (specific_area != null && !istype(get_area(found_turf), specific_area.GetType()))
                continue;

            if (!isgroundlessturf(found_turf) && !Dm.truthy(await found_turf.is_blocked_turf()))
                Dm.append(ref possible_loc, found_turf);
        }

        // Need at least one free location.
        if (possible_loc.len < 1)
            return FALSE;

        return pick(possible_loc);
    }
}

// tg: code/__HELPERS/game.dm:197 (silicons SI2: the ghosts' alerts)
public static partial class GlobalProcs
{
    ///Flash the window of a player
    // tg: code/__HELPERS/game.dm:197
    public static void window_flash(object flashed_client = null, float? ignorepref = null)
    {
        ignorepref ??= FALSE;
        if (flashed_client is mob player_mob)
        {
            if (player_mob.client != null)
                flashed_client = player_mob.client;
        }
        client the_client = flashed_client as client;
        if (the_client == null || (!Dm.truthy(the_client.prefs.read_preference(typeof(datum_preference_toggle_window_flashing))) && !Dm.truthy(ignorepref)))
            return;
        // winset(flashed_client, SKIN_MAINWINDOW, "flash=5"): the BYOND skin's taskbar flash (the skin is not ported, as setup.cs's macros)
        Dm.Deferred("code/__HELPERS/game.dm:204", "window_flash: winset(flashed_client, SKIN_MAINWINDOW, \"flash=5\"), the game window's taskbar flash (the BYOND skin, not ported)");
    }
}

public static partial class GlobalProcs
{
    /**
     * Disables power in most station APCs temporarily
     *
     * * duration_min - the minimum duration of the power failure in seconds (not deciseconds)
     * * duration_max - the maximum duration of the power failure in seconds (not deciseconds)
     */
    // tg: code/__HELPERS/game.dm:313  (gaps E1b: the grid check)
    public static void power_fail(object duration_min = null, object duration_max = null)
    {
        foreach (obj_machinery_power_apc current_apc in Dm.each<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
        {
            if (current_apc.cell == null || !Dm.truthy(SSmapping.level_trait(current_apc.z, ZTRAIT_STATION)) || HAS_TRAIT(current_apc.area, TRAIT_AREA_BLOCK_POWER_FAIL))
                continue;

            float duration = rand(Dm.num(duration_min), Dm.num(duration_max));
            current_apc.energy_fail(duration);
            CHECK_TICK();
        }
    }
}
