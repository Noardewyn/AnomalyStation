// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/openspace.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325, multi-z). The open space turf draws nothing of its own: the 3D client leaves its floor out and draws the
// level below through it.

// tg: code/game/turfs/open/openspace.dm:1
public partial class turf_open_openspace : turf_open
{
    // Reminder, any behavior code written here needs to be duped to /turf/open/space/openspace
    // I am so sorry
    // tg: code/game/turfs/open/openspace.dm:27
    public override object Initialize(object mapload = null, params object[] __extra) // handle plane and layer here so that they don't cover other obs/turfs in Dream Maker
    {
        base.Initialize(mapload, __extra);
        // tg: :29-30, PERFORM_ALL_TESTS(maptest_log_mapping) is FALSE in production (rules 1.7)
        RegisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_created));
        area our_area = loc as area;
        if (our_area is area_space)
            force_no_gravity = TRUE;
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/turfs/open/openspace.dm:38
    public override void LateInitialize(params object[] __extra)
    {
        ADD_TURF_TRANSPARENCY(this, INNATE_TRAIT);
    }

    // tg: code/game/turfs/open/openspace.dm:41
    public override async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        UnregisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON);
        return await base.ChangeTurf(path, new_baseturfs, flags);
    }

    /**
     * Prepares a moving movable to be precipitated if Move() is successful.
     * This is done in Enter() and not Entered() because there's no easy way to tell
     * if the latter was called by Move() or forceMove() while the former is only called by Move().
     */
    // tg: code/game/turfs/open/openspace.dm:50
    [DmParams("movable", "oldloc")]
    public override async DmTask<object> Enter(atom_movable movable = null, atom oldloc = null)
    {
        object dot = await base.Enter(movable, oldloc);
        if (Dm.truthy(dot))
            //higher priority than CURRENTLY_Z_FALLING so the movable doesn't fall on Entered()
            movable.set_currently_z_moving(CURRENTLY_Z_FALLING_FROM_MOVE);
        return dot;
    }

    ///Makes movables fall when forceMove()'d to this turf.
    // tg: code/game/turfs/open/openspace.dm:57
    [DmParams("movable")]
    public override async DmTask<object> Entered(atom_movable movable = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(movable, old_loc, old_locs);
        if (Dm.truthy(movable.set_currently_z_moving(CURRENTLY_Z_FALLING)))
            await zFall(movable, falling_from_move: TRUE);
        return dot;
    }

    // tg: code/game/turfs/open/openspace.dm:62
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        await zfall_if_on_turf(occupant);
    }

    /**
     * Drops movables spawned on this turf after they are successfully initialized.
     * so that spawned movables that should fall to gravity, will fall.
     */
    // tg: code/game/turfs/open/openspace.dm:70
    [SignalHandler]
    public virtual void on_atom_created(datum source = null, atom created_atom = null)
    {
        if (ismovable(created_atom))
            Waiting.NoSleep(zfall_if_on_turf(created_atom as atom_movable));
    }

    // tg: code/game/turfs/open/openspace.dm:75
    public virtual async DmTask zfall_if_on_turf(atom_movable movable = null)
    {
        if (QDELETED(movable) || movable.loc != this)
            return;
        await zFall(movable);
    }

    // tg: code/game/turfs/open/openspace.dm:80
    public override object can_have_cabling()
    {
        if (Dm.truthy(Dm.locate<obj_structure_lattice_catwalk>(this)))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/turfs/open/openspace.dm:85
    [DmParams()]
    public override object zAirIn(object direction = null, turf source = null)
    {
        return TRUE;
    }

    // tg: code/game/turfs/open/openspace.dm:88
    [DmParams()]
    public override object zAirOut(object direction = null, turf source = null)
    {
        return TRUE;
    }

    // tg: code/game/turfs/open/openspace.dm:91
    public override object zPassIn(object direction = null)
    {
        if (Dm.eq(direction, DOWN))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_IN_DOWN)))
                    return FALSE;
            }
            return TRUE;
        }
        if (Dm.eq(direction, UP))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_IN_UP)))
                    return FALSE;
            }
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/openspace.dm:104
    public override object zPassOut(object direction = null)
    {
        if (Dm.eq(direction, DOWN))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_OUT_DOWN)))
                    return FALSE;
            }
            return TRUE;
        }
        if (Dm.eq(direction, UP))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_OUT_UP)))
                    return FALSE;
            }
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/openspace.dm:117
    public virtual object CanCoverUp()
    {
        return can_cover_up;
    }

    // tg: code/game/turfs/open/openspace.dm:120
    public override object CanBuildHere()
    {
        return can_build_on;
    }

    // tg: code/game/turfs/open/openspace.dm:123
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (!Dm.truthy(CanBuildHere()))
            return dot;

        if (tool is obj_item_stack_rods)
        {
            await build_with_rods(tool as obj_item_stack_rods, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (ismetaltile(tool))
        {
            await build_with_floor_tiles(tool as obj_item_stack_tile_iron, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stack_thermoplastic)
        {
            await build_with_transport_tiles(tool as obj_item_stack_thermoplastic, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stack_sheet_mineral_titanium)
        {
            await build_with_titanium(tool as obj_item_stack_sheet_mineral_titanium, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/turfs/open/openspace.dm:146
    [DmParams("used_tiles")]
    public override async DmTask build_with_floor_tiles(obj_item_stack_tile_iron used_tiles = null, object user = null)
    {
        if (!Dm.truthy(CanCoverUp()))
            return;
        await base.build_with_floor_tiles(used_tiles, user);
    }

    // tg: code/game/turfs/open/openspace.dm:151
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF) && the_rcd.rcd_design_path == typeof(turf_open_floor_plating_rcd))
        {
            obj_structure_lattice L = Dm.locate<obj_structure_lattice>(this);
            if (Dm.truthy(L))
                return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 1));
            else
                return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 3));
        }

        return FALSE;
    }

    // tg: code/game/turfs/open/openspace.dm:161
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_TURF) && Dm.eq(rcd_data[RCD_DESIGN_PATH], typeof(turf_open_floor_plating_rcd)))
        {
            await place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/openspace.dm:167
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        atom_movable our_movable = pass_info.requester_ref?.resolve() as atom_movable;
        if (Dm.truthy(our_movable) && !Dm.truthy(our_movable.can_z_move(DOWN, this, null, ZMOVE_FALL_FLAGS))) //If we can't fall here (flying/lattice), it's fine to path through
            return TRUE;
        return FALSE;
    }

    // tg: code/game/turfs/open/openspace.dm:173
    public override async DmTask replace_floor(Type new_floor_path = null, object flags = null)
    {
        if (!Dm.truthy(Dm.initial(new_floor_path, (turf_open p) => p.overfloor_placed)))
        {
            await ChangeTurf(new_floor_path, flags: flags);
            return;
        }
        // Create plating under tiled floor we try to create directly onto the air
        await place_on_top(typeof(turf_open_floor_plating), flags: flags);
        await place_on_top(new_floor_path, flags: flags);
    }

    // tg: code/game/turfs/open/openspace.dm:182
    public override object can_cross_safely(atom_movable crossing = null)
    {
        return Dm.num(HAS_TRAIT(crossing, TRAIT_MOVE_FLYING) || !Dm.truthy(crossing.can_z_move(DOWN, this, z_move_flags: ZMOVE_FALL_FLAGS)));
    }
}

// tg: code/game/turfs/open/openspace.dm:185
public partial class turf_open_openspace_icemoon : turf_open_openspace
{
    // tg: code/game/turfs/open/openspace.dm:197
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        turf T = GET_TURF_BELOW(this) as turf;
        //I wonder if I should error here
        if (!Dm.truthy(T))
            return dot;
        if (Dm.truthy(Dm.bitand(T.turf_flags, NO_RUINS)) && Dm.truthy(protect_ruin))
        {
            turf newturf = Waiting.NoSleep(ChangeTurf(replacement_turf, null, CHANGETURF_IGNORE_AIR)) as turf;
            if (!isopenspaceturf(newturf)) // only openspace turfs should be returning INITIALIZE_HINT_LATELOAD
                return INITIALIZE_HINT_NORMAL;
            return dot;
        }
        if (!ismineralturf(T) || !Dm.truthy(drill_below))
            return dot;
        turf_closed_mineral M = T as turf_closed_mineral;
        M.mineral_amt = 0;
        Waiting.NoSleep(M.gets_drilled());
        baseturfs = typeof(turf_open_openspace_icemoon); //This is to ensure that IF random turf generation produces a openturf, there won't be other turfs assigned other than openspace.
        return dot;
    }
}
