// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/change_turf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P2: the file, but what the port does not have yet: the camera chunks (SScameras), the shuttle ceilings (multi-z, D108) and the
// open turf's wet floor (/turf/open/copyTurf). M4b slice 1 (the air that moves): the atmos adjacency, the active-turf
// bookkeeping, the inherited air (CHANGETURF_INHERIT_AIR) and Assimilate_Air. M7 lumcount: the lighting corners and the
// directional opacity (the lighting object is the turf's mark, DV85) and the space starlight. M10 slice G2: the global
// blacklisted_automated_baseturfs (:2), which the baseturf helpers read (baseturfs.dm).
// TURF-IDENTITY (reader.cs's header): after `new path(src)` tg's src is the new turf, so ChangeTurf's tail writes new_turf.

// tg: code/game/turfs/change_turf.dm:2  (GLOBAL_LIST_INIT(blacklisted_automated_baseturfs, ...), __DEFINES/__globals.dm)
public partial class datum_controller_global_vars : datum_controller
{
    // This is a list of turf types we dont want to assign to baseturfs unless through initialization or explicitly
    // tg: code/game/turfs/change_turf.dm:2
    public DmList blacklisted_automated_baseturfs;

    // tg: code/game/turfs/change_turf.dm:2  (GLOBAL_MANAGED(blacklisted_automated_baseturfs, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblacklisted_automated_baseturfs()
    {
        blacklisted_automated_baseturfs = typecacheof(Dm.list(typeof(turf_open_space), typeof(turf_baseturf_bottom))) as DmList;
        Dm.append(ref gvars_datum_init_order, "blacklisted_automated_baseturfs");
    }
}

// tg: code/game/turfs/change_turf.dm:7
public partial class turf : atom
{
    // tg: code/game/turfs/change_turf.dm:7
    // object, rules 3.4: tg passes FALSE for no turf change (jumpToNullSpace); an omitted argument is space
    public virtual async DmTask empty(object turf_type = null, object baseturf_type = null, DmList ignore_typecache = null, object flags = null)
    {
        turf_type ??= typeof(turf_open_space);
        // Remove all atoms except observers, landmarks, docking ports
        DmList ignored_atoms = typecacheof(Dm.list(typeof(mob_dead), typeof(obj_effect_landmark), typeof(obj_docking_port))) as DmList; // tg: var/static
        DmList allowed_contents = typecache_filter_list_reverse(get_all_contents_ignoring(ignore_typecache) as DmList, ignored_atoms) as DmList;
        Dm.remove(allowed_contents, this);
        for (float i = 1; i <= allowed_contents.len; i++)
        {
            object thing = allowed_contents[i];
            qdel(thing, force: TRUE);
        }

        if (Dm.truthy(turf_type))
        {
            var new_turf = await ChangeTurf(turf_type, baseturf_type as DmList, flags) as turf;
            SSair.remove_from_active(new_turf);
            CALCULATE_ADJACENT_TURFS(new_turf, KILL_EXCITED);
        }
    }

    // tg: code/game/turfs/change_turf.dm:21
    public virtual async DmTask<object> copyTurf(turf copy_to_turf = null, float? copy_air = null, object flags = null)
    {
        copy_air ??= FALSE;
        if (copy_to_turf.type != type)
        {
            await copy_to_turf.ChangeTurf(type, null, flags);
            copy_to_turf = (turf)turf.dm_identity(copy_to_turf); // TURF-IDENTITY: tg's copy_to_turf names the location, the new turf from here on
        }
        if (copy_to_turf.icon_state != icon_state)
            copy_to_turf.icon_state = icon_state;
        if (!Dm.eq(copy_to_turf.icon, icon))
            copy_to_turf.icon = icon;
        if (Dm.truthy(length(custom_materials)))
            await copy_to_turf.set_custom_materials(custom_materials);
        if (Dm.truthy(LAZYLEN(atom_colours)))
        {
            copy_to_turf.atom_colours = atom_colours.Copy();
            await copy_to_turf.update_atom_colour();
        }
        // New atom_colours system overrides color, but in rare cases its still used
        else if (Dm.truthy(color))
        {
            copy_to_turf.color = color;
        }
        if (!Dm.eq(copy_to_turf.dir, dir))
            await copy_to_turf.setDir(dir);
        return copy_to_turf;
    }

