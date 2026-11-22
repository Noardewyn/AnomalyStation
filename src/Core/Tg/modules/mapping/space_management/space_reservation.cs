// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/space_management/space_reservation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G1 (D272): the whole file. tg's turf reservations: the shuttles' templates load into them and the transit docks are
// made of them (the reserved level SSmapping adds at its initialize).

//Yes, they can only be rectangular.
//Yes, I'm sorry.
// tg: code/modules/mapping/space_management/space_reservation.dm:3
[DmPath("/datum/turf_reservation")]
public partial class datum_turf_reservation : datum
{
    // tg: code/modules/mapping/space_management/space_reservation.dm:41
    public virtual void Release()
    {
        bottom_left_turfs.Cut();
        top_right_turfs.Cut();

        DmList reserved_copy = reserved_turfs.Copy();
        Dm.remove(ref SSmapping.used_turfs, reserved_turfs);
        reserved_turfs = Dm.list();

        DmList cordon_copy = cordon_turfs.Copy();
        Dm.remove(ref SSmapping.used_turfs, cordon_turfs);
        cordon_turfs = Dm.list();

        DmList release_turfs = Dm.add(reserved_copy, cordon_copy) as DmList;

        foreach (turf reserved_turf in Dm.each<turf>(release_turfs))
        {
            // SEND_SIGNAL(reserved_turf, COMSIG_TURF_RESERVATION_RELEASED, src) (rules 11.1 (e), a statement)
            if (Dm.truthy(reserved_turf._listen_lookup?[COMSIG_TURF_RESERVATION_RELEASED]))
                reserved_turf._SendSignal(COMSIG_TURF_RESERVATION_RELEASED, Dm.list(reserved_turf, this));

            // immediately disconnect from atmos
            reserved_turf.blocks_air = TRUE;
            CALCULATE_ADJACENT_TURFS(reserved_turf, KILL_EXCITED);
        }

        // Makes the linter happy, even tho we don't await this
        Waiting.Detach(SSmapping.reserve_turfs(release_turfs)); // INVOKE_ASYNC
    }

    /// Attempts to calaculate and store a list of turfs around the reservation for cordoning. Returns whether a valid cordon was calculated
    // tg: code/modules/mapping/space_management/space_reservation.dm:66
    public virtual object calculate_cordon_turfs(turf bottom_left = null, turf top_right = null)
    {
        if (bottom_left.x < 2 || bottom_left.y < 2 || top_right.x > (Dm.world.maxx - 2) || top_right.y > (Dm.world.maxy - 2))
            return FALSE; // no space for a cordon here

        DmList possible_turfs = CORNER_OUTLINE(bottom_left, Dm.num(width), Dm.num(height));
        // if they're our cordon turfs, accept them
        Dm.remove(ref possible_turfs, cordon_turfs);
        foreach (turf cordon_turf in Dm.each<turf>(possible_turfs))
        {
            if (!Dm.truthy(Dm.bitand(cordon_turf.turf_flags, UNUSED_RESERVATION_TURF)))
                return FALSE;
        }
        Dm.combine(ref cordon_turfs, possible_turfs);

        if (Dm.truthy(pre_cordon_distance))
        {
            float distance = Dm.num(pre_cordon_distance);
            turf offset_turf = locate(bottom_left.x + distance, bottom_left.y + distance, bottom_left.z);
            DmList to_add = CORNER_OUTLINE(offset_turf, Dm.num(width) - distance * 2, Dm.num(height) - distance * 2); //we step-by-stop move inwards from the outer cordon
            foreach (turf turf_being_added in Dm.each<turf>(to_add))
                Dm.combine(ref pre_cordon_turfs, turf_being_added); //add one by one so we can filter out duplicates
        }

        return TRUE;
    }

