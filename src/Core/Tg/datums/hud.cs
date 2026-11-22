// Translated from tgstation (snapshot 1d99bd7f): code/datums/hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Whole (M8 slice F, D185): the atom HUD datum with its show/hide, z-level and per-viewer image bookkeeping, GLOB.huds (the data HUD
// datums, created at global init as in tg) and the mob procs reload_huds and add_click_catcher. The images reach a viewer through
// client.images, which the replicator sends to that session alone (D40).

/* HUD DATUMS */

public partial class datum_controller_global_vars
{
    // tg: code/datums/hud.dm:3  GLOBAL_LIST_EMPTY(all_huds)
    public DmList all_huds;

    // tg: code/datums/hud.dm:3  (GLOBAL_MANAGED(all_huds, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_huds()
    {
        all_huds = Dm.list();
        Dm.append(ref gvars_datum_init_order, "all_huds");
    }

    ///gets filled by each /datum/atom_hud/New().
    ///associative list of the form: list(hud category = list(all global atom huds that use that category))
    // tg: code/datums/hud.dm:7  GLOBAL_LIST_EMPTY(huds_by_category)
    public DmList huds_by_category;

    // tg: code/datums/hud.dm:7  (GLOBAL_MANAGED(huds_by_category, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhuds_by_category()
    {
        huds_by_category = Dm.list();
        Dm.append(ref gvars_datum_init_order, "huds_by_category");
    }

    /// GLOBAL HUD LIST: the data HUD datums by their DATA_HUD_ key (an alist, so the numeric keys are keys)
    // tg: code/datums/hud.dm:10  GLOBAL_ALIST_INIT(huds, alist(...))
    public DmList huds;

    // tg: code/datums/hud.dm:10  (GLOBAL_MANAGED(huds, alist(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhuds()
    {
        huds = Dm.alist(
            Dm.kv(DATA_HUD_SECURITY_BASIC, Dm.@new<datum_atom_hud_data_human_security_basic>()),
            Dm.kv(DATA_HUD_SECURITY_ADVANCED, Dm.@new<datum_atom_hud_data_human_security_advanced>()),
            Dm.kv(DATA_HUD_MEDICAL_BASIC, Dm.@new<datum_atom_hud_data_human_medical_basic>()),
            Dm.kv(DATA_HUD_MEDICAL_ADVANCED, Dm.@new<datum_atom_hud_data_human_medical_advanced>()),
            Dm.kv(DATA_HUD_DIAGNOSTIC, Dm.@new<datum_atom_hud_data_diagnostic>()),
            Dm.kv(DATA_HUD_BOT_PATH, Dm.@new<datum_atom_hud_data_bot_path>()),
            Dm.kv(DATA_HUD_ABDUCTOR, Dm.@new<datum_atom_hud_abductor>()),
            Dm.kv(DATA_HUD_AI_DETECT, Dm.@new<datum_atom_hud_ai_detector>()),
            Dm.kv(DATA_HUD_FAN, Dm.@new<datum_atom_hud_data_human_fan_hud>()),
            Dm.kv(DATA_HUD_MALF_APC, Dm.@new<datum_atom_hud_data_malf_apc>()),
            Dm.kv(DATA_HUD_BLOOD, Dm.@new<datum_atom_hud_data_human_blood>()));
        Dm.append(ref gvars_datum_init_order, "huds");
    }

    /// Assoc list of traits to the huds they give.
    // tg: code/datums/hud.dm:25  GLOBAL_LIST_INIT(trait_to_hud, list(...))
    public DmList trait_to_hud;

