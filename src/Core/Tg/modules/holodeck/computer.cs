// Translated from tgstation (snapshot 1d99bd7f): code/modules/holodeck/computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the holodeck control console (its programs loaded as map templates into its area, the holographic atoms it tracks
// and derezzes when they leave, the safeties and the emag's restricted programs, the floor check and its overload, the power) with
// its Holodeck window, and the holodeck templates' blacklist of floors that are not holographic. The var blocks are generated (D111).

/*
Map Template Holodeck

Holodeck finds the location of mapped_start_area and loads offline_program in it on LateInitialize. It then passes its program templates to Holodeck.js in the form of program_cache and emag_programs. when a user selects a program the
ui calls load_program() with the id of the selected program.
load_program() -> map_template/load() on map_template/holodeck.

holodeck map templates:
1. have an update_blacklist that doesnt allow placing on non holofloors (except for engine floors so you can repair it)
2. has should_place_on_top = FALSE, so that the baseturfs list doesnt pull a kilostation oom crash
3. has returns_created = TRUE, so that SSatoms gives the map template a list of spawned atoms
all the fancy flags and shit are added to holodeck objects in finish_spawn()
*/

/// typecache for turfs that should be considered ok during floorchecks.
/// A linked turf being anything not in this typecache will cause the holodeck to perform an emergency shutdown.
// tg: code/modules/holodeck/computer.dm:32
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/holodeck/computer.dm:32  GLOBAL_LIST_INIT(typecache_holodeck_linked_floorcheck_ok, ...)
    public DmList typecache_holodeck_linked_floorcheck_ok;

    // tg: code/modules/holodeck/computer.dm:32
    public virtual void InitGlobaltypecache_holodeck_linked_floorcheck_ok()
    {
        typecache_holodeck_linked_floorcheck_ok = typecacheof(Dm.list(typeof(turf_open_floor_holofloor), typeof(turf_closed))) as DmList;
    }
}

// tg: code/modules/holodeck/computer.dm:34
public partial class obj_machinery_computer_holodeck : obj_machinery_computer
{
    // tg: code/modules/holodeck/computer.dm:29-30
    private const float HOLODECK_CD = 2 * SECONDS;
    private const float HOLODECK_DMG_CD = 5 * SECONDS;

    //from here linked is populated and the program list is generated. its also set to load the offline program
    // tg: code/modules/holodeck/computer.dm:88
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        linked = GLOB.areas_by_type[mapped_start_area] as area_station_holodeck;
        if (linked == null)
        {
            log_mapping(Dm.text("[] at [] has no matching holodeck area.", this, AREACOORD(this)));
            qdel(this);
            return;
        }

        bottom_left = locate(linked.x, linked.y, z);
        if (bottom_left == null)
        {
            log_mapping(Dm.text("[] at [] has an invalid holodeck area.", this, AREACOORD(this)));
            qdel(this);
            return;
        }

        var computer_area = get_area(this) as area;
        if (istype(computer_area, typeof(area_station_holodeck)))
        {
            log_mapping("Holodeck computer cannot be in a holodeck, This would cause circular power dependency.");
            qdel(this);
            return;
        }

        // the following is necessary for power reasons
        if (!Dm.truthy(offline_program))
        {
            stack_trace("Holodeck console created without an offline program", "code/modules/holodeck/computer.dm", 109);
            qdel(this);
            return;
        }

        linked.linked = this;
        var my_area = get_area(this) as area;
        if (my_area != null)
            linked.energy_usage = my_area.energy_usage;
        else
            linked.energy_usage = Dm.list_n(AREA_USAGE_LEN);

