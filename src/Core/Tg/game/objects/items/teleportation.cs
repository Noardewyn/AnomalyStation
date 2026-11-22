// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/teleportation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the experimental syndicate teleporter (the syndicate campaign G2, D316): four charges that come back over time,
// a jump 4-8 tiles ahead, the emergency jump out of a wall, the telefrag and the blood, the EMP's stray jumps (the gib's
// investigate log is a row); the bluespace locator with its BluespaceLocator window and the hand teleporter (gaps TP1: a portal
// pair to an engaged teleporter's target or a dangerous random spot, the right-click reopening the last one, up to three pairs,
// its own portals dispelled by a click).

/*
 * Locator
 */
// tg: code/game/objects/items/teleportation.dm:13  (gaps TP1)
public partial class obj_item_locator : obj_item
{
    // tg: code/game/objects/items/teleportation.dm:29
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BluespaceLocator", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/teleportation.dm:35
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["trackingrange"] = tracking_range;

        var current_turf = get_turf(this) as turf; // Get our current turf location.
        if (current_turf == null)
            return data;

        DmList track_implants = Dm.list();
        foreach (obj_item_implant_tracking tracking_implant in Dm.each<obj_item_implant_tracking>(GLOB.tracked_implants))
        {
            if (tracking_implant.imp_in == null || !isliving(tracking_implant.imp_in))
                continue;

            mob_living tracked_mob = tracking_implant.imp_in;
            if (Dm.eq(tracked_mob.stat, DEAD))
            {
                if (tracked_mob.timeofdeath + tracking_implant.lifespan_postmortem < world.time)
                    continue;
            }

            var implant_turf = get_turf(tracking_implant) as turf;
            if (implant_turf == null || implant_turf.z != current_turf.z)
                continue;

            float distance = Dm.max(Dm.abs(implant_turf.x - current_turf.x), Dm.abs(implant_turf.y - current_turf.y)); // get Chebyshev distance
            if (distance > tracking_range)
                continue;

            object implant_direction = dir2text(get_dir(current_turf, implant_turf));
            Dm.append(ref track_implants, Dm.list(Dm.list(Dm.kv("name", tracking_implant.imp_in.name), Dm.kv("direction", implant_direction), Dm.kv("distance", distance))));
        }

        data["trackimplants"] = track_implants;

        return data;
    }
}

/*
 * Hand-tele
 */
// tg: code/game/objects/items/teleportation.dm:72  (gaps TP1)
public partial class obj_item_hand_tele : obj_item
{
    // tg: code/game/objects/items/teleportation.dm:1-2  #define SOURCE_PORTAL 1, DESTINATION_PORTAL 2 (the file's own)
    private const int SOURCE_PORTAL = 1;
    private const int DESTINATION_PORTAL = 2;
    // tg: code/game/objects/items/teleportation.dm:67-68  #define PORTAL_LOCATION_DANGEROUS, PORTAL_DANGEROUS_EDGE_LIMIT (the file's own)
    private const string PORTAL_LOCATION_DANGEROUS = "portal_location_dangerous";
    private const int PORTAL_DANGEROUS_EDGE_LIMIT = 8;

    ///Checks if the targeted portal was created by us, then causes it to expire, removing it
    // tg: code/game/objects/items/teleportation.dm:115
    public virtual object try_dispel_portal(atom target = null, mob user = null)
    {
        if (Dm.truthy(is_parent_of_portal(target as obj_effect_portal)))
        {
            to_chat(user, span_notice(I18n.T("You dispel [] with []!", target, this)));
            var portal = (obj_effect_portal)target;
            portal.expire();
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/teleportation.dm:123
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(try_dispel_portal(interacting_with, user)))
            return ITEM_INTERACT_SUCCESS;
        return NONE;
    }

    // tg: code/game/objects/items/teleportation.dm:128
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/teleportation.dm:131
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object portal_location = last_portal_location;

        if (isweakref(portal_location))
        {
            var last_portal_location_ref = (datum_weakref)last_portal_location;
            portal_location = last_portal_location_ref.resolve();
        }