    // tg: code/datums/hud.dm:25  (GLOBAL_MANAGED(trait_to_hud, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltrait_to_hud()
    {
        trait_to_hud = Dm.list(
            Dm.kv(TRAIT_ABDUCTOR_HUD, DATA_HUD_ABDUCTOR),
            Dm.kv(TRAIT_BOT_PATH_HUD, DATA_HUD_BOT_PATH),
            Dm.kv(TRAIT_CLOWN_ENJOYER, DATA_HUD_FAN),
            Dm.kv(TRAIT_DIAGNOSTIC_HUD, DATA_HUD_DIAGNOSTIC),
            Dm.kv(TRAIT_MEDICAL_HUD, DATA_HUD_MEDICAL_ADVANCED),
            Dm.kv(TRAIT_MEDICAL_HUD_SENSOR_ONLY, DATA_HUD_MEDICAL_BASIC),
            Dm.kv(TRAIT_MIME_FAN, DATA_HUD_FAN),
            Dm.kv(TRAIT_SECURITY_HUD, DATA_HUD_SECURITY_ADVANCED),
            Dm.kv(TRAIT_SECURITY_HUD_ID_ONLY, DATA_HUD_SECURITY_BASIC),
            Dm.kv(TRAIT_BLOOD_HUD, DATA_HUD_BLOOD));
        Dm.append(ref gvars_datum_init_order, "trait_to_hud");
    }

    /// Assoc list of traits that block other traits' huds to list of hud (traits) that they block
    // tg: code/datums/hud.dm:39  GLOBAL_LIST_INIT(trait_blockers_to_hud, list(...))
    public DmList trait_blockers_to_hud;

    // tg: code/datums/hud.dm:39  (GLOBAL_MANAGED(trait_blockers_to_hud, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltrait_blockers_to_hud()
    {
        trait_blockers_to_hud = Dm.list(
            Dm.kv(TRAIT_BLOCK_SECHUD, Dm.list(TRAIT_SECURITY_HUD, TRAIT_SECURITY_HUD_ID_ONLY)),
            Dm.kv(TRAIT_BLOCK_MEDHUD, Dm.list(TRAIT_MEDICAL_HUD, TRAIT_MEDICAL_HUD_SENSOR_ONLY)));
        Dm.append(ref gvars_datum_init_order, "trait_blockers_to_hud");
    }
}

// tg: code/datums/hud.dm:44
[DmPath("/datum/atom_hud")]
public partial class datum_atom_hud : datum
{
    // tg: code/datums/hud.dm:76
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.append(ref GLOB.all_huds, this);
        for (float z_level = 1; z_level <= world.maxz; z_level++)
        {
            Dm.append(ref hud_atoms, Dm.list(Dm.list()));
            Dm.append(ref hud_users, Dm.list(Dm.list()));
        }
        if (LAZYLEN(hud_icons) != 0)
            hud_icons = (DmList)string_list(hud_icons);

        RegisterSignal(SSdcs, COMSIG_GLOB_NEW_Z, nameof(add_z_level_huds));