    /// Actually generates the cordon around the reservation, and marking the cordon turfs as reserved
    // tg: code/modules/mapping/space_management/space_reservation.dm:87
    public virtual async DmTask generate_cordon()
    {
        foreach (turf cordon_turf in Dm.each<turf>(cordon_turfs))
        {
            var cordon_area = (area_misc_cordon)(GLOB.areas_by_type[typeof(area_misc_cordon)] ?? Dm.@new<area_misc_cordon>());
            var old_area = (area)cordon_turf.loc;

            // LISTASSERTLEN(old_area.turfs_to_uncontain_by_zlevel, cordon_turf.z, list()) (__HELPERS/_lists.dm:114), expanded inline
            datum_controller_subsystem_mapping.ListAssertLen(old_area.turfs_to_uncontain_by_zlevel, cordon_turf.z);
            datum_controller_subsystem_mapping.ListAssertLen(cordon_area.turfs_by_zlevel, cordon_turf.z);
            Dm.append_at(old_area.turfs_to_uncontain_by_zlevel, cordon_turf.z, cordon_turf);
            Dm.append_at(cordon_area.turfs_by_zlevel, cordon_turf.z, cordon_turf);
            Dm.append(cordon_area.contents, cordon_turf);

            // Its no longer unused, but its also not "used"
            cordon_turf.turf_flags = Dm.bitand(cordon_turf.turf_flags, Dm.bitnot(UNUSED_RESERVATION_TURF));
            await cordon_turf.empty(typeof(turf_cordon), typeof(turf_cordon));
            var new_cordon_turf = (turf)turf.dm_identity(cordon_turf); // TURF-IDENTITY: tg's cordon_turf names the new cordon
            dm_replace_in(cordon_turfs, cordon_turf, new_cordon_turf); // and so does tg's cordon_turfs
            DmList unused = SSmapping.unused_turfs[Dm.text("[]", new_cordon_turf.z)] as DmList;
            Dm.remove(unused, cordon_turf); // the replaced object: a cordon is no reservation turf, so ChangeTurf left it listed
            // still gets linked to us though
            SSmapping.used_turfs[new_cordon_turf] = this;
        }

        //swap the area with the pre-cordoning area
        foreach (turf pre_cordon_turf in Dm.each<turf>(pre_cordon_turfs))
            make_repel(pre_cordon_turf);
    }

    ///Register signals in the cordon "danger zone" to do something with whoever trespasses
    // tg: code/modules/mapping/space_management/space_reservation.dm:110
    public virtual void make_repel(turf pre_cordon_turf = null)
    {
        //Okay so hear me out. If we place a special turf IN the reserved area, it will be overwritten, so we can't do that
        //But signals are preserved even between turf changes, so even if we register a signal now it will stay even if that turf is overriden by the template
        RegisterSignals(pre_cordon_turf, Dm.list(COMSIG_QDELETING, COMSIG_TURF_RESERVATION_RELEASED), nameof(on_stop_repel));
    }

    // tg: code/modules/mapping/space_management/space_reservation.dm:116
    public virtual void on_stop_repel(turf pre_cordon_turf = null)
    {
        stop_repel(pre_cordon_turf);
    }

    ///Unregister all the signals we added in RegisterRepelSignals
    // tg: code/modules/mapping/space_management/space_reservation.dm:123
    public virtual void stop_repel(turf pre_cordon_turf = null)
    {
        UnregisterSignal(pre_cordon_turf, Dm.list(COMSIG_QDELETING, COMSIG_TURF_RESERVATION_RELEASED));
    }

