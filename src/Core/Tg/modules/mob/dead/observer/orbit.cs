// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/observer/orbit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice C (D281): the whole file, the ghosts' orbit menu (tgui's Orbit window, godot/scripts/Shared/Tgui/Interfaces/Orbit.cs).

// tg: code/modules/mob/dead/observer/orbit.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/dead/observer/orbit.dm:1  GLOBAL_DATUM_INIT(orbit_menu, /datum/orbit_menu, new) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_orbit_menu orbit_menu;

    // tg: code/modules/mob/dead/observer/orbit.dm:1  (GLOBAL_MANAGED(orbit_menu, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalorbit_menu()
    {
        orbit_menu = Dm.@new<datum_orbit_menu>();
        Dm.append(ref gvars_datum_init_order, "orbit_menu");
    }
}

// tg: code/modules/mob/dead/observer/orbit.dm:3
[DmPath("/datum/orbit_menu")]
public partial class datum_orbit_menu : datum
{
    ///mobs worth orbiting. Because spaghetti, all mobs have the point of interest, but only some are allowed to actually show up.
    ///this obviously should be changed in the future, so we only add mobs as POI if they actually are interesting, and we don't use
    ///a typecache.
    // tg: code/modules/mob/dead/observer/orbit.dm:7  var/static/list/mob_allowed_typecache (rules 10.6: a TgState field)
    private static ref object mob_allowed_typecache => ref TgState.Current.datum_orbit_menu__mob_allowed_typecache;

    // tg: code/modules/mob/dead/observer/orbit.dm:9
    public override object ui_state(mob user = null)
    {
        return GLOB.observer_state;
    }