    //wrapper for ChangeTurf()s that you want to prevent/affect without overriding ChangeTurf() itself
    // tg: code/game/turfs/change_turf.dm:51
    public virtual async DmTask<object> TerraformTurf(object path = null, object new_baseturf = null, object flags = null, params object[] __extra)
    {
        return await ChangeTurf(path, new_baseturf as DmList, flags);
    }

    // Creates a new turf
    // new_baseturfs can be either a single type or list of types, formated the same as baseturfs. see turf.dm
    // tg: code/game/turfs/change_turf.dm:56
    public virtual async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        // (new_baseturfs: tg declares a list, but its callers pass a single type as well, which baseturfs_string_list keeps;
        // dm_change_turf_virtual hands such a type over in dm_pending_baseturfs)
        object untyped = dm_pending_baseturfs;
        dm_pending_baseturfs = null;
        return await dm_change_turf(path, (object)new_baseturfs ?? untyped, flags);
    }

    // tg's untyped new_baseturfs (one type) on its way through the ChangeTurf overrides, whose slot types it as a list.
    private object dm_pending_baseturfs;

    // A ChangeTurf call with tg's untyped new_baseturfs (ScrapeAway passes one type where its stack is down to one): the call
    // runs the ChangeTurf overrides (/turf/open's CHANGETURF_INHERIT_AIR, the floor's direction, space's nearstation area), as
    // tg's does (M5 slice 4: before, ScrapeAway went to the body directly and a pried floor lost its air).
    internal async DmTask<object> dm_change_turf_virtual(object path, object new_baseturfs, object flags)
    {
        if (new_baseturfs == null || new_baseturfs is DmList)
            return await ChangeTurf(path, (DmList)new_baseturfs, flags);
        dm_pending_baseturfs = new_baseturfs;
        try
        {
            return await ChangeTurf(path, null, flags);
        }
        finally
        {
            dm_pending_baseturfs = null;
        }
    }

    // The body of ChangeTurf with new_baseturfs as tg's untyped value (a list or one type, turf.dm's baseturfs format).
    internal async DmTask<object> dm_change_turf(object path, object new_baseturfs, object flags)
    {
        if (path == null)
            return null;
        if (Dm.eq(path, typeof(turf_baseturf_bottom)))
        {
            path = SSmapping.level_trait(z, ZTRAIT_BASETURF) ?? typeof(turf_open_space);
            if (!ispath(path))
            {
                path = text2path(Dm.text("[]", path));
                if (!ispath(path))
                {
                    warning(Dm.text("Z-level [] has invalid baseturf '[]'", z, SSmapping.level_trait(z, ZTRAIT_BASETURF)));
                    path = typeof(turf_open_space);
                }
            }
        }
        else if (Dm.eq(path, typeof(turf_open_space_basic)))
        {
            // basic doesn't initialize and this will cause issues
            // no warning though because this can happen naturaly as a result of it being built on top of
            path = typeof(turf_open_space);
        }

        if (!Dm.truthy(GLOB.use_preloader) && Dm.eq(path, type) && !Dm.truthy(Dm.bitand(flags, CHANGETURF_FORCEOP)) && Dm.eq(baseturfs, new_baseturfs)) // Don't no-op if the map loader requires it to be reconstructed, or if this is a new set of baseturfs
            return this;

        if (Dm.truthy(Dm.bitand(flags, CHANGETURF_SKIP)))
            return Dm.@new((Type)path, this);

        // DEVIATION(DV85): the turf's lighting object mark stands for old_lighting_object (no lighting objects exist)
        float old_lighting_object = dm_lighting_object;
        datum_lighting_corner old_lighting_corner_NE = lighting_corner_NE;
        datum_lighting_corner old_lighting_corner_SE = lighting_corner_SE;
        datum_lighting_corner old_lighting_corner_SW = lighting_corner_SW;
        datum_lighting_corner old_lighting_corner_NW = lighting_corner_NW;
        object old_rcd_memory = rcd_memory;
        DmList old_explosion_throw_details = explosion_throw_details;
        float old_directional_opacity = directional_opacity;
        float old_opacity = opacity;
        // I'm so sorry brother
        // This is used for a starlight optimization
        float old_light_range = light_range;
        // We get just the bits of explosive_resistance that aren't the turf
        float old_explosive_resistance = explosive_resistance - Dm.num(get_explosive_block());
        float old_lattice_underneath = lattice_underneath;

        DmList old_bp = blueprint_data;
        blueprint_data = null;

        object old_baseturfs = baseturfs;
        Type old_type = type;
        datum_weakref old_ref = weak_reference;
        weak_reference = null;

        DmList post_change_callbacks = Dm.list();
        if (Dm.truthy(_listen_lookup?[COMSIG_TURF_CHANGE]))
            _SendSignal(COMSIG_TURF_CHANGE, Dm.list(this, path, new_baseturfs, flags, post_change_callbacks));

        changing_turf = TRUE;
        qdel(this); //Just get the side effects and call Destroy
        //We do this here so anything that doesn't want to persist can clear itself
        DmList old_listen_lookup = _listen_lookup?.Copy();
        DmList old_signal_procs = _signal_procs?.Copy();
        object carryover_turf_flags = Dm.bitand(Dm.bitor(RESERVATION_TURF, UNUSED_RESERVATION_TURF), turf_flags);
        // Turf references survive replacement, but their signal lists must be set before Initialize.
        var new_turf = (turf)Dm.@new((Type)path, this, old_listen_lookup, old_signal_procs);
        dm_replaced_by = new_turf; // TURF-IDENTITY: the reference this replaced turf names from now on (turf.dm_identity)
        // TURF-IDENTITY (M8z): its listeners are the new turf's now, rekeyed to it in its New; in tg no other turf is left to signal them,
        // so the replaced one keeps none (a signal it still sent, the Exited of a thing leaving it, reached a climbable table's connect_loc
        // handler that no longer knew it)
        _listen_lookup = null;
        new_turf.turf_flags = Dm.num(Dm.bitor(new_turf.turf_flags, carryover_turf_flags));
        // TURF-IDENTITY (object-model.md 7.1, its M5 obligation, met by M3 P3 for the area's own lists): in tg the area's
        // turfs_by_zlevel and turfs_to_uncontain_by_zlevel go on holding src, which is the new turf; the port's replaced turf is
        // another object, so its entries became the new turf in the new turf's placement above (turf.builtin.cs dm_place_new).
        // TURF-IDENTITY (M10 slice G1): the same for the turf reservations' lists (SSmapping's used and unused turfs, the reservation's
        // reserved, cordon and corner turfs), which in tg hold the location
        datum_turf_reservation.dm_replace_reserved_turf(this, new_turf);
        // TURF-IDENTITY (M4b slice 1): the same for the atmos lists that hold src by reference, the neighbours'
        // atmos_adjacent_turfs and SSair's turf lists (the new turf's own vars start from its type, as in tg).
        dm_replace_atmos_turf(new_turf);
        CeilingSystem.Of(World.Current).TurfChanged((int)new_turf.x, (int)new_turf.y, (int)new_turf.z);
        // TURF-IDENTITY (M5x slice 1): SSexplosions' turf lists (a turf an explosion queued that an ex_act replaces before its
        // flames and throws are processed: a floor scraped to space keeps its hotspot and its throw details' turn).
        dm_replace_explosion_turf(new_turf);
        // TURF-IDENTITY (M5 slice 1): the datums that listen to src keep src as the key of their _signal_procs (an
        // atmos_sensitive grille registers its own check_atmos_process on its turf, atmos_sensitive.dm:13); in tg that key is
        // the new turf, which got src's listeners above, so their keys become the new turf, in place (else the new turf's
        // signal reaches a listener that does not know it: a breach's space turf exposing the grille that stayed). M5x slice 6:
        // done in the new turf's New, before its Initialize sends its first signals (turf.cs), with old_listen_lookup.

        foreach (datum_callback callback in Dm.each<datum_callback>(post_change_callbacks))
        {
            // TURF-IDENTITY (M4b slice 2): a callback that bound src (a firelock's adjacent_change binds the changed turf,
            // firedoor.dm:275) means the turf at this location, which in tg is the new turf; the port's replaced turf is
            // another object, so the bound reference becomes the new turf.
            if (callback.arguments != null)
                for (int i__1 = 1; i__1 <= callback.arguments.len; i__1++)
                    if (callback.arguments[i__1] == (object)this)
                        callback.arguments[i__1] = new_turf;
            callback.InvokeAsync(new_turf);
        }
        if (isgroundlessturf(new_turf) && !isgroundlessturf(this))
            await DropUnsupportedFixtures(new_turf);

        if (Dm.truthy(new_baseturfs))
            new_turf.baseturfs = await baseturfs_string_list(new_baseturfs, new_turf);
        else
            new_turf.baseturfs = await baseturfs_string_list(old_baseturfs, new_turf); //Just to be safe

        if (!Dm.truthy(Dm.bitand(flags, CHANGETURF_DEFER_CHANGE)))
            new_turf.AfterChange(flags, old_type);

        if (Dm.truthy(Dm.bitand(flags, CHANGETURF_GENERATE_SHUTTLE_CEILING)))
        {
            // tg: :127-134  the hull ceiling on the turf above: multi-z (D108) and shuttles
            Dm.Deferred("code/game/turfs/change_turf.dm:127", "ChangeTurf: CHANGETURF_GENERATE_SHUTTLE_CEILING (the turf above: multi-z and shuttles)");
        }

        new_turf.blueprint_data = old_bp;
        new_turf.rcd_memory = old_rcd_memory;
        new_turf.explosion_throw_details = old_explosion_throw_details;
        new_turf.explosive_resistance += old_explosive_resistance;

        // TURF-IDENTITY: tg's src here is the new turf
        new_turf.lighting_corner_NE = old_lighting_corner_NE;
        new_turf.lighting_corner_SE = old_lighting_corner_SE;
        new_turf.lighting_corner_SW = old_lighting_corner_SW;
        new_turf.lighting_corner_NW = old_lighting_corner_NW;
        // TURF-IDENTITY: the corners name src as their master (lighting_corner.dm:46-79), which in tg is the new turf
        if (old_lighting_corner_NE?.master_SW == this) old_lighting_corner_NE.master_SW = new_turf;
        if (old_lighting_corner_SE?.master_NW == this) old_lighting_corner_SE.master_NW = new_turf;
        if (old_lighting_corner_SW?.master_NE == this) old_lighting_corner_SW.master_NE = new_turf;
        if (old_lighting_corner_NW?.master_SE == this) old_lighting_corner_NW.master_SE = new_turf;

        new_turf.lattice_underneath = old_lattice_underneath;

        new_turf.weak_reference = old_ref;
        // TURF-IDENTITY: tg's weakref locates its datum by `locate(reference)` (weakrefs.dm:76), and BYOND's reference of a
        // turf is its location, so the carried-over weakref reaches the new turf. The port's REF names the replaced object.
        if (old_ref != null)
            old_ref.reference = REF(new_turf);

        if (Dm.truthy(SSlighting?.initialized)) // TURF-IDENTITY: src from here on is the new turf
        {
            // Space tiles should never have lighting objects
            if (!Dm.truthy(new_turf.space_lit))
            {
                // DEVIATION(DV85): the mark moves to the new turf (tg: lighting_object = old_lighting_object; vis_contents += it)
                if (Dm.truthy(old_lighting_object))
                    new_turf.dm_lighting_object = TRUE;
                // Should have a lighting object if we never had one
                else
                    new_turf.dm_lighting_object_new();
            }
            else if (Dm.truthy(old_lighting_object))
                new_turf.dm_lighting_object_del();

            new_turf.directional_opacity = old_directional_opacity;
            new_turf.recalculate_directional_opacity();

            // tg: :170-171  if(lighting_object && !lighting_object.needs_update) lighting_object.update(): the object's visual
            // update (DV85: no lighting objects; the client draws the light, D24)
        }

        // If we're space, then we're either lit, or not, and impacting our neighbors, or not
        if (isspaceturf(new_turf)) // TURF-IDENTITY: src, the new turf
        {
            turf_open_space lit_turf = (turf_open_space)new_turf;
            // This also counts as a removal, so we need to do a full rebuild
            if (!ispath(old_type, typeof(turf_open_space)))
            {
                await lit_turf.update_starlight();
                foreach (turf_open_space space_tile in Dm.each<turf_open_space>(Dm.sub(RANGE_TURFS(1, new_turf), new_turf)))
                    await space_tile.update_starlight();
            }
            else if (Dm.truthy(old_light_range))
                await lit_turf.enable_starlight();
        }
        // If we're a cordon we count against a light, but also don't produce any ourselves
        else if (new_turf is turf_cordon)
        {
            // This counts as removing a source of starlight, so we need to update the space tile to inform it
            if (!ispath(old_type, typeof(turf_open_space)))
            {
                foreach (turf_open_space space_tile in Dm.each<turf_open_space>(RANGE_TURFS(1, new_turf)))
                    await space_tile.update_starlight();
            }
        }
        // If we're not either, but were formerly a space turf, then we want light
        else if (ispath(old_type, typeof(turf_open_space)))
        {
            foreach (turf_open_space space_tile in Dm.each<turf_open_space>(RANGE_TURFS(1, new_turf)))
                await space_tile.enable_starlight();
        }

        if (old_opacity != new_turf.opacity && SSticker != null)
        {
            // tg: SScameras.bare_major_chunk_change(src): the cameras' visibility chunks (the camera network, a later milestone)
            Dm.Deferred("code/game/turfs/change_turf.dm:198", "ChangeTurf: SScameras.bare_major_chunk_change (the camera network, a later milestone)");
        }

        // We will only run this logic if the tile is not on the prime z layer, since we use area overlays to cover that
        if (Dm.truthy(SSmapping.z_level_to_plane_offset?[new_turf.z])) // TURF-IDENTITY: src.z, the new turf's
        {
            // tg: :203-205  the area's lighting_effects overlay of the plane offset: BYOND's lighting (D114) on stacked levels
            Dm.Deferred("code/game/turfs/change_turf.dm:203", "ChangeTurf: the area lighting overlay of an offset plane (BYOND lighting, multi-z)");
        }

        // only queue for smoothing if SSatom initialized us, and we'd be changing smoothing state
        if (Dm.truthy(Dm.bitand(new_turf.flags_1, INITIALIZED_1)))
        {
            QUEUE_SMOOTH_NEIGHBORS(new_turf);
            QUEUE_SMOOTH(new_turf);
        }

        // we need to update gravity for any mob on a tile that is being created or destroyed
        foreach (mob_living target in Dm.each<mob_living>(new_turf.contents))
            await target.refresh_gravity();

        return new_turf;
    }

    // TURF-IDENTITY: the entry of this (replaced) turf in its area's per-z turf lists becomes new_turf, at its index. Called for
    // every new turf that replaces a turf of the map (turf.builtin.cs dm_place_new: ChangeTurf's, and tg's own `new T(turf)` of the
    // cave generator, the biomes and the rivers, which BYOND keeps listed because a turf reference is its location). A generator
    // or a map load replaces turfs in the listing's own order, so the search starts after the area's last replacement and wraps
    // (tg's area_contents unit test checks the lists; a search from the start made a generated level quadratic).
    // A turf can be listed twice: change_area leaves it in the old area's list until the area cleans its
    // turfs_to_uncontain_by_zlevel (areas.dm:332), so a turf that leaves an area and comes back before the cleaning sits there
    // twice, once to be taken out (a shuttle that docks at a transit block and leaves it). Every entry must name the new turf,
    // or the cleaning's subtraction takes out the new one and leaves the deleted one; such a turf is in the area's
    // turfs_to_uncontain list, so only then are the lists searched whole.
    internal void dm_replace_listed_turf(turf new_turf)
    {
        if (new_turf.loc is not area our_area)
            return;
        float level = new_turf.z; // the replaced turf is off the grid
        bool uncontained = dm_replace_every_listed(our_area.turfs_to_uncontain_by_zlevel, level, new_turf);
        DmList by_zlevel = our_area.turfs_by_zlevel;
        if (uncontained)
        {
            dm_replace_every_listed(by_zlevel, level, new_turf);
            return;
        }
        if (by_zlevel == null || level < 1 || level > by_zlevel.len)
            return;
        if (by_zlevel[level] is not DmList zturfs || zturfs.len == 0)
            return;
        float hint = Math.Clamp(our_area.dm_relist_hint, 1, zturfs.len);
        float index = zturfs.Find(this, hint);
        if (index == 0 && hint > 1)
            index = zturfs.Find(this, 1, hint);
        if (index == 0)
            return;
        zturfs[index] = new_turf;
        our_area.dm_relist_hint = index + 1;
    }

    // Every entry of this turf in by_zlevel[level] becomes new_turf; whether there was one.
    private bool dm_replace_every_listed(DmList by_zlevel, float level, turf new_turf)
    {
        if (by_zlevel == null || level < 1 || level > by_zlevel.len || by_zlevel[level] is not DmList zturfs)
            return false;
        bool found = false;
        for (float index = zturfs.Find(this); index != 0; index = index < zturfs.len ? zturfs.Find(this, index + 1) : 0)
        {
            zturfs[index] = new_turf;
            found = true;
        }
        return found;
    }

    // TURF-IDENTITY: in tg a turf reference is its location, so after the replacement every list that held src holds the
    // new turf: the atmos-adjacent neighbours' atmos_adjacent_turfs (keys, with their values), SSair's active turfs and
    // queues, and src's excited group (CHANGETURF_INHERIT_AIR hands it to the new turf). The port's replaced turf is
    // another object, so its entries become the new turf, in place.
    private void dm_replace_atmos_turf(turf new_turf)
    {
        foreach (turf neighbor in Dm.each<turf>(atmos_adjacent_turfs))
            dm_replace_turf_in(neighbor.atmos_adjacent_turfs, new_turf);
        if (SSair != null)
        {
            foreach (DmList listed in new[] { SSair.active_turfs, SSair.currentrun, SSair.adjacent_rebuild, SSair.high_pressure_delta,
                SSair.active_super_conductivity, SSair.atom_process, SSair.queued_for_activation })
                dm_replace_turf_in(listed, new_turf);
        }
        if (this is turf_open open && open.excited_group != null)
            dm_replace_turf_in(open.excited_group.turf_list, new_turf);
    }

    // TURF-IDENTITY (M5x slice 1): SSexplosions' lists hold the turfs an explosion reached (explosions.dm:26-35) by location in
    // tg; the port's replaced turf is another object, so its entries become the new turf, in place.
    private void dm_replace_explosion_turf(turf new_turf)
    {
        if (SSexplosions == null)
            return;
        foreach (DmList listed in new[] { SSexplosions.lowturf, SSexplosions.medturf, SSexplosions.highturf, SSexplosions.flameturf,
            SSexplosions.throwturf, SSexplosions.held_throwturf })
            dm_replace_turf_in(listed, new_turf);
    }

    internal void dm_replace_signal_turf(DmList listen_lookup, turf new_turf)
    {
        if (listen_lookup == null)
            return;
        foreach (object sigtype in Dm.each(listen_lookup))
        {
            object listeners = listen_lookup[sigtype];
            if (listeners is DmList many)
            {
                foreach (datum listener in Dm.each<datum>(many))
                    dm_replace_listener_turf(listener, new_turf);
            }
            else if (listeners is datum one)
                dm_replace_listener_turf(one, new_turf);
        }
    }

    private void dm_replace_listener_turf(datum listener, turf new_turf)
    {
        dm_replace_turf_in(listener._signal_procs, new_turf);
        // TURF-IDENTITY (M5 slice 4, the M5 obligation): the elements that listen to their occupied turfs key their own lists by
        // the turf (tg's location reference): give_turf_traits' trait_sources (a lattice's or catwalk's turf traits, reapplied
        // by its post-change callback from that key) and footstep_override's occupied_turfs (a lattice's catwalk steps).
        if (listener is datum_element_give_turf_traits turf_traits)
            dm_replace_turf_in(turf_traits.trait_sources, new_turf);
        else if (listener is datum_element_footstep_override footsteps)
            dm_replace_turf_in(footsteps.occupied_turfs, new_turf);
    }

    private void dm_replace_turf_in(DmList listed, turf new_turf)
    {
        if (listed == null)
            return;
        for (float index = listed.Find(this); index > 0; index = listed.Find(this, index + 1))
        {
            object value = listed[(object)this];
            listed[index] = new_turf;
            if (value != null)
                listed[(object)new_turf] = value == (object)this ? new_turf : value;
        }
    }

    //If you modify this function, ensure it works correctly with lateloaded map templates.
    // tg: code/game/turfs/change_turf.dm:239
    // DEVIATION(DV178): a floor that becomes space or an open hole takes with it what was fixed to it: tg leaves vents, pipes,
    // holopads and cables hanging in space (they read as space debris in 2D, as floating fixtures in the 3D view). They fall
    // apart as a falling wall mount does (atom_mounted.dm:104), and what stays of them is unanchored and drifts. A lattice or
    // a catwalk under them still holds them; wall mounts keep their wall.
    private static async DmTask DropUnsupportedFixtures(turf floor)
    {
        if (Dm.truthy(Dm.locate<obj_structure_lattice>(floor)))
            return;
        foreach (obj fixture in Dm.each<obj>(floor.contents))
        {
            if (QDELETED(fixture) || HAS_TRAIT(fixture, TRAIT_WALLMOUNTED)
                || fixture is not (obj_machinery or obj_structure_cable or obj_structure_disposalpipe))
                continue;
            if (InertSeam.IsInert(fixture) || InertSeam.HasUndeclaredOverride(fixture, "atom_deconstruct")
                || InertSeam.HasUndeclaredOverride(fixture, "on_deconstruction"))
                qdel(fixture);
            else
                await fixture.deconstruct(FALSE);
        }
        foreach (obj left in Dm.each<obj>(floor.contents))
            if (!QDELETED(left) && Dm.truthy(left.anchored) && !HAS_TRAIT(left, TRAIT_WALLMOUNTED) && left is obj_machinery or obj_structure_frame)
                await left.set_anchored(FALSE);
    }

    public virtual void AfterChange(object flags = null, object oldType = null) //called after a turf has been replaced in ChangeTurf()
    {
        levelupdate();
        if (Dm.truthy(Dm.bitand(flags, CHANGETURF_RECALC_ADJACENT)))
        {
            immediate_calculate_adjacent_turfs();
            if (ispath(oldType, typeof(turf_closed)) && isopenturf(this))
                SSair.add_to_active(this);
        }
        else //In effect, I want closed turfs to make their tile active when sheered, but we need to queue it since they have no adjacent turfs
            CALCULATE_ADJACENT_TURFS(this, (ispath(oldType, typeof(turf_closed)) && isopenturf(this) ? MAKE_ACTIVE : NORMAL_TURF));
    }
}