    /// Internal proc which handles reserving the area for the reservation.
    // tg: code/modules/mapping/space_management/space_reservation.dm:152
    public virtual async DmTask<object> _reserve_area(object width = null, object height = null, object zlevel = null)
    {
        this.width = Dm.num(width);
        this.height = Dm.num(height);
        float w = Dm.num(width), h = Dm.num(height);
        if (w > Dm.world.maxx || h > Dm.world.maxy || w < 1 || h < 1)
            return FALSE;
        DmList avail = SSmapping.unused_turfs[Dm.text("[]", zlevel)] as DmList;
        turf BL = null;
        turf TR = null;
        DmList final = Dm.list();
        bool passing = false;
        foreach (object i in Dm.each(avail))
        {
            // CHECK_TICK (__DEFINES/_tick.dm:21), awaited in a suspending proc
            if (TICK_CHECK())
                await stoplag();
            BL = (turf)i;
            if (!Dm.truthy(Dm.bitand(BL.turf_flags, UNUSED_RESERVATION_TURF)))
                continue;
            if (BL.x + w > Dm.world.maxx || BL.y + h > Dm.world.maxy)
                continue;
            TR = locate(BL.x + w - 1, BL.y + h - 1, BL.z);
            if (!Dm.truthy(Dm.bitand(TR.turf_flags, UNUSED_RESERVATION_TURF)))
                continue;
            final = block(BL, TR);
            if (final == null)
                continue;
            passing = true;
            foreach (turf checking in Dm.each<turf>(final))
            {
                if (!Dm.truthy(Dm.bitand(checking.turf_flags, UNUSED_RESERVATION_TURF)))
                {
                    passing = false;
                    break;
                }
            }
            if (passing) // found a potentially valid area, now try to calculate its cordon
                passing = Dm.truthy(calculate_cordon_turfs(BL, TR));
            if (!passing)
                continue;
            break;
        }
        if (!passing || BL == null || TR == null)
            return FALSE;
        foreach (turf T in Dm.each<turf>(final))
        {
            Dm.combine(ref reserved_turfs, T);
            DmList unused = SSmapping.unused_turfs[Dm.text("[]", T.z)] as DmList;
            Dm.remove(unused, T);
            SSmapping.used_turfs[T] = this;
            T.turf_flags = Dm.bitand(Dm.bitor(T.turf_flags, RESERVATION_TURF), Dm.bitnot(UNUSED_RESERVATION_TURF));
            await T.empty(turf_type as Type, Dm.truthy(turf_type_is_baseturf) ? turf_type : null);
        }

        // TURF-IDENTITY: tg's BL and TR name their locations, whose turfs the loop above replaced
        Dm.append(ref bottom_left_turfs, turf.dm_identity(BL));
        Dm.append(ref top_right_turfs, turf.dm_identity(TR));
        return TRUE;
    }

    // tg: code/modules/mapping/space_management/space_reservation.dm:200
    public virtual async DmTask<object> reserve(object width = null, object height = null, object z_size = null, object z_reservation = null)
    {
        this.z_size = Dm.num(z_size);
        bool failed_reservation = false;
        for (float _ = 1, __end = Dm.num(z_size); _ <= __end; _++)
        {
            if (!Dm.truthy(await _reserve_area(width, height, z_reservation)))
            {
                failed_reservation = true;
                break;
            }
        }

        if (failed_reservation)
        {
            Release();
            return FALSE;
        }

        await generate_cordon();
        return TRUE;
    }

    /// Calculates the effective bounds information for the given turf. Returns a list of the information, or null if not applicable.
    // tg: code/modules/mapping/space_management/space_reservation.dm:216
    public virtual object calculate_turf_bounds_information(turf target = null)
    {
        for (float z_idx = 1, z__end = Dm.num(z_size); z_idx <= z__end; z_idx++)
        {
            var bottom_left = (turf)bottom_left_turfs[z_idx];
            var top_right = (turf)top_right_turfs[z_idx];
            float bl_x = bottom_left.x;
            float bl_y = bottom_left.y;
            float tr_x = top_right.x;
            float tr_y = top_right.y;

            if (target.x < bl_x)
                continue;

            if (target.y < bl_y)
                continue;

            if (target.x > tr_x)
                continue;

            if (target.y > tr_y)
                continue;

            DmList return_information = Dm.list();
            return_information["z_idx"] = z_idx;
            return_information["offset_x"] = target.x - bl_x;
            return_information["offset_y"] = target.y - bl_y;
            return return_information;
        }
        return null;
    }

    /// Gets the turf below the given target. Returns null if there is no turf below the target
    // tg: code/modules/mapping/space_management/space_reservation.dm:245
    public virtual object get_turf_below(turf target = null)
    {
        DmList bounds_info = calculate_turf_bounds_information(target) as DmList;
        if (bounds_info == null)
            return null;

        float z_idx = Dm.num(bounds_info["z_idx"]);
        // check what z level, if its the max, then there is no turf below
        if (Dm.eq(z_idx, z_size))
            return null;

        float offset_x = Dm.num(bounds_info["offset_x"]);
        float offset_y = Dm.num(bounds_info["offset_y"]);
        var bottom_left = (turf)bottom_left_turfs[z_idx + 1];
        return locate(bottom_left.x + offset_x, bottom_left.y + offset_y, bottom_left.z);
    }