        if (portal_location == null)
        {
            to_chat(user, span_warning(I18n.T("[] flashes briefly. No target is locked in.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        await try_create_portal_to(user, portal_location);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/teleportation.dm:145
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/teleportation.dm:148
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(can_teleport_notifies(user)))
            return null;

        DmList locations = Dm.list();
        foreach (obj_machinery_computer_teleporter computer in Dm.each<obj_machinery_computer_teleporter>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_teleporter))))
        {
            var target = computer.target_ref?.resolve() as atom;
            if (target == null)
            {
                computer.target_ref = null;
                continue;
            }
            if (!Dm.truthy(check_teleport_valid(user, get_turf(computer), TELEPORT_CHANNEL_BLUESPACE)))
                continue;

            if (computer.power_station == null || computer.power_station.teleporter_hub == null)
                continue;

            if (Dm.truthy(Dm.bitand(computer.power_station.machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), MAINT))) || Dm.truthy(computer.power_station.panel_open))
                continue;

            if (Dm.truthy(Dm.bitand(computer.power_station.teleporter_hub.machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), MAINT))) || Dm.truthy(computer.power_station.teleporter_hub.panel_open))
                continue;

            if (Dm.truthy(computer.power_station.engaged))
                locations[I18n.T("[] (Active)", get_area(target))] = computer;
            else
                locations[I18n.T("[] (Inactive)", get_area(target))] = computer;
        }

        locations[I18n.T("None (Dangerous)")] = PORTAL_LOCATION_DANGEROUS;

        object teleport_location_key = await tgui_input_list(user, I18n.T("Teleporter to lock on"), I18n.T("Hand Teleporter"), (DmList)sort_list(locations));
        if (teleport_location_key == null)
            return null;
        if (user.get_active_held_item() != this || Dm.truthy(user.incapacitated))
            return null;

        // Not always a datum, but needed for IS_WEAKREF_OF to cast properly.
        object teleport_location = locations[teleport_location_key];
        if (!Dm.truthy(await try_create_portal_to(user, teleport_location)))
            return null;

        if (Dm.eq(teleport_location, PORTAL_LOCATION_DANGEROUS))
            last_portal_location = PORTAL_LOCATION_DANGEROUS;
        else if (!IS_WEAKREF_OF(teleport_location, last_portal_location))
        {
            // tg tests isweakref(teleport_location), which never holds (a console, not its weakref), so the old console's signal
            // stays registered; kept as tg wrote it
            if (isweakref(teleport_location))
            {
                var about_to_replace_location_ref = last_portal_location as datum_weakref;
                var about_to_replace_location = about_to_replace_location_ref?.resolve() as obj_machinery_computer_teleporter;
                if (about_to_replace_location != null)
                    UnregisterSignal(about_to_replace_location, COMSIG_TELEPORTER_NEW_TARGET);
            }

            RegisterSignal((datum)teleport_location, COMSIG_TELEPORTER_NEW_TARGET, nameof(on_teleporter_new_target));

            last_portal_location = WEAKREF(teleport_location as datum);
        }
        return null;
    }

    /// Takes either PORTAL_LOCATION_DANGEROUS or an /obj/machinery/computer/teleport/computer.
    // tg: code/game/objects/items/teleportation.dm:197
    public virtual async DmTask<object> try_create_portal_to(mob user = null, object teleport_location = null)
    {
        if (Dm.length(active_portal_pairs) >= max_portal_pairs)
        {
            user.show_message(span_notice(I18n.T("[] is recharging!", this)));
            return null;
        }

        atom teleport_target;

        if (Dm.eq(teleport_location, PORTAL_LOCATION_DANGEROUS))
        {
            DmList dangerous_turfs = Dm.list();
            foreach (turf dangerous_turf in Dm.each<turf>(urange(10, get_turf(this), TRUE)))
            {
                if (dangerous_turf.x > world.maxx - PORTAL_DANGEROUS_EDGE_LIMIT || dangerous_turf.x < PORTAL_DANGEROUS_EDGE_LIMIT)
                    continue; //putting them at the edge is dumb
                if (dangerous_turf.y > world.maxy - PORTAL_DANGEROUS_EDGE_LIMIT || dangerous_turf.y < PORTAL_DANGEROUS_EDGE_LIMIT)
                    continue;
                if (!Dm.truthy(check_teleport_valid(this, dangerous_turf)))
                    continue;
                Dm.append(ref dangerous_turfs, dangerous_turf);
            }

            teleport_target = pick(dangerous_turfs) as atom;
        }
        else
        {
            var computer = (obj_machinery_computer_teleporter)teleport_location;
            var target = computer.target_ref?.resolve() as atom;
            if (target == null)
                computer.target_ref = null;
            teleport_target = target;
        }

        if (teleport_target == null)
        {
            to_chat(user, span_notice(I18n.T("[] vibrates, then stops. Maybe you should try something else.", this)));
            return null;
        }

        if (!Dm.truthy(check_teleport_valid(this, teleport_target)))
        {
            to_chat(user, span_notice(I18n.T("[] is malfunctioning.", this)));
            return null;
        }

        if (!Dm.truthy(can_teleport_notifies(user)))
            return null;

        var created = create_portal_pair(get_turf(user) as turf, get_turf(teleport_target) as turf, 300, 1, null) as DmList;
        if (Dm.num(LAZYLEN(created)) != 2)
            return null;

        var portal1 = (obj_effect_portal)created[1];
        var portal2 = (obj_effect_portal)created[2];

        RegisterSignal(portal1, COMSIG_QDELETING, nameof(on_portal_destroy));
        RegisterSignal(portal2, COMSIG_QDELETING, nameof(on_portal_destroy));

        await try_move_adjacent(portal1, user.dir);
        if (QDELETED(portal1) || QDELETED(portal2)) //in the event that something managed to delete the portal objects, i.e. something teleported them
        {
            to_chat(user, span_notice(I18n.T("[] vibrates, but no portal seems to appear. Maybe you should try something else.", this)));
            return null;
        }
        active_portal_pairs[portal1] = portal2;

        investigate_log(Dm.text("was used by [] at [] to create a portal pair with destinations [] and [].", key_name(user), AREACOORD(user), AREACOORD(portal1), AREACOORD(portal2)), INVESTIGATE_PORTAL);
        add_fingerprint(user);

        user.show_message(span_notice(I18n.T("Locked in.")), MSG_AUDIBLE);

        return TRUE;
    }

    ///Checks for whether creating a portal in our area is allowed or not,
    ///returning FALSE when in a NOTELEPORT area, an away mission or when the user is not on a turf.
    ///Is, for some reason, separate from the teleport target's check in try_create_portal_to()
    // tg: code/game/objects/items/teleportation.dm:257
    public virtual object can_teleport_notifies(mob user = null)
    {
        var current_location = get_turf(user) as turf;
        if (current_location == null || !Dm.truthy(check_teleport_valid(this, current_location)) || !isturf(user.loc) || (Dm.truthy(away_restricted) && Dm.truthy(is_away_level(current_location.z))))
        {
            to_chat(user, span_notice(I18n.T("[] is malfunctioning.", this)));
            return FALSE;
        }

        return TRUE;
    }

    ///Clears last teleport location when the teleporter providing our target location changes its target
    // tg: code/game/objects/items/teleportation.dm:267
    [SignalHandler]
    public virtual void on_teleporter_new_target(datum source = null)
    {
        if (IS_WEAKREF_OF(source, last_portal_location))
        {
            last_portal_location = null;
            UnregisterSignal(source, COMSIG_TELEPORTER_NEW_TARGET);
        }
    }

    ///Removes a destroyed portal from active_portal_pairs list
    // tg: code/game/objects/items/teleportation.dm:275
    [SignalHandler]
    public virtual void on_portal_destroy(datum P = null)
    {
        Dm.remove(ref active_portal_pairs, P); //If this portal pair is made by us it'll be erased along with the other portal by the portal.
    }

    // tg: code/game/objects/items/teleportation.dm:281
    public virtual object is_parent_of_portal(obj_effect_portal P = null)
    {
        if (P == null)
            return FALSE;
        if (Dm.truthy(active_portal_pairs[P]))
            return SOURCE_PORTAL;
        foreach (object i in Dm.each(active_portal_pairs))
        {
            if (active_portal_pairs[i] == P)
                return DESTINATION_PORTAL;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/teleportation.dm:291
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (iscarbon(user))
        {
            user.visible_message(span_suicide(I18n.T("[] is creating a weak portal and sticking [] head through! It looks like [] trying to commit suicide!", user, user.p_their(), user.p_theyre())));
            var itemUser = (mob_living_carbon)user;
            var head = itemUser.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            if (head != null)
            {
                await head.drop_limb();
                var safeLevels = SSmapping.levels_by_any_trait(Dm.list(ZTRAIT_SPACE_RUINS, ZTRAIT_LAVA_RUINS, ZTRAIT_STATION, ZTRAIT_MINING)) as DmList;
                await head.forceMove(locate(rand(1, world.maxx), rand(1, world.maxy), Dm.num(pick(safeLevels))));
                itemUser.visible_message(span_suicide(I18n.T("The portal snaps closed taking []'s head with it!", user)));
            }
            else
                itemUser.visible_message(span_suicide(I18n.T("[] looks even further depressed as they realize they do not have a head...and suddenly dies of shame!", user)));
            return BRUTELOSS;
        }
        return null;
    }
}