// tg: code/game/turfs/change_turf.dm:212
public partial class turf_open : turf
{
    //Resist the temptation to make this default to keeping air.
    // tg: code/game/turfs/change_turf.dm:212
    public override async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        if (Dm.truthy(Dm.bitand(flags, CHANGETURF_INHERIT_AIR)) && ispath(path, typeof(turf_open)))
        {
            datum_gas_mixture stashed_air = Dm.@new<datum_gas_mixture>();
            stashed_air.copy_from(air);
            float stashed_state = excited;
            datum_excited_group stashed_group = excited_group;
            object dot = await base.ChangeTurf(path, new_baseturfs, flags); //If path == type this will return us, don't bank on making a new type
            if (!Dm.truthy(dot)) // changeturf failed or didn't do anything
                return dot;
            turf_open new_turf = dot as turf_open;
            new_turf.air.copy_from(stashed_air);
            new_turf.excited = stashed_state;
            new_turf.excited_group = stashed_group;
            // :224-226  #ifdef VISUALIZE_ACTIVE_TURFS #endif: inactive in production (rules 1.7)
            if (stashed_group != null)
            {
                if (Dm.truthy(stashed_group.should_display) || Dm.truthy(SSair.display_all_groups))
                    stashed_group.display_turf(new_turf);
            }
            return dot;
        }
        else
        {
            if (Dm.truthy(excited) || excited_group != null)
                SSair.remove_from_active(this); //Clean up wall excitement, and refresh excited groups
            if (ispath(path, typeof(turf_closed)) || ispath(path, typeof(turf_cordon)))
                flags = Dm.bitor(flags, CHANGETURF_RECALC_ADJACENT);
            return await base.ChangeTurf(path, new_baseturfs, flags);
        }
    }

    // tg: code/game/turfs/change_turf.dm:249
    public override void AfterChange(object flags = null, object oldType = null)
    {
        base.AfterChange(flags, oldType);
        RemoveLattice();
        if (!Dm.truthy(Dm.bitand(flags, Dm.bitor(CHANGETURF_IGNORE_AIR, CHANGETURF_INHERIT_AIR))))
            Assimilate_Air();
    }

    //////Assimilate Air//////
    // tg: code/game/turfs/change_turf.dm:255
    public virtual void Assimilate_Air()
    {
        float turf_count = LAZYLEN(atmos_adjacent_turfs);
        if (Dm.truthy(blocks_air) || turf_count == 0) //if there weren't any open turfs, no need to update.
            return;

        datum_gas_mixture total = Dm.@new<datum_gas_mixture>(); //Holders to assimilate air from nearby turfs
        //Stolen blatently from self_breakdown
        DmList turf_list = Dm.add(atmos_adjacent_turfs, this) as DmList;
        float turflen = turf_list.len;
        float energy = 0;
        float heat_cap = 0;

        foreach (turf_open turf in Dm.each<turf_open>(turf_list))
        {
            //Cache?
            datum_gas_mixture mix = turf.air;
            //"borrowing" this code from merge(), I need to play with the temp portion. Lets expand it out
            //temperature = (giver.temperature * giver_heat_capacity + temperature * self_heat_capacity) / combined_heat_capacity
            float capacity = Dm.num(mix.heat_capacity());
            energy += mix.temperature * capacity;
            heat_cap += capacity;

            foreach ((object giver_id, object amount) in Dm.pairs(mix.moles))
                total.adjust_gas(giver_id, amount);
        }

        total.temperature = energy / heat_cap;
        DmList cached_total_moles = total.moles;
        foreach (object id in Dm.each(cached_total_moles))
            cached_total_moles[id] = Dm.num(cached_total_moles[id]) / turflen;

        foreach (turf_open turf in Dm.each<turf_open>(turf_list))
        {
            turf.air.copy_from(total);
            turf.update_visuals();
            SSair.add_to_active(turf);
        }
    }
}