    /// Gets the turf above the given target. Returns null if there is no turf above the target
    // tg: code/modules/mapping/space_management/space_reservation.dm:261
    public virtual object get_turf_above(turf target = null)
    {
        DmList bounds_info = calculate_turf_bounds_information(target) as DmList;
        if (bounds_info == null)
            return null;

        float z_idx = Dm.num(bounds_info["z_idx"]);
        // check what z level, if its the min, then there is no turf above
        if (z_idx == 1)
            return null;

        float offset_x = Dm.num(bounds_info["offset_x"]);
        float offset_y = Dm.num(bounds_info["offset_y"]);
        var bottom_left = (turf)bottom_left_turfs[z_idx - 1];
        return locate(bottom_left.x + offset_x, bottom_left.y + offset_y, bottom_left.z);
    }

    // TURF-IDENTITY (object-model.md 7.1; M10 slice G1): tg's reservation lists hold locations, so a reserved or reservable turf that
    // ChangeTurf replaces (a reservation emptying its block, a template loading into it, a shuttle moving through it) stays listed;
    // the port's lists hold the replaced object, so its entries become the new turf, in place.
    internal static void dm_replace_reserved_turf(turf old, turf replacement)
    {
        if (!Dm.truthy(Dm.bitand(replacement.turf_flags, Dm.bitor(RESERVATION_TURF, UNUSED_RESERVATION_TURF))))
            return;
        datum_controller_subsystem_mapping mapping = SSmapping;
        if (mapping == null)
            return;
        if (mapping.unused_turfs?[Dm.text("[]", replacement.z)] is DmList unused)
            dm_replace_in(unused, old, replacement);
        if (mapping.used_turfs == null || !Dm.@in(old, mapping.used_turfs))
            return;
        object owner = mapping.used_turfs[old];
        Dm.remove(mapping.used_turfs, old);
        mapping.used_turfs[replacement] = owner;
        if (owner is datum_turf_reservation reservation)
        {
            foreach (DmList listed in new[] { reservation.reserved_turfs, reservation.cordon_turfs, reservation.pre_cordon_turfs, reservation.bottom_left_turfs, reservation.top_right_turfs })
                dm_replace_in(listed, old, replacement);
        }
    }

    private static void dm_replace_in(DmList list, turf old, turf replacement)
    {
        if (list == null)
            return;
        for (float index = list.Find(old); index > 0; index = list.Find(old, index + 1))
            list[index] = replacement;
    }

    // tg: code/modules/mapping/space_management/space_reservation.dm:276
    public override async DmTask New(params object[] __extra)
    {
        // LAZYADD(SSmapping.turf_reservations, src) (__HELPERS/_lists.dm:62), expanded inline
        SSmapping.turf_reservations ??= Dm.list();
        Dm.append(ref SSmapping.turf_reservations, this);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mapping/space_management/space_reservation.dm:279
    public override object Destroy(float? force = null)
    {
        Release();
        // LAZYREMOVE(SSmapping.turf_reservations, src) (__HELPERS/_lists.dm:68), expanded inline
        if (SSmapping.turf_reservations != null)
        {
            Dm.remove(ref SSmapping.turf_reservations, this);
            if (SSmapping.turf_reservations.len == 0)
                SSmapping.turf_reservations = null;
        }
        return base.Destroy(force);
    }
}

// tg: code/modules/mapping/space_management/space_reservation.dm:126
public partial class datum_turf_reservation_transit : datum_turf_reservation
{
    // tg: code/modules/mapping/space_management/space_reservation.dm:126
    public override void make_repel(turf pre_cordon_turf = null)
    {
        base.make_repel(pre_cordon_turf);

        RegisterSignal(pre_cordon_turf, COMSIG_ATOM_ENTERED, nameof(space_dump_soft));
    }

    // tg: code/modules/mapping/space_management/space_reservation.dm:131
    public override void stop_repel(turf pre_cordon_turf = null)
    {
        base.stop_repel(pre_cordon_turf);

        UnregisterSignal(pre_cordon_turf, COMSIG_ATOM_ENTERED);
    }

    // tg: code/modules/mapping/space_management/space_reservation.dm:136
    public virtual void space_dump(atom source = null, atom_movable enterer = null)
    {
        dump_in_space(enterer);
    }

    ///Only dump if we don't have the hyperspace cordon movement exemption trait
    // tg: code/modules/mapping/space_management/space_reservation.dm:142
    public virtual void space_dump_soft(atom source = null, atom_movable enterer = null)
    {
        if (!HAS_TRAIT(enterer, TRAIT_FREE_HYPERSPACE_SOFTCORDON_MOVEMENT))
            space_dump(source, enterer);
    }
}