        if (Dm.truthy(uses_global_hud_category))
        {
            foreach (object hud_icon in Dm.each(hud_icons))
                Dm.append_at(GLOB.huds_by_category, hud_icon, Dm.list(this));
        }
    }

    // tg: code/datums/hud.dm:90
    public override object Destroy(float? force = null)
    {
        foreach (mob mob in Dm.each_cast<mob>(hud_users_all_z_levels))
            hide_from(mob);

        foreach (atom atom in Dm.each_cast<atom>(hud_atoms_all_z_levels))
            remove_atom_from_hud(atom);

        if (Dm.truthy(uses_global_hud_category))
        {
            foreach (object hud_icon in Dm.each(hud_icons))
                LAZYREMOVEASSOC(ref GLOB.huds_by_category, hud_icon, this);
        }

        Dm.remove(ref GLOB.all_huds, this);
        return base.Destroy(force);
    }

    // DEVIATION(DV109): tg gives the lists one entry per z level at New and one more per COMSIG_GLOB_NEW_Z; a world whose maxz grew
    // without that signal (the toy rooms of the tests) would index past them, so every entry point first grows them to maxz.
    private void sync_z_levels()
    {
        while (Dm.length(hud_atoms) < world.maxz)
        {
            Dm.append(ref hud_atoms, Dm.list(Dm.list()));
            Dm.append(ref hud_users, Dm.list(Dm.list()));
        }
    }

    // tg: code/datums/hud.dm:104
    [SignalHandler]
    public virtual void add_z_level_huds()
    {
        Dm.append(ref hud_atoms, Dm.list(Dm.list()));
        Dm.append(ref hud_users, Dm.list(Dm.list()));
    }

    ///returns a list of all hud atoms in the given z level and linked lower z levels (because hud users in higher z levels can see below)
    // tg: code/datums/hud.dm:110
    public virtual object get_hud_atoms_for_z_level(object z_level = null)
    {
        sync_z_levels();
        if (Dm.num(z_level) <= 0)
            return FALSE;
        if (Dm.num(z_level) > Dm.length(hud_atoms))
        {
            stack_trace("get_hud_atoms_for_z_level() was given a z level index out of bounds of hud_atoms!", "code/datums/hud.dm", 114);
            return FALSE;
        }

        DmList result = Dm.list();
        Dm.append(ref result, hud_atoms[z_level]);

        float max_number_of_linked_z_levels_i_care_to_support_here = 10;

        while (Dm.truthy(max_number_of_linked_z_levels_i_care_to_support_here))
        {
            object lower_z_level_exists = SSmapping.level_trait(z_level, ZTRAIT_DOWN);

            if (Dm.truthy(lower_z_level_exists))
            {
                z_level = Dm.num(z_level) - 1;
                Dm.append(ref result, hud_atoms[z_level]);
                max_number_of_linked_z_levels_i_care_to_support_here--;
                continue;
            }

            else
                break;
        }
        return result;
    }

    ///returns a list of all hud users in the given z level and linked upper z levels (because hud users in higher z levels can see below)
    // tg: code/datums/hud.dm:135
    public virtual object get_hud_users_for_z_level(object z_level = null)
    {
        sync_z_levels();
        if (Dm.num(z_level) > Dm.length(hud_users) || Dm.num(z_level) <= 0)
        {
            stack_trace(Dm.text("get_hud_atoms_for_z_level() was given a z level index [] out of bounds 1->[] of hud_atoms!", z_level, Dm.length(hud_users)), "code/datums/hud.dm", 137);
            return FALSE;
        }

        DmList result = Dm.list();
        Dm.append(ref result, hud_users[z_level]);

        float max_number_of_linked_z_levels_i_care_to_support_here = 10;

        while (Dm.truthy(max_number_of_linked_z_levels_i_care_to_support_here))
        {
            object upper_level_exists = SSmapping.level_trait(z_level, ZTRAIT_UP);

            if (Dm.truthy(upper_level_exists))
            {
                z_level = Dm.num(z_level) + 1;
                Dm.append(ref result, hud_users[z_level]);
                max_number_of_linked_z_levels_i_care_to_support_here--;
                continue;
            }

            else
                break;
        }
        return result;
    }

    ///show this hud to the passed in user
    // tg: code/datums/hud.dm:158
    public virtual void show_to(mob new_viewer = null)
    {
        if (!Dm.truthy(new_viewer))
            return;
        sync_z_levels();

        if (!Dm.truthy(hud_users_all_z_levels[new_viewer]))
        {
            hud_users_all_z_levels[new_viewer] = 1f;

            RegisterSignal(new_viewer, COMSIG_QDELETING, nameof(unregister_atom), @override: TRUE); //both hud users and hud atoms use these signals
            RegisterSignal(new_viewer, COMSIG_MOVABLE_Z_CHANGED, nameof(on_atom_or_user_z_level_changed), @override: TRUE);
            RegisterSignal(new_viewer, COMSIG_LIVING_LOOK_Z_CHANGE, nameof(on_look_z_level_changed), @override: TRUE);

            turf their_turf = get_turf(new_viewer);
            if (!Dm.truthy(their_turf))
                return;
            ((DmList)hud_users[their_turf.z])[new_viewer] = TRUE;

            if (Dm.num(next_time_allowed[new_viewer]) > world.time)
            {
                if (!Dm.truthy(queued_to_see[new_viewer]))
                {
                    _addtimer(Dm.@new<datum_callback>(this, nameof(show_hud_images_after_cooldown), new_viewer), Dm.num(next_time_allowed[new_viewer]) - world.time, file: "code/datums/hud.dm", line: 175);
                    queued_to_see[new_viewer] = TRUE;
                }
            }

            else
            {
                next_time_allowed[new_viewer] = world.time + ADD_HUD_TO_COOLDOWN;
                foreach (atom hud_atom_to_add in Dm.each_cast<atom>(get_hud_atoms_for_z_level(their_turf.z)))
                    add_atom_to_single_mob_hud(new_viewer, hud_atom_to_add);
            }
        }
        else
            Dm.append_at(hud_users_all_z_levels, new_viewer, 1f); //increment the number of times this hud has been added to this hud user
    }

    ///Hides the images in this hud from former_viewer
    ///If absolute is set to true, this will forcefully remove the hud, even if sources in theory remain
    // tg: code/datums/hud.dm:188
    public virtual void hide_from(mob former_viewer = null, float? absolute = null)
    {
        absolute ??= FALSE;
        if (!Dm.truthy(former_viewer) || !Dm.truthy(hud_users_all_z_levels[former_viewer]))
            return;

        Dm.remove_at(hud_users_all_z_levels, former_viewer, 1f);//decrement number of sources for this hud on this user (bad way to track i know)

        if (Dm.truthy(absolute) || Dm.num(hud_users_all_z_levels[former_viewer]) <= 0)//if forced or there aren't any sources left, remove the user
        {
            if (!Dm.truthy(hud_atoms_all_z_levels[former_viewer]))//make sure we aren't unregistering changes on a mob that's also a hud atom for this hud
            {
                UnregisterSignal(former_viewer, COMSIG_MOVABLE_Z_CHANGED);
                UnregisterSignal(former_viewer, COMSIG_QDELETING);
                UnregisterSignal(former_viewer, COMSIG_LIVING_LOOK_Z_CHANGE);
            }

            Dm.remove(ref hud_users_all_z_levels, former_viewer);

            if (Dm.truthy(next_time_allowed[former_viewer]))
                Dm.remove(ref next_time_allowed, former_viewer);

            turf their_turf = get_turf(former_viewer);
            if (Dm.truthy(their_turf))
                Dm.remove_at(hud_users, their_turf.z, former_viewer);

            if (Dm.truthy(queued_to_see[former_viewer]))
                Dm.remove(ref queued_to_see, former_viewer);
            else if (Dm.truthy(their_turf))
            {
                foreach (atom hud_atom in Dm.each_cast<atom>(get_hud_atoms_for_z_level(their_turf.z)))
                    remove_atom_from_single_hud(former_viewer, hud_atom);
            }
        }
    }

    /// add new_hud_atom to this hud
    // tg: code/datums/hud.dm:217
    [DmParams("new_hud_atom")]
    public virtual object add_atom_to_hud(atom new_hud_atom = null, params object[] __extra)
    {
        if (!Dm.truthy(new_hud_atom))
            return FALSE;
        sync_z_levels();

        // No matter where or who you are, you matter to me :)
        RegisterSignal(new_hud_atom, COMSIG_MOVABLE_Z_CHANGED, nameof(on_atom_or_user_z_level_changed), @override: TRUE);
        RegisterSignal(new_hud_atom, COMSIG_QDELETING, nameof(unregister_atom), @override: TRUE); //both hud atoms and hud users use these signals
        hud_atoms_all_z_levels[new_hud_atom] = TRUE;

        turf atom_turf = get_turf(new_hud_atom);
        if (!Dm.truthy(atom_turf))
            return TRUE;

        Dm.combine_at(hud_atoms, atom_turf.z, new_hud_atom);

        foreach (mob mob_to_show in Dm.each_cast<mob>(get_hud_users_for_z_level(atom_turf.z)))
        {
            if (!Dm.truthy(queued_to_see[mob_to_show]))
                add_atom_to_single_mob_hud(mob_to_show, new_hud_atom);
        }
        return TRUE;
    }

    /// remove this atom from this hud completely
    // tg: code/datums/hud.dm:238
    public virtual object remove_atom_from_hud(atom hud_atom_to_remove = null)
    {
        if (!Dm.truthy(hud_atom_to_remove) || !Dm.truthy(hud_atoms_all_z_levels[hud_atom_to_remove]))
            return FALSE;

        //make sure we aren't unregistering a hud atom that's also a hud user mob
        if (!Dm.truthy(hud_users_all_z_levels[hud_atom_to_remove]))
        {
            UnregisterSignal(hud_atom_to_remove, COMSIG_MOVABLE_Z_CHANGED);
            UnregisterSignal(hud_atom_to_remove, COMSIG_QDELETING);
        }

        foreach (mob mob_to_remove in Dm.each_cast<mob>(hud_users_all_z_levels))
            remove_atom_from_single_hud(mob_to_remove, hud_atom_to_remove);

        Dm.remove(ref hud_atoms_all_z_levels, hud_atom_to_remove);

        turf atom_turf = get_turf(hud_atom_to_remove);
        if (!Dm.truthy(atom_turf))
            return TRUE;

        Dm.remove_at(hud_atoms, atom_turf.z, hud_atom_to_remove);

        return TRUE;
    }

    ///adds a newly active hud category's image on a hud atom to every mob that could see it
    // tg: code/datums/hud.dm:261
    public virtual object add_single_hud_category_on_atom(atom hud_atom = null, object hud_category_to_add = null)
    {
        if (!Dm.truthy(hud_atom?.active_hud_list?[hud_category_to_add]) || QDELING(hud_atom) || !Dm.@in(hud_category_to_add, hud_icons))
            return FALSE;

        if (!Dm.truthy(hud_atoms_all_z_levels[hud_atom]))
        {
            add_atom_to_hud(hud_atom);
            return TRUE;
        }

        turf atom_turf = get_turf(hud_atom);
        if (!Dm.truthy(atom_turf))
            return FALSE;

        foreach (mob hud_user in Dm.each_cast<mob>(get_hud_users_for_z_level(atom_turf.z)))
        {
            if (!Dm.truthy(hud_user.client))
                continue;
            if (!Dm.truthy(hud_exceptions[hud_user]) || !Dm.@in(hud_atom, hud_exceptions[hud_user]))
                Dm.combine(hud_user.client.images, hud_atom.active_hud_list[hud_category_to_add]);
        }

        return TRUE;
    }

    ///removes the image or images in hud_atom.hud_list[hud_category_to_remove] from every mob that can see it but leaves every other image
    ///from that atom there.
    // tg: code/datums/hud.dm:283
    public virtual object remove_single_hud_category_on_atom(atom hud_atom = null, object hud_category_to_remove = null)
    {
        if (QDELETED(hud_atom) || !Dm.@in(hud_category_to_remove, hud_icons) || !Dm.truthy(hud_atoms_all_z_levels[hud_atom]))
            return FALSE;

        if (!Dm.truthy(hud_atom.active_hud_list))
        {
            remove_atom_from_hud(hud_atom);
            return TRUE;
        }

        turf atom_turf = get_turf(hud_atom);
        if (!Dm.truthy(atom_turf))
            return FALSE;

        foreach (mob hud_user in Dm.each_cast<mob>(get_hud_users_for_z_level(atom_turf.z)))
        {
            if (!Dm.truthy(hud_user.client))
                continue;
            Dm.remove(hud_user.client.images, hud_atom.active_hud_list[hud_category_to_remove]);//by this point it shouldn't be in active_hud_list
        }

        return TRUE;
    }

    ///when a hud atom or hud user changes z levels this makes sure it gets the images it needs and removes the images it doesn't need.
    ///because of how signals work we need the same proc to handle both use cases because being a hud atom and being a hud user aren't mutually exclusive
    // tg: code/datums/hud.dm:304
    [SignalHandler]
    public virtual void on_atom_or_user_z_level_changed(atom_movable moved_atom = null, turf old_turf = null, turf new_turf = null)
    {
        sync_z_levels();
        if (Dm.truthy(old_turf))
        {
            if (Dm.truthy(hud_users_all_z_levels[moved_atom]))
            {
                Dm.remove_at(hud_users, old_turf.z, moved_atom);

                remove_all_atoms_from_single_hud(moved_atom as mob, get_hud_atoms_for_z_level(old_turf.z) as DmList);
            }

            if (Dm.truthy(hud_atoms_all_z_levels[moved_atom]))
            {
                Dm.remove_at(hud_atoms, old_turf.z, moved_atom);

                //this wont include moved_atom since its removed
                remove_atom_from_all_huds(get_hud_users_for_z_level(old_turf.z) as DmList, moved_atom);
            }
        }

        if (Dm.truthy(new_turf))
        {
            if (Dm.truthy(hud_users_all_z_levels[moved_atom]))
            {
                ((DmList)hud_users[new_turf.z])[moved_atom] = TRUE; //hud users is associative, hud atoms isn't

                add_all_atoms_to_single_mob_hud(moved_atom as mob, get_hud_atoms_for_z_level(new_turf.z) as DmList);
            }

            if (Dm.truthy(hud_atoms_all_z_levels[moved_atom]))
            {
                Dm.combine_at(hud_atoms, new_turf.z, moved_atom);

                add_atom_to_all_mob_huds(get_hud_users_for_z_level(new_turf.z) as DmList, moved_atom);
            }
        }
    }

    /// The same operations of on_atom_or_user_z_level_changed(), but without changing the mover's hud.
    /// (change the huds the user sees on other people, not the hud other people see on the user)
    /// Used for mobs that see through a separate eye mob, and looking up/down.
    // tg: code/datums/hud.dm:332
    [SignalHandler]
    public virtual void on_look_z_level_changed(atom_movable user = null, turf old_turf = null, turf new_turf = null)
    {
        sync_z_levels();
        // handle removing hud images from the old z-level
        if (Dm.truthy(old_turf) && Dm.truthy(hud_users_all_z_levels[user])) // old_turf can be null if we tried to look through the ceiling or through the floor
        {
            Dm.remove_at(hud_users, old_turf.z, user);

            remove_all_atoms_from_single_hud(user as mob, get_hud_atoms_for_z_level(old_turf.z) as DmList);
        }

        // handle adding hud images from the new z-level
        if (Dm.truthy(hud_users_all_z_levels[user]))
        {
            ((DmList)hud_users[new_turf.z])[user] = TRUE;

            add_all_atoms_to_single_mob_hud(user as mob, get_hud_atoms_for_z_level(new_turf.z) as DmList);
        }
    }

    /// add just hud_atom's hud images (that are part of this atom_hud) to requesting_mob's client.images list
    // tg: code/datums/hud.dm:348
    public virtual void add_atom_to_single_mob_hud(mob requesting_mob = null, atom hud_atom = null) //unsafe, no sanity apart from client
    {
        if (!Dm.truthy(requesting_mob) || !Dm.truthy(requesting_mob.client) || !Dm.truthy(hud_atom))
            return;

        foreach (object hud_category in Dm.each(Dm.bitand(hud_icons, hud_atom.active_hud_list)))
        {
            if (!Dm.truthy(hud_exceptions[requesting_mob]) || !Dm.@in(hud_atom, hud_exceptions[requesting_mob]))
                Dm.combine(requesting_mob.client.images, hud_atom.active_hud_list[hud_category]);
        }
    }

    /// all passed in hud_atoms's hud images (that are part of this atom_hud) to requesting_mob's client.images list
    /// optimization of [/datum/atom_hud/proc/add_atom_to_single_mob_hud] for hot cases, we assert that no nulls will be passed in via the list
    // tg: code/datums/hud.dm:358
    public virtual void add_all_atoms_to_single_mob_hud(mob requesting_mob = null, DmList hud_atoms = null) //unsafe, no sanity apart from client
    {
        if (!Dm.truthy(requesting_mob) || !Dm.truthy(requesting_mob.client))
            return;

        // Hud entries this mob ignores
        object mob_exceptions = hud_exceptions[requesting_mob];

        foreach (object hud_category in Dm.each(hud_icons))
        {
            foreach (atom hud_atom in Dm.each_cast<atom>(hud_atoms))
            {
                if (Dm.truthy(mob_exceptions) && Dm.@in(hud_atom, hud_exceptions[requesting_mob]))
                    continue;
                image output = hud_atom.active_hud_list?[hud_category] as image;
                // byond throws a fit if you try to add null to the images list
                if (!Dm.truthy(output))
                    continue;
                Dm.combine(requesting_mob.client.images, output);
            }
        }
    }

    /// add just hud_atom's hud images (that are part of this atom_hud) to all the requesting_mobs's client.images list
    /// optimization of [/datum/atom_hud/proc/add_atom_to_single_mob_hud] for hot cases, we assert that no nulls will be passed in via the list
    // tg: code/datums/hud.dm:377
    public virtual void add_atom_to_all_mob_huds(DmList requesting_mobs = null, atom hud_atom = null) //unsafe, no sanity apart from client
    {
        if (!Dm.truthy(hud_atom?.active_hud_list))
            return;

        DmList images_to_add = Dm.list();
        foreach (object hud_category in Dm.each(Dm.bitand(hud_icons, hud_atom.active_hud_list)))
            Dm.combine(ref images_to_add, hud_atom.active_hud_list[hud_category]);

        // Cache for sonic speed, lists are structs
        DmList exceptions = hud_exceptions;
        foreach (mob requesting_mob in Dm.each_cast<mob>(requesting_mobs))
        {
            if (!Dm.truthy(requesting_mob.client))
                continue;
            if (!Dm.truthy(exceptions[requesting_mob]) || !Dm.@in(hud_atom, exceptions[requesting_mob]))
                Dm.combine(requesting_mob.client.images, images_to_add);
        }
    }

    /// remove every hud image for this hud on atom_to_remove from client_mob's client.images list
    // tg: code/datums/hud.dm:394
    public virtual void remove_atom_from_single_hud(mob client_mob = null, atom atom_to_remove = null)
    {
        if (!Dm.truthy(client_mob) || !Dm.truthy(client_mob.client) || !Dm.truthy(atom_to_remove?.active_hud_list))
            return;
        foreach (object hud_image in Dm.each(hud_icons))
            Dm.remove(client_mob.client.images, atom_to_remove.active_hud_list[hud_image]);
    }

    /// remove every hud image for this hud pulled from atoms_to_remove from client_mob's client.images list
    /// optimization of [/datum/atom_hud/proc/remove_atom_from_single_hud] for hot cases, we assert that no nulls will be passed in via the list
    // tg: code/datums/hud.dm:402
    public virtual void remove_all_atoms_from_single_hud(mob client_mob = null, DmList atoms_to_remove = null)
    {
        if (!Dm.truthy(client_mob) || !Dm.truthy(client_mob.client))
            return;
        foreach (object hud_image in Dm.each(hud_icons))
        {
            foreach (atom atom_to_remove in Dm.each_cast<atom>(atoms_to_remove))
                Dm.remove(client_mob.client.images, atom_to_remove.active_hud_list?[hud_image]);
        }
    }

    /// remove every hud image for this hud on atom_to_remove from client_mobs's client.images list
    /// optimization of [/datum/atom_hud/proc/remove_atom_from_single_hud] for hot cases, we assert that no nulls will be passed in via the list
    // tg: code/datums/hud.dm:411
    public virtual void remove_atom_from_all_huds(DmList client_mobs = null, atom atom_to_remove = null)
    {
        if (!Dm.truthy(atom_to_remove?.active_hud_list))
            return;

        DmList images_to_remove = Dm.list();
        foreach (object hud_image in Dm.each(hud_icons))
            Dm.combine(ref images_to_remove, atom_to_remove.active_hud_list[hud_image]);

        foreach (mob client_mob in Dm.each_cast<mob>(client_mobs))
        {
            if (!Dm.truthy(client_mob.client))
                continue;
            Dm.remove(client_mob.client.images, images_to_remove);
        }
    }

    // tg: code/datums/hud.dm:424
    [SignalHandler]
    public virtual void unregister_atom(datum source = null, object force = null)
    {
        hide_from(source as mob, TRUE);
        remove_atom_from_hud(source as atom);
    }

    // tg: code/datums/hud.dm:429
    public virtual void hide_single_atomhud_from(mob hud_user = null, atom hidden_atom = null)
    {
        if (Dm.truthy(hud_users_all_z_levels[hud_user]))
            remove_atom_from_single_hud(hud_user, hidden_atom);

        if (!Dm.truthy(hud_exceptions[hud_user]))
            hud_exceptions[hud_user] = Dm.list(hidden_atom);
        else
            Dm.append_at(hud_exceptions, hud_user, hidden_atom);
    }

    // tg: code/datums/hud.dm:439
    public virtual void unhide_single_atomhud_from(mob hud_user = null, atom hidden_atom = null)
    {
        Dm.remove_at(hud_exceptions, hud_user, hidden_atom);

        turf hud_atom_turf = get_turf(hidden_atom);

        if (!Dm.truthy(hud_atom_turf))
            return;

        if (Dm.truthy(((DmList)hud_users[hud_atom_turf.z])[hud_user]))
            add_atom_to_single_mob_hud(hud_user, hidden_atom);
    }

    // tg: code/datums/hud.dm:450
    public virtual void show_hud_images_after_cooldown(mob queued_hud_user = null)
    {
        if (!Dm.truthy(queued_to_see[queued_hud_user]))
            return;

        Dm.remove(ref queued_to_see, queued_hud_user);
        next_time_allowed[queued_hud_user] = world.time + ADD_HUD_TO_COOLDOWN;

        turf user_turf = get_turf(queued_hud_user);
        if (!Dm.truthy(user_turf))
            return;

        foreach (atom hud_atom_to_show in Dm.each_cast<atom>(get_hud_atoms_for_z_level(user_turf.z)))
            add_atom_to_single_mob_hud(queued_hud_user, hud_atom_to_show);
    }
}

//MOB PROCS
// tg: code/datums/hud.dm:464
public partial class mob : atom_movable
{
    // tg: code/datums/hud.dm:465
    public virtual void reload_huds()
    {
        turf our_turf = get_turf(this);
        if (!Dm.truthy(our_turf))
            return;

        foreach (datum_atom_hud hud in Dm.each<datum_atom_hud>(GLOB.all_huds))
        {
            if (Dm.truthy(hud?.hud_users_all_z_levels[this]))
            {
                foreach (atom hud_atom in Dm.each_cast<atom>(hud.get_hud_atoms_for_z_level(our_turf.z)))
                    hud.add_atom_to_single_mob_hud(this, hud_atom);
            }
        }
    }
}

// tg: code/datums/hud.dm:474
public partial class mob_dead_new_player : mob_dead
{
    // tg: code/datums/hud.dm:475
    public override void reload_huds()
    {
        return;
    }
}