// tg: code/game/objects/items/teleportation.dm:312
public partial class obj_item_syndicate_teleporter : obj_item
{
    // tg: code/game/objects/items/teleportation.dm:337
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/teleportation.dm:341
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/teleportation.dm:345
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("[] has <b>[]</b> out of [] charges left.", this, charges, max_charges)));
        return dot;
    }

    // tg: code/game/objects/items/teleportation.dm:349
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await attempt_teleport(user: user, triggered_by_emp: FALSE);
        return TRUE;
    }

    // tg: code/game/objects/items/teleportation.dm:356
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (SPT_PROB(10, seconds_per_tick) && charges < max_charges)
        {
            charges++;
            if (ishuman(loc))
            {
                mob_living_carbon_human holder = (mob_living_carbon_human)loc;
                balloon_alert(holder, I18n.T("teleporter beeps"));
            }
            playsound(this, Dm.resource("sound/machines/beep/twobeep.ogg"), 10, TRUE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
        }
        return null;
    }

    // tg: code/game/objects/items/teleportation.dm:364
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!prob(50 / Dm.num(severity)))
            return dot;
        bool teleported_something = false;
        if (ishuman(loc))
        {
            mob_living_carbon_human holder = (mob_living_carbon_human)loc;
            balloon_alert(holder, I18n.T("teleporter buzzes!"));
            await attempt_teleport(user: holder, triggered_by_emp: TRUE);
        }
        else
        {
            turf teleport_turf = get_turf(this);
            foreach (mob_living mob_on_same_tile in Dm.each<mob_living>(teleport_turf))
            {
                if (!teleported_something)
                    teleported_something = true;
                await attempt_teleport(user: mob_on_same_tile, triggered_by_emp: TRUE, not_holding_tele: TRUE);
            }
            if (!teleported_something)
            {
                visible_message(span_danger(I18n.T("[] blinks out of existence!", this)));
                do_sparks(2, 1, this);
                qdel(this);
            }
        }
        return dot;
    }

    /**
     * Tries to teleport the user forward based on random number between min/max teleport distance vars.
     * If destination is closed turf, try to save user from gibbing via a panic teleport.
     * Wearing bag of holding or triggering teleport via EMP removes panic teleport, higher chance of being gibbed.
     * Mobs on same tile as destination get telefragged.
     **/
    // tg: code/game/objects/items/teleportation.dm:390
    public virtual async DmTask attempt_teleport(mob user = null, float? triggered_by_emp = null, float? not_holding_tele = null)
    {
        triggered_by_emp ??= FALSE;
        not_holding_tele ??= FALSE;
        if (!Dm.truthy(charges) && !Dm.truthy(triggered_by_emp))
        {
            balloon_alert(user, I18n.T("recharging!"));
            return;
        }

        turf current_location = get_turf(user);

        if (Dm.truthy(malfunctioning(user, current_location)))
        {
            if (Dm.truthy(not_holding_tele))
                return;
            balloon_alert(user, I18n.T("malfunctioning!"));
            return;
        }

        float teleport_distance = rand(minimum_teleport_distance, maximum_teleport_distance);
        turf destination = get_teleport_loc(current_location, user, teleport_distance) as turf;
        bool bagholdingcheck = false;
        foreach (obj_item check in Dm.each_cast<obj_item>(user.get_all_contents_type(typeof(obj_item))))
        {
            if (HAS_TRAIT(check, TRAIT_BLUESPACE_INTERFERENCE))
                bagholdingcheck = true;
        }
        if (isclosedturf(destination))
        {
            if (!Dm.truthy(triggered_by_emp) && !bagholdingcheck)
            {
                await panic_teleport(user, destination); //We're in a wall, engage emergency parallel teleport.
            }
            else
            {
                if (bagholdingcheck && !Dm.truthy(not_holding_tele))
                    to_chat(user, span_warning(I18n.T("The bluespace interface on your bag of holding interferes with the teleport!")));
                await get_fragged(user as mob_living, destination, not_holding_tele); //EMP teleported you into a wall? Wearing a BoH? You're dead.
            }
        }
        else
        {
            await telefrag(destination, user);
            await do_teleport(user, destination, channel: TELEPORT_CHANNEL_FREE);
            charges = max(charges - 1, 0);
            Dm.@new<obj_effect_temp_visual_teleport_abductor_syndi_teleporter>(current_location);
            Dm.@new<obj_effect_temp_visual_teleport_abductor_syndi_teleporter>(destination);
            if (Dm.truthy(await make_bloods(current_location, destination, user as mob_living)))
                Dm.@new<obj_effect_temp_visual_circle_wave_syndi_teleporter_bloody>(destination);
            else
                Dm.@new<obj_effect_temp_visual_circle_wave_syndi_teleporter>(destination);
            playsound(current_location, SFX_PORTAL_ENTER, 50, 1, SHORT_RANGE_SOUND_EXTRARANGE);
            playsound(destination, Dm.resource("sound/effects/phasein.ogg"), 25, 1, SHORT_RANGE_SOUND_EXTRARANGE);
            playsound(destination, SFX_PORTAL_ENTER, 50, 1, SHORT_RANGE_SOUND_EXTRARANGE);
        }
    }

    // tg: code/game/objects/items/teleportation.dm:430
    public virtual object malfunctioning(mob guy_teleporting = null, turf current_location = null)
    {
        if (current_location == null)
            return TRUE;
        if (!Dm.truthy(check_teleport_valid(this, current_location)))
            return TRUE;
        if (Dm.truthy(is_away_level(current_location.z)))
            return TRUE;
        if (Dm.truthy(is_centcom_level(current_location.z)))
            return TRUE;
        if (Dm.truthy(is_reserved_level(current_location.z)))
            return TRUE;
        if (!isturf(guy_teleporting.loc))
            return TRUE;
        return FALSE;
    }

    /**
     * Checks parallel_teleport_distance amount of tiles parallel to user's teleport destination.
     * If no valid closed turfs found, gib user.
     **/
    // tg: code/game/objects/items/teleportation.dm:449
    public virtual async DmTask panic_teleport(mob user = null, turf destination = null)
    {
        turf mobloc = get_turf(user);
        turf emergency_destination = get_teleport_loc(destination, user, distance: 0, closed_turf_check: TRUE, errorx: parallel_teleport_distance) as turf;

        if (emergency_destination != null)
        {
            await telefrag(emergency_destination, user);
            await do_teleport(user, emergency_destination, channel: TELEPORT_CHANNEL_FREE);
            charges = max(charges - 1, 0);
            Dm.@new<obj_effect_temp_visual_teleport_abductor_syndi_teleporter>(mobloc);
            Dm.@new<obj_effect_temp_visual_teleport_abductor_syndi_teleporter>(emergency_destination);
            balloon_alert(user, I18n.T("emergency teleport triggered!"));
            if (Dm.truthy(await make_bloods(destination, emergency_destination, user as mob_living)))
                Dm.@new<obj_effect_temp_visual_circle_wave_syndi_teleporter_bloody>(destination);
            else
                Dm.@new<obj_effect_temp_visual_circle_wave_syndi_teleporter>(destination);
            playsound(mobloc, SFX_PORTAL_ENTER, 50, 1, SHORT_RANGE_SOUND_EXTRARANGE);
            playsound(emergency_destination, Dm.resource("sound/effects/phasein.ogg"), 25, 1, SHORT_RANGE_SOUND_EXTRARANGE);
            playsound(emergency_destination, SFX_PORTAL_ENTER, 50, 1, SHORT_RANGE_SOUND_EXTRARANGE);
            playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 25, TRUE);
        }
        else //We tried to save. We failed. Death time.
        {
            await get_fragged(user as mob_living, destination);
        }
    }

    ///Force move victim to destination, explode destination, drop all victim's items, gib them
    // tg: code/game/objects/items/teleportation.dm:472
    public virtual async DmTask get_fragged(mob_living victim = null, turf destination = null, float? not_holding_tele = null)
    {
        not_holding_tele ??= FALSE;
        turf mobloc = get_turf(victim);
        await victim.forceMove(destination);
        Dm.@new<obj_effect_temp_visual_teleport_abductor_syndi_teleporter>(mobloc);
        Dm.@new<obj_effect_temp_visual_teleport_abductor_syndi_teleporter>(destination);
        playsound(mobloc, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        playsound(destination, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        playsound(destination, Dm.resource("sound/effects/magic/disintegrate.ogg"), 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        if (!Dm.truthy(not_holding_tele))
            to_chat(victim, span_userdanger(I18n.T("You teleport into [], [] tries to save you, but...", destination, this)));
        else
            to_chat(victim, span_userdanger(I18n.T("You teleport into [].", destination)));
        destination.ex_act(EXPLODE_HEAVY);
        await victim.unequip_everything();
        victim.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
        await victim.gib(DROP_ALL_REMAINS);
    }

    ///Damage and stun all mobs in fragging_location turf, called after a teleport
    // tg: code/game/objects/items/teleportation.dm:490
    public virtual async DmTask telefrag(turf fragging_location = null, mob user = null) // Don't let this gib. Never let this gib.
    {
        foreach (mob_living victim in Dm.each<mob_living>(fragging_location))//Hit everything in the turf
        {
            await victim.apply_damage(20, BRUTE);
            await victim.Paralyze(6 * SECONDS);
            to_chat(victim, span_warning(I18n.T("[] teleports into you, knocking you to the floor with the bluespace wave!", user)));
            await victim.throw_at(get_step_rand(victim), 1, 1, user, spin: TRUE);
        }
    }

    ///Bleed and make blood splatters at tele start and end points
    // tg: code/game/objects/items/teleportation.dm:498
    public virtual async DmTask<object> make_bloods(turf old_location = null, turf new_location = null, mob_living user = null)
    {
        if (!Dm.eq(user.can_bleed(BLOOD_COVER_TURFS), BLEED_SPLATTER))
            return FALSE;
        await user.add_splatter_floor(old_location);
        await user.add_splatter_floor(new_location);
        if (!iscarbon(user))
            return FALSE;
        mob_living_carbon carbon_user = (mob_living_carbon)user;

        // always lose a bit
        await carbon_user.bleed(bleed_amount * 0.25f);
        // sometimes lose a lot
        // average evens out to 10 per teleport, but the randomness spices things up
        if (prob(25) && Dm.truthy(bleed_amount))
        {
            playsound(this, Dm.resource("sound/effects/wounds/pierce1.ogg"), 40, vary: TRUE);
            visible_message(span_warning(I18n.T("Blood visibly spurts out of [] as [] fails to teleport [] body properly!", user, this, user.p_their())),
                span_boldwarning(I18n.T("Blood visibly spurts out of you as [] fails to teleport your body properly!", this)));
            await carbon_user.bleed(bleed_amount * 0.75f);
            await carbon_user.spray_blood(pick(GLOB.alldirs), rand(1, 3));
            return TRUE;
        }

        return FALSE;
        // retval used for picking wave type
    }
}