    // tg: code/modules/mob/dead/observer/orbit.dm:12
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Orbit");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/dead/observer/orbit.dm:18
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);

        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "orbit"))
        {
            object @ref = @params?["ref"];
            object auto_observe = @params?["auto_observe"];
            atom poi = SSpoints_of_interest.get_poi_atom_by_ref(@ref) as atom;

            if ((ismob(poi) && !Dm.truthy(await SSpoints_of_interest.is_valid_poi(poi, Dm.@new<datum_callback>(this, nameof(validate_mob_poi)))))
                || !Dm.truthy(await SSpoints_of_interest.is_valid_poi(poi)))
            {
                to_chat(usr, span_notice(I18n.T("That point of interest is no longer valid.")));
                return TRUE;
            }

            mob_dead_observer user = usr as mob_dead_observer;
            await user.ManualFollow(poi as atom_movable);
            await user.reset_perspective(null);
            user.orbiting_ref = @ref;
            if (Dm.truthy(auto_observe))
            {
                if (poi != user)
                    await user.do_observe(poi as mob);
            }
            return TRUE;
        }
        if (Dm.eq(action, "refresh"))
        {
            await ui.send_full_update();
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/mob/dead/observer/orbit.dm:51
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (isobserver(user))
            data["orbiting"] = await get_currently_orbiting((mob_dead_observer)user);

        return data;
    }

    // tg: code/modules/mob/dead/observer/orbit.dm:60
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList new_mob_pois = await SSpoints_of_interest.get_mob_pois(Dm.@new<datum_callback>(this, nameof(validate_mob_poi)), append_dead_role: FALSE) as DmList;
        DmList new_other_pois = await SSpoints_of_interest.get_other_pois() as DmList;
        object is_admin = user?.client?.holder;

        DmList alive = Dm.list();
        DmList antagonists = Dm.list();
        DmList critical = Dm.list();
        DmList deadchat_controlled = Dm.list();
        DmList dead = Dm.list();
        DmList ghosts = Dm.list();
        DmList misc = Dm.list();
        DmList npcs = Dm.list();

        foreach (object name in Dm.each(new_mob_pois))
        {
            DmList serialized = Dm.list();
            mob mob_poi = new_mob_pois[name] as mob;
            float number_of_orbiters = length(mob_poi.get_all_orbiters());

            serialized["ref"] = REF(mob_poi);
            serialized["full_name"] = name;
            if (Dm.truthy(number_of_orbiters))
                serialized["orbiters"] = number_of_orbiters;

            if (Dm.truthy(is_admin))
                serialized["ckey"] = mob_poi.ckey;

            if (Dm.truthy(mob_poi.GetComponent(typeof(datum_component_deadchat_control))))
                Dm.append(ref deadchat_controlled, Dm.list(serialized));

            if (isobserver(mob_poi))
            {
                Dm.append(ref ghosts, Dm.list(serialized));
                continue;
            }

            if (mob_poi.stat == DEAD)
            {
                Dm.append(ref dead, Dm.list(serialized));
                continue;
            }

            if (mob_poi.mind == null)
            {
                if (isliving(mob_poi))
                {
                    mob_living npc = (mob_living)mob_poi;
                    serialized["health"] = FLOOR(npc.health / npc.maxHealth * 100, 1);
                }

                Dm.append(ref npcs, Dm.list(serialized));
                continue;
            }

            serialized["client"] = Dm.num(Dm.truthy(mob_poi.client));
            serialized["name"] = mob_poi.real_name;

            if (isliving(mob_poi))
                Dm.append(ref serialized, get_living_data((mob_living)mob_poi));

            DmList antag_data = get_antag_data(mob_poi.mind, is_admin) as DmList;
            if (Dm.truthy(length(antag_data)))
            {
                Dm.append(ref serialized, antag_data);
                Dm.append(ref antagonists, Dm.list(serialized));
                continue;
            }

            Dm.append(ref alive, Dm.list(serialized));
        }

        foreach (object name in Dm.each(new_other_pois))
        {
            atom atom_poi = new_other_pois[name] as atom;

            // Deadchat Controlled objects are orbitable
            if (Dm.truthy(atom_poi.GetComponent(typeof(datum_component_deadchat_control))))
            {
                float number_of_orbiters = length(atom_poi.get_all_orbiters());
                Dm.append(ref deadchat_controlled, Dm.list(Dm.list(
                    Dm.kv("ref", REF(atom_poi)),
                    Dm.kv("full_name", name),
                    Dm.kv("orbiters", number_of_orbiters))));
                continue;
            }

            DmList other_data = get_misc_data(atom_poi as atom_movable) as DmList;
            DmList misc_data = Dm.list(other_data[1]);

            Dm.append(ref misc, misc_data);

            if (Dm.truthy(other_data[2])) // Critical = TRUE
                Dm.append(ref critical, misc_data);
        }

        return Dm.list(
            Dm.kv("alive", alive),
            Dm.kv("antagonists", antagonists),
            Dm.kv("critical", critical),
            Dm.kv("deadchat_controlled", deadchat_controlled),
            Dm.kv("dead", dead),
            Dm.kv("ghosts", ghosts),
            Dm.kv("misc", misc),
            Dm.kv("npcs", npcs),
            Dm.kv("can_observe", Dm.num(!HAS_TRAIT(user, TRAIT_NO_OBSERVE))));
    }

    /// Shows the UI to the specified user.
    // tg: code/modules/mob/dead/observer/orbit.dm:162
    public virtual async DmTask show(mob user = null)
    {
        await ui_interact(user);
    }

    /// Helper function to get threat type, group, overrides for job and icon
    // tg: code/modules/mob/dead/observer/orbit.dm:167
    public virtual object get_antag_data(datum_mind poi_mind = null, object is_admin = null)
    {
        DmList serialized = Dm.list();

        foreach (datum_antagonist antag in Dm.each_cast<datum_antagonist>(poi_mind.antag_datums))
        {
            if (!Dm.truthy(antag.show_to_ghosts) && !Dm.truthy(is_admin))
                continue;

            serialized["antag"] = antag.name;
            serialized["antag_group"] = antag.antagpanel_category;
            serialized["icon"] = antag.hud_icon;
            serialized["icon_state"] = antag.antag_hud_name;

            return serialized;
        }
        return null;
    }

    /// Helper to get the current thing we're orbiting (if any)
    // tg: code/modules/mob/dead/observer/orbit.dm:182
    public virtual async DmTask<object> get_currently_orbiting(mob_dead_observer user = null)
    {
        if (user.orbiting_ref == null)
            return null;

        atom poi = SSpoints_of_interest.get_poi_atom_by_ref(user.orbiting_ref) as atom;
        if (poi == null)
        {
            user.orbiting_ref = null;
            return null;
        }

        if ((ismob(poi) && !Dm.truthy(await SSpoints_of_interest.is_valid_poi(poi, Dm.@new<datum_callback>(this, nameof(validate_mob_poi)))))
            || !Dm.truthy(await SSpoints_of_interest.is_valid_poi(poi)))
        {
            user.orbiting_ref = null;
            return null;
        }

        DmList serialized = Dm.list();

        if (!ismob(poi))
        {
            DmList misc_info = get_misc_data(poi as atom_movable) as DmList;
            Dm.append(ref serialized, misc_info[1]);
            return serialized;
        }

        mob mob_poi = (mob)poi;
        serialized["full_name"] = mob_poi.name;
        serialized["ref"] = REF(poi);

        if (Dm.truthy(mob_poi.mind))
        {
            serialized["client"] = Dm.num(Dm.truthy(mob_poi.client));
            serialized["name"] = mob_poi.real_name;
        }

        if (isliving(mob_poi))
            Dm.append(ref serialized, get_living_data((mob_living)mob_poi));

        return serialized;
    }

    /// Helper function to get job / icon / health data for a living mob
    // tg: code/modules/mob/dead/observer/orbit.dm:218
    public virtual object get_living_data(mob_living player = null)
    {
        DmList serialized = Dm.list();

        serialized["health"] = FLOOR(player.health / player.maxHealth * 100, 1);

        obj_item_card_id id_card = player.get_idcard(hand_first: FALSE) as obj_item_card_id;
        serialized["job"] = id_card?.get_trim_assignment() is var or__1 && Dm.truthy(or__1) ? or__1 : player.job;
        serialized["icon"] = id_card?.get_trim_sechud_icon() is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.resource("icons/mob/huds/hud.dmi"); // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75), a resource expanded inline (rules 3.7)
        if (issilicon(player))
            serialized["icon_state"] = "borg";
        else
            serialized["icon_state"] = id_card?.get_trim_sechud_icon_state();

        datum_job job = player.mind?.assigned_role;
        if (job == null)
            return serialized;

        serialized["mind_job"] = job.title;
        serialized["mind_job_icon"] = Dm.truthy(job.tgui_icon) ? job.tgui_icon : FA_ICON_QUESTION;
        // var/datum/outfit/outfit = job.get_outfit(): the job's outfit is a typepath, read as its type's vars
        Type outfit = job.get_outfit() as Type;
        if (outfit == null)
            return serialized;

        Type trim = Dm.initial(outfit, (datum_outfit p) => p.id_trim) as Type;
        if (trim != null)
        {
            serialized["mind_icon"] = Dm.initial(trim, (datum_id_trim p) => p.sechud_icon);
            serialized["mind_icon_state"] = Dm.initial(trim, (datum_id_trim p) => p.sechud_icon_state);
        }
        return serialized;
    }

    /// Gets a list: Misc data and whether it's critical. Handles all snowflakey type cases
    // tg: code/modules/mob/dead/observer/orbit.dm:245
    public virtual object get_misc_data(atom_movable atom_poi = null)
    {
        DmList misc = Dm.list();
        float critical = FALSE;

        misc["ref"] = REF(atom_poi);
        misc["full_name"] = atom_poi.name;

        // Display the supermatter crystal integrity
        if (atom_poi is obj_machinery_power_supermatter_crystal crystal)
        {
            float integrity = round(Dm.num(crystal.get_integrity_percent()));
            misc["extra"] = I18n.T("Integrity: []%", integrity);

            if (integrity < 10)
                critical = TRUE;

            return Dm.list(misc, critical);
        }

        // Display the nuke timer
        if (atom_poi is obj_machinery_nuclearbomb bomb)
        {
            if (Dm.truthy(bomb.timing))
            {
                misc["extra"] = I18n.T("Timer: []s", bomb.countdown?.displayed_text);
                critical = TRUE;
            }

            return Dm.list(misc, critical);
        }

        // Display the holder if its a nuke disk
        if (atom_poi is obj_item_disk_nuclear disk)
        {
            mob holder = (disk.pulledby is var or__1 && Dm.truthy(or__1) ? or__1 : get(disk, typeof(mob))) as mob;
            misc["extra"] = I18n.T("Location: []", holder?.real_name is var or__2 && Dm.truthy(or__2) ? or__2 : I18n.T("Unsecured"));

            return Dm.list(misc, critical);
        }

        // Display singuloths if they exist
        if (atom_poi is obj_singularity singulo)
        {
            misc["extra"] = I18n.T("Energy: []", round(singulo.energy));

            if (singulo.current_size > 2)
                critical = TRUE;

            return Dm.list(misc, critical);
        }

        return Dm.list(misc, critical);
    }

    /**
     * Helper POI validation function passed as a callback to various SSpoints_of_interest procs.
     *
     * Provides extended validation above and beyond standard, limiting mob POIs without minds or ckeys
     * unless they're mobs, eye mobs or megafauna. Also allows exceptions for mobs that are deadchat controlled.
     *
     * If they satisfy that requirement, falls back to default validation for the POI.
     */
    // tg: code/modules/mob/dead/observer/orbit.dm:301
    public virtual object validate_mob_poi(datum_point_of_interest_mob_poi potential_poi = null)
    {
        mob potential_mob_poi = (mob)potential_poi.target;
        if (!Dm.truthy(potential_mob_poi.mind) && !Dm.truthy(potential_mob_poi.ckey))
        {
            if (!Dm.truthy(mob_allowed_typecache))
            {
                mob_allowed_typecache = typecacheof(Dm.list(
                    typeof(mob_eye),
                    typeof(mob_living_basic_boss),
                    typeof(mob_living_basic_regal_rat),
                    typeof(mob_living_basic_bot),
                    typeof(mob_living_simple_animal_hostile_megafauna)));
            }
            if (!Dm.truthy(is_type_in_typecache(potential_mob_poi, mob_allowed_typecache)) && !Dm.truthy(potential_mob_poi.GetComponent(typeof(datum_component_deadchat_control))))
                return FALSE;
        }

        return potential_poi.validate();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/dead/observer/orbit.dm:7  /datum/orbit_menu var/static/list/mob_allowed_typecache
    internal object datum_orbit_menu__mob_allowed_typecache;
}