// tg: code/game/turfs/change_turf.dm:290
public partial class turf : atom
{
    /// Attempts to replace a tile with lattice. Amount is the amount of tiles to scrape away.
    // tg: code/game/turfs/change_turf.dm:290
    public virtual async DmTask attempt_lattice_replacement(float? amount = null)
    {
        amount ??= 2;
        if (!Dm.truthy(lattice_underneath))
        {
            await ScrapeAway(amount, flags: CHANGETURF_INHERIT_AIR);
            return;
        }

        DmList successful_replacement_callbacks = Dm.list();
        if (Dm.truthy(_listen_lookup?[COMSIG_TURF_ATTEMPT_LATTICE_REPLACEMENT]))
            _SendSignal(COMSIG_TURF_ATTEMPT_LATTICE_REPLACEMENT, Dm.list(this, successful_replacement_callbacks));
        object new_turf = await ScrapeAway(amount, flags: CHANGETURF_INHERIT_AIR);
        if (new_turf is turf_open_floor)
            return;

        // TURF-IDENTITY: tg's src is the turf the scrape left
        object new_lattice = Dm.@new<obj_structure_lattice>(new_turf as turf ?? this);
        foreach (datum_callback callback in Dm.each<datum_callback>(successful_replacement_callbacks))
            await callback.Invoke(new_lattice);
    }
}

public partial class area
{
    // TURF-IDENTITY: where the last replaced turf of this area sat in its per-z turf list (turf.dm_replace_listed_turf's search hint).
    internal float dm_relist_hint = 1;
}