        holodeck_cooldown = world.time + HOLODECK_CD; // COOLDOWN_START(src, holodeck_cooldown, HOLODECK_CD)
        generate_program_list();
        await load_program(offline_program, TRUE);
    }

    ///adds all programs that this holodeck has access to, and separates the restricted and unrestricted ones
    // tg: code/modules/holodeck/computer.dm:125
    public virtual void generate_program_list()
    {
        foreach (Type typekey in Dm.each<Type>(subtypesof(program_type)))
        {
            Type program = typekey;
            DmList info_this = Dm.list(Dm.kv("id", Dm.initial(program, (datum_map_template_holodeck p) => p.template_id)), Dm.kv("name", Dm.initial(program, (datum_map_template p) => p.name)));
            if (Dm.truthy(Dm.initial(program, (datum_map_template_holodeck p) => p.restricted)))
                LAZYADD(ref emag_programs, Dm.list(info_this));
            else
                LAZYADD(ref program_cache, Dm.list(info_this));
        }
    }

    // tg: code/modules/holodeck/computer.dm:134
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Holodeck", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/holodeck/computer.dm:141
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["default_programs"] = program_cache;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            data["emagged"] = TRUE;
            data["emag_programs"] = emag_programs;
        }
        data["program"] = program;
        data["can_toggle_safety"] = Dm.num(HAS_SILICON_ACCESS(user));
        return data;
    }

    // tg: code/modules/holodeck/computer.dm:152
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        dot = TRUE;

        switch (action)
        {
            case "load_program":
            {
                object program_to_load = @params["id"];

                DmList @checked = program_cache.Copy();
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                    Dm.combine(ref @checked, emag_programs);
                bool valid = false; //dont tell security about this

                //checks if program_to_load is any one of the loadable programs, if it isnt then it rejects it
                foreach (DmList check_list in Dm.each<DmList>(@checked))
                {
                    if (Dm.eq(check_list["id"], program_to_load))
                    {
                        valid = true;
                        break;
                    }
                }
                if (!valid)
                    return FALSE;
                //load the map_template that program_to_load represents
                if (Dm.truthy(program_to_load))
                    await load_program(program_to_load);
                break;
            }
            case "safety":
                if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && !issilicon(usr))
                    return dot;
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.truthy(program))
                    await emergency_shutdown();
                nerf(Dm.bitand(obj_flags, EMAGGED), FALSE);
                obj_flags = Dm.bitxor(obj_flags, EMAGGED);
                await say(I18n.T("Safeties reset. Restarting..."));
                usr.log_message("disabled Holodeck safeties.", LOG_GAME);
                break;
        }
        return dot;
    }

    ///loads the template whose id string it was given ("offline_program" loads datum/map_template/holodeck/offline)
    // tg: code/modules/holodeck/computer.dm:196
    public virtual async DmTask load_program(object map_id = null, float? force = null, float? add_delay = null)
    {
        force ??= FALSE;
        add_delay ??= TRUE;
        if (Dm.eq(program, map_id))
            return;

        if (!Dm.truthy(is_operational)) //load_program is called once with a timer (in toggle_power) we dont want this to load anything if its off
        {
            map_id = offline_program;
            force = TRUE;
        }

        if (!Dm.truthy(force) && (!(holodeck_cooldown <= world.time) || Dm.truthy(spawning_simulation))) // COOLDOWN_FINISHED(src, holodeck_cooldown)
        {
            await say(I18n.T("ERROR. Recalibrating projection apparatus."));
            return;
        }

        if (Dm.truthy(spawning_simulation))
            return;

        if (Dm.truthy(add_delay))
        {
            holodeck_cooldown = world.time + (Dm.truthy(damaged) ? HOLODECK_CD + HOLODECK_DMG_CD : HOLODECK_CD); // COOLDOWN_START(src, holodeck_cooldown, ...)
            if (Dm.truthy(damaged) && Dm.truthy(floorcheck()))
                damaged = FALSE;
        }

        spawning_simulation = TRUE;
        active = Dm.num(!Dm.eq(map_id, offline_program));
        update_mode_power_usage(ACTIVE_POWER_USE, Dm.initial(this, (obj_machinery p) => p.active_power_usage));
        program = Dm.str(map_id);

        await clear_projection();

        template = SSmapping.holodeck_templates[map_id] as datum_map_template_holodeck;
        await template.load(turf.dm_identity(bottom_left) as turf); //this is what actually loads the holodeck simulation into the map

        if (Dm.truthy(template.restricted))
        {
            if (usr != null)
            {
                usr.log_message(Dm.text("loaded a restricted Holodeck program: [].", program), LOG_GAME);
                message_admins(Dm.text("[] loaded a restricted Holodeck program: [].", ADMIN_LOOKUPFLW(usr), program));
            }
            else
            {
                log_game(Dm.text("loaded a restricted Holodeck program: [].", program));
                message_admins(Dm.text("loaded a restricted Holodeck program: [].", program));
            }
        }

        spawned = template.created_atoms; //populate the spawned list with the atoms belonging to the holodeck

        if (istype(template, typeof(datum_map_template_holodeck_thunderdome1218)) && !Dm.truthy(SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_MEDISIM]))
        {
            await say(I18n.T("Special note from \"1218 AD\" developer: I see you too are interested in the REAL dark ages of humanity! I've made this program also unlock some interesting shuttle designs on any communication console around. Have fun!"));
            SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_MEDISIM] = TRUE;
        }

        nerf(Dm.num(!Dm.truthy(Dm.bitand(obj_flags, EMAGGED))));
        finish_spawn();
    }

    ///To be used on destroy, mainly to prevent sleeping inside well, destroy. Missing a lot of the things contained in load_program
    // tg: code/modules/holodeck/computer.dm:245
    public virtual async DmTask reset_to_default()
    {
        if (program == offline_program)
            return;

        program = offline_program;
        await clear_projection();
        //update_mode_power_usage(ACTIVE_POWER_USE, initial(active_power_usage))
        update_use_power(IDLE_POWER_USE);

        template = SSmapping.holodeck_templates[offline_program] as datum_map_template_holodeck;
        Waiting.Detach(template.load(turf.dm_identity(bottom_left) as turf)); //this is what actually loads the holodeck simulation into the map
    }

    // tg: code/modules/holodeck/computer.dm:257
    public virtual async DmTask clear_projection()
    {
        //clear the items from the previous program
        foreach (object holo_atom in Dm.each(spawned))
            await derez(holo_atom as atom_movable);

        foreach (obj_effect_holodeck_effect holo_effect in Dm.each<obj_effect_holodeck_effect>(effects))
        {
            Dm.remove(ref effects, holo_effect);
            await holo_effect.deactivate(this);
        }

        //makes sure that any time a holoturf is inside a baseturf list (e.g. if someone put a wall over it) its set to the OFFLINE turf
        //so that you cant bring turfs from previous programs into other ones (like putting the plasma burn turf into lounge for example)
        foreach (turf_closed holo_turf in Dm.each<turf_closed>(linked))
            await holo_turf.replace_baseturf(typeof(turf_open_floor_holofloor), typeof(turf_open_floor_holofloor_plating));
    }

    ///finalizes objects in the spawned list
    // tg: code/modules/holodeck/computer.dm:272
    public virtual void finish_spawn()
    {
        foreach (atom holo_atom in Dm.each<atom>(spawned))
        {
            if (QDELETED(holo_atom))
            {
                Dm.remove(ref spawned, holo_atom);
                continue;
            }
            finalize_spawned(holo_atom);
        }
        spawning_simulation = FALSE;
    }

    // tg: code/modules/holodeck/computer.dm:280
    public virtual void finalize_spawned(atom holo_atom = null)
    {
        RegisterSignal(holo_atom, COMSIG_QDELETING, nameof(remove_from_holo_lists));
        holo_atom.flags_1 = Dm.num(Dm.bitor(holo_atom.flags_1, HOLOGRAM_1));

        if (istype(holo_atom, typeof(obj_effect_holodeck_effect))) //activates holo effects and transfers them from the spawned list into the effects list (isholoeffect, is_helpers.dm, inline)
        {
            var holo_effect = (obj_effect_holodeck_effect)holo_atom;
            Dm.append(ref effects, holo_effect);
            Dm.remove(ref spawned, holo_effect);
            object holo_effect_product = holo_effect.activate(this); //change name
            if (istype(holo_effect_product, typeof(atom)))
            {
                Dm.append(ref spawned, holo_effect_product); // we want mobs or objects spawned via holoeffects to be tracked as objects
                RegisterSignal((datum)holo_effect_product, COMSIG_QDELETING, nameof(remove_from_holo_lists));
            }
            if (islist(holo_effect_product))
            {
                foreach (atom atom_product in Dm.each<atom>(holo_effect_product))
                {
                    Dm.append(ref spawned, atom_product);
                    RegisterSignal(atom_product, COMSIG_QDELETING, nameof(remove_from_holo_lists));
                }
            }
            return;
        }

        if (isobj(holo_atom))
        {
            var holo_object = (obj)holo_atom;
            holo_object.resistance_flags = Dm.bitor(Dm.bitor(Dm.bitor(LAVA_PROOF, FIRE_PROOF), UNACIDABLE), ACID_PROOF);

            if (isstructure(holo_object))
            {
                holo_object.obj_flags = Dm.bitor(holo_object.obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION);
                if (istype(holo_object, typeof(obj_structure_closet)))
                    RegisterSignal(holo_object, COMSIG_CLOSET_CONTENTS_INITIALIZED, nameof(register_contents));
                return;
            }

            if (ismachinery(holo_object))
            {
                var holo_machine = (obj_machinery)holo_object;
                holo_machine.obj_flags = Dm.bitor(holo_machine.obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION);
                holo_machine.power_change();

                if (istype(holo_machine, typeof(obj_machinery_button)))
                {
                    var holo_button = (obj_machinery_button)holo_machine;
                    holo_button.setup_device();
                }
            }
        }
    }

    // tg: code/modules/holodeck/computer.dm:318
    public virtual void register_contents(obj_structure_closet storage = null)
    {
        foreach (atom_movable item in Dm.each<atom_movable>(storage.get_all_contents_type(typeof(atom_movable))))
        {
            if (item == storage)
                continue;
            add_to_spawned(item);
        }
    }

    /**
     * A separate proc for objects that weren't loaded by the template nor spawned by holo effects
     * yet need to be added to the list of spawned objects. (e.g. holographic fishes)
     */
    // tg: code/modules/holodeck/computer.dm:331
    public virtual void add_to_spawned(atom holo_atom = null)
    {
        Dm.combine(ref spawned, holo_atom);
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && isitem(holo_atom))
        {
            var to_be_nerfed = (obj_item)holo_atom;
            to_be_nerfed.damtype = STAMINA;
        }
        finalize_spawned(holo_atom);
    }

    ///this qdels holoitems that should no longer exist for whatever reason
    // tg: code/modules/holodeck/computer.dm:339
    public virtual async DmTask derez(atom_movable holo_atom = null, float? silent = null, float? forced = null)
    {
        silent ??= TRUE;
        forced ??= FALSE;
        Dm.remove(ref spawned, holo_atom);
        if (holo_atom == null)
            return;
        UnregisterSignal(holo_atom, COMSIG_QDELETING);
        turf target_turf = get_turf(holo_atom);
        foreach (atom_movable atom_contents in Dm.each<atom_movable>(holo_atom)) //make sure that things inside of a holoitem are moved outside before destroying it
        {
            if (Dm.truthy(Dm.bitand(atom_contents.flags_1, HOLOGRAM_1))) //hologram in hologram is fine
                continue;
            await atom_contents.forceMove(target_turf);
        }

        if (istype(holo_atom, typeof(obj_item_clothing_under)))
        {
            var holo_clothing = (obj_item_clothing_under)holo_atom;
            await holo_clothing.dump_attachments();
        }

        if (istype(holo_atom, typeof(obj_item_organ)))
        {
            var holo_organ = (obj_item_organ)holo_atom;
            if (holo_organ.owner != null) // a mob has the holo organ inside them... oh dear
                to_chat(holo_organ.owner, span_warning(I18n.T("\\The [] inside of you fades away!", holo_organ)));
        }
        if (!Dm.truthy(silent))
            visible_message(span_notice(I18n.T("[] fades away!", holo_atom)));

        if (Dm.truthy(is_type_in_list(holo_atom.loc, special_locs)))
            qdel(holo_atom.loc);
        qdel(holo_atom);
    }

    // tg: code/modules/holodeck/computer.dm:367
    public virtual void remove_from_holo_lists(datum to_remove = null, object _forced = null)
    {
        Dm.remove(ref spawned, to_remove);
        UnregisterSignal(to_remove, COMSIG_QDELETING);
    }

    // tg: code/modules/holodeck/computer.dm:372
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(damaged) && SPT_PROB(5, seconds_per_tick))
        {
            foreach (turf holo_turf in Dm.each<turf>(linked))
            {
                if (SPT_PROB(2.5f, seconds_per_tick))
                {
                    do_sparks(2, 1, holo_turf);
                    return null;
                }
            }
        }
        if (Dm.truthy(spawning_simulation))
            return null; // putting it here because updating power would be pointless we are only loading it
        object dot = base.process(seconds_per_tick, __extra);
        if (!Dm.truthy(dot) || program == offline_program) //we dont need to scan the holodeck if the holodeck is offline
        {
            update_use_power(IDLE_POWER_USE);
            return dot;
        }

        if (!Dm.truthy(floorcheck())) //if any turfs in the floor of the holodeck are broken
        {
            Waiting.NoSleep(emergency_shutdown());
            damaged = TRUE;
            visible_message("The holodeck overloads!");
            foreach (turf holo_turf in Dm.each<turf>(linked))
            {
                if (prob(30))
                    do_sparks(2, 1, holo_turf);
                Dm.append(ref SSexplosions.lowturf, holo_turf);
                holo_turf.hotspot_expose(1000, 500, 1);
            }
        }

        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            foreach (object item in Dm.each(spawned))
            {
                if (!Dm.@in(get_turf(item as atom), linked))
                    Waiting.NoSleep(derez(item as atom_movable));
            }
        }
        foreach (obj_effect_holodeck_effect holo_effect in Dm.each<obj_effect_holodeck_effect>(effects))
            holo_effect.tick();
        update_use_power(ACTIVE_POWER_USE);
        update_mode_power_usage(ACTIVE_POWER_USE, Dm.num(Dm.initial(this, (obj_machinery p) => p.active_power_usage)) + (spawned.len * 15 + effects.len * 25));
        return dot;
    }

    // tg: code/modules/holodeck/computer.dm:404
    public virtual async DmTask toggle_power(float? toggleOn = null)
    {
        toggleOn ??= FALSE;
        if (Dm.eq(active, toggleOn))
            return;

        if (Dm.truthy(toggleOn))
        {
            if (Dm.truthy(last_program) && !Dm.eq(last_program, offline_program))
                _addtimer(Dm.@new<datum_callback>(this, nameof(load_program), last_program, TRUE), 2.5f * SECONDS, file: "code/modules/holodeck/computer.dm", line: 410);
            active = TRUE;
        }
        else
        {
            last_program = program;
            await load_program(offline_program, TRUE);
            active = FALSE;
        }
    }

    // tg: code/modules/holodeck/computer.dm:417
    public override object power_change()
    {
        object dot = base.power_change();
        Waiting.Detach(toggle_power(Dm.num(!Dm.truthy(machine_stat)))); // INVOKE_ASYNC(src, PROC_REF(toggle_power), !machine_stat)
        return dot;
    }

    ///shuts down the holodeck and force loads the offline_program
    // tg: code/modules/holodeck/computer.dm:422
    public virtual async DmTask emergency_shutdown()
    {
        last_program = program;
        active = FALSE;
        await load_program(offline_program, TRUE);
    }

    ///returns TRUE if all floors of the holodeck are present, returns FALSE if any are broken or removed
    // tg: code/modules/holodeck/computer.dm:428
    public virtual object floorcheck()
    {
        foreach (turf holo_floor in Dm.each<turf>(linked))
        {
            if (Dm.truthy(is_type_in_typecache(holo_floor, GLOB.typecache_holodeck_linked_floorcheck_ok)))
                continue;
            return FALSE;
        }
        return TRUE;
    }

    ///changes all weapons in the holodeck to do stamina damage if set
    // tg: code/modules/holodeck/computer.dm:436
    public virtual void nerf(object nerf_this = null, float? is_loading = null)
    {
        is_loading ??= TRUE;
        if (!Dm.truthy(nerf_this) && Dm.truthy(is_loading))
            return;
        foreach (obj_item to_be_nerfed in Dm.each<obj_item>(spawned))
            to_be_nerfed.damtype = Dm.truthy(nerf_this) ? STAMINA : Dm.initial(to_be_nerfed, (obj_item p) => p.damtype);
        foreach (obj_effect_holodeck_effect holo_effect in Dm.each<obj_effect_holodeck_effect>(effects))
            holo_effect.safety(nerf_this);
    }

    // tg: code/modules/holodeck/computer.dm:444
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        if (!Dm.truthy(LAZYLEN(emag_programs)))
        {
            balloon_alert(user, I18n.T("no card swipe port!"));
            return FALSE;
        }
        playsound(this, SFX_SPARKS, 75, TRUE);
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (user != null)
        {
            balloon_alert(user, I18n.T("safety protocols destroyed")); // im gonna keep this once since this perfectly describes it
            to_chat(user, span_warning(I18n.T("You override the safety and security protocols.")));
            user.log_message("emagged the Holodeck Control Console.", LOG_GAME);
            message_admins(Dm.text("[] emagged the Holodeck Control Console.", ADMIN_LOOKUPFLW(user)));
        }

        await say(I18n.T("Warning. Automatic shutoff and derezzing protocols have been corrupted. Please call Nanotrasen maintenance and do not use the simulator."));
        nerf(Dm.num(!Dm.truthy(Dm.bitand(obj_flags, EMAGGED))), FALSE);
        return TRUE;
    }

    // tg: code/modules/holodeck/computer.dm:462
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await emergency_shutdown();
        return dot;
    }

    // tg: code/modules/holodeck/computer.dm:468
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        Waiting.NoSleep(emergency_shutdown());
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/holodeck/computer.dm:472
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(reset_to_default());
        if (linked != null)
        {
            linked.linked = null;
            linked.energy_usage = Dm.list_n(AREA_USAGE_LEN);
        }
        return base.Destroy(force);
    }

    // tg: code/modules/holodeck/computer.dm:479
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        await emergency_shutdown();
        return await base.blob_act(attacking_blob);
    }
}

///this is what makes the holodeck not spawn anything on broken tiles (space and non engine plating / non holofloors)
// tg: code/modules/holodeck/computer.dm:188
public partial class datum_map_template_holodeck : datum_map_template
{
    // tg: code/modules/holodeck/computer.dm:189
    public override void update_blacklist(turf T = null, DmList input_blacklist = null)
    {
        turf placement = T;
        foreach (turf possible_blacklist in Dm.each<turf>(get_affected_turfs(placement)))
        {
            if (Dm.truthy(possible_blacklist.holodeck_compatible))
                continue;
            input_blacklist[possible_blacklist] = TRUE;
        }
    }
}
