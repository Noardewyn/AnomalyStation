// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4 (the floors subset): the floor's init (the station's dirt, GLOB.station_turfs), its broken and burnt states and its
// ChangeTurf; M4b: item_interaction (the extinguisher sprays an adjacent floor); M5 slice 4: its construction (replacing a
// tile, sheets on the floor, the crowbar's pry, make_plating, break_tile_to_plating, crush, remove_tile, spawn_tile). M5x
// slice 1: ex_act and is_explosion_shielded (explosions).
// M5x slice 5: the singularity's pull. Generated live-partial stubs (docs/ai/specs/live-partial.txt's P4 section):
// blobs, cult and rust (M10+). Silicons SI1c: acid_melt. E13: rcd_vals and rcd_act (the RCD). The var block is generated;
// /turf/open/floor/material (floor.dm:376) waits for materials.

// tg: code/game/turfs/open/floor.dm:2
public partial class turf_open_floor : turf_open
{
    // tg: code/game/turfs/open/floor.dm:128  (M4b: an item used on a floor, the extinguisher's spray at an adjacent tile)
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (Dm.truthy(overfloor_placed) && istype(tool, typeof(obj_item_stack_tile)))
        {
            await try_replace_tile(tool as obj_item_stack_tile, user, modifiers);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(user.combat_mode) && istype(tool, typeof(obj_item_stack_sheet)))
        {
            obj_item_stack_sheet sheets = tool as obj_item_stack_sheet;
            if (!Dm.truthy(await sheets.on_attack_floor(this, user, modifiers)))
                return ITEM_INTERACT_BLOCKING;

            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/turfs/open/floor.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && prob(33))
            MakeDirty();

        if (Dm.truthy(is_station_level(z)))
            Dm.append(ref GLOB.station_turfs, this);
        return dot;
    }

    // tg: code/game/turfs/open/floor.dm:37
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }

    // tg: code/game/turfs/open/floor.dm:40
    public override object burnt_states()
    {
        return Dm.list();
    }

    // tg: code/game/turfs/open/floor.dm:43
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(is_station_level(z)))
            Dm.remove(ref GLOB.station_turfs, this);
        return base.Destroy(force);
    }

    // tg: code/game/turfs/open/floor.dm:48  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of waiting-model.md
    // 5.4; ScrapeAway may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        dot.Value = base.ex_act(severity, target, __extra);

        // TURF-IDENTITY: tg's src after a ScrapeAway is the turf it left; the new turf is taken from its result (or, after
        // attempt_lattice_replacement, which returns none, from the cell)
        float cell_x = x, cell_y = y, cell_z = z;
        turf src = this;
        if (target == this)
        {
            await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        if (Dm.truthy(is_explosion_shielded(severity)))
            return FALSE;

        if (Dm.truthy(target))
            severity = EXPLODE_LIGHT;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                await ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR);
                break;
            case EXPLODE_HEAVY:
                switch (rand(1, 3))
                {
                    case 1:
                        if (!ispath(baseturf_at_depth(2), typeof(turf_open_floor)))
                        {
                            await attempt_lattice_replacement();
                            src = locate(cell_x, cell_y, cell_z) ?? src; // TURF-IDENTITY
                        }
                        else
                            src = await ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR) as turf ?? src; // TURF-IDENTITY
                        if (prob(33))
                            Dm.@new<obj_item_stack_sheet_iron>(src);
                        break;
                    case 2:
                        await ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR);
                        break;
                    case 3:
                        if (prob(80))
                            src = await ScrapeAway(flags: CHANGETURF_INHERIT_AIR) as turf ?? src; // TURF-IDENTITY
                        else
                            src.break_tile();
                        src.hotspot_expose(1000, CELL_VOLUME);
                        if (prob(33))
                            Dm.@new<obj_item_stack_sheet_iron>(src);
                        break;
                }
                break;
            case EXPLODE_LIGHT:
                if (prob(50))
                {
                    src.break_tile();
                    src.hotspot_expose(1000, CELL_VOLUME);
                }
                break;
        }

        return FALSE;
    }

    // tg: code/game/turfs/open/floor.dm:89
    public override object is_explosion_shielded(object severity = null)
    {
        if (Dm.num(severity) >= EXPLODE_DEVASTATE)
            return FALSE;
        foreach (obj blocker in Dm.each<obj>(this))
        {
            if (Dm.truthy(blocker.density))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/floor.dm:103
    public virtual async DmTask break_tile_to_plating()
    {
        turf_open_floor_plating T = await make_plating() as turf_open_floor_plating;
        if (!istype(T, typeof(turf_open_floor_plating)))
            return;
        T.break_tile();
    }

    // tg: code/game/turfs/open/floor.dm:207
    public override async DmTask acid_melt()
    {
        await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
    }

    /// Things seem to rely on this actually returning plating. Override it if you have other baseturfs.
    // tg: code/game/turfs/open/floor.dm:110
    public virtual async DmTask<object> make_plating(float? force = null)
    {
        force ??= FALSE;
        return await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
    }

    ///For when the floor is placed under heavy load. Calls break_tile(), but exists to be overridden by floor types that should resist crushing force.
    // tg: code/game/turfs/open/floor.dm:114
    public virtual void crush()
    {
        break_tile();
    }

    // tg: code/game/turfs/open/floor.dm:117
    public override async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        if (!isfloorturf(this))
            return await base.ChangeTurf(path, new_baseturfs, flags); //fucking turfs switch the fucking src of the fucking running procs
        if (!ispath(path, typeof(turf_open_floor)))
            return await base.ChangeTurf(path, new_baseturfs, flags);
        float old_dir = dir;
        turf_open_floor W = await base.ChangeTurf(path, new_baseturfs, flags) as turf_open_floor;
        await W.setDir(old_dir);
        W.update_appearance();
        return W;
    }

    // tg: code/game/turfs/open/floor.dm:144
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(overfloor_placed) && Dm.truthy(await pry_tile(tool, user)))
            return TRUE;
        return null;
    }

    // tg: code/game/turfs/open/floor.dm:148
    public virtual async DmTask<object> try_replace_tile(obj_item_stack_tile tile = null, mob user = null, DmList modifiers = null)
    {
        if (Dm.eq(tile.turf_type, type) && Dm.eq(tile.turf_dir, dir))
            return null;
        obj_item crowbar = user.is_holding_tool_quality(TOOL_CROWBAR) as obj_item;
        if (!Dm.truthy(crowbar))
            return null;
        turf_open_floor_plating plating = await pry_tile(crowbar, user, TRUE) as turf_open_floor_plating;
        if (!istype(plating, typeof(turf_open_floor_plating)))
            return null;
        await plating.base_item_interaction(user as mob_living, tile, modifiers);
        return null;
    }

    // tg: code/game/turfs/open/floor.dm:159
    public virtual async DmTask<object> pry_tile(obj_item I = null, mob user = null, float? silent = null)
    {
        silent ??= FALSE;
        I.play_tool_sound(this, 80);
        return await remove_tile(user, silent);
    }

    // tg: code/game/turfs/open/floor.dm:163
    public virtual async DmTask<object> remove_tile(mob user = null, float? silent = null, float? make_tile = null, object force_plating = null)
    {
        silent ??= FALSE;
        make_tile ??= TRUE;
        if (Dm.truthy(broken) || Dm.truthy(burnt))
        {
            broken = FALSE;
            burnt = FALSE;
            if (Dm.truthy(user) && !Dm.truthy(silent))
                to_chat(user, span_notice(I18n.T("You remove the broken plating.")));
        }
        else
        {
            if (Dm.truthy(user) && !Dm.truthy(silent))
                to_chat(user, span_notice(I18n.T("You remove the floor tile.")));
            if (Dm.truthy(make_tile))
                await spawn_tile();
        }
        return await make_plating(force_plating == null ? null : Dm.num(force_plating));
    }

    // tg: code/game/turfs/open/floor.dm:176
    public virtual object has_tile()
    {
        return floor_tile;
    }

    // tg: code/game/turfs/open/floor.dm:179
    public virtual async DmTask<object> spawn_tile()
    {
        if (!Dm.truthy(has_tile()))
            return null;
        return Dm.@new((Type)floor_tile, this);
    }
}

// tg: code/game/turfs/open/floor.dm:184 (interleaved types, rules 1.9; M5x slice 5)
public partial class turf_open_floor : turf_open
{
    // tg: code/game/turfs/open/floor.dm:184
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        bool sheer = false;
        float size = Dm.num(current_size);
        if (size == STAGE_THREE)
        {
            if (prob(30))
                sheer = true;
        }
        else if (size == STAGE_FOUR)
        {
            if (prob(50))
                sheer = true;
        }
        else if (size >= STAGE_FIVE)
        {
            if (prob(70))
                sheer = true;
        }
        if (sheer)
        {
            if (Dm.truthy(has_tile()))
                await remove_tile(null, TRUE, TRUE, TRUE);
        }
        return null;
    }
}

// tg: code/game/turfs/open/floor.dm:210 (interleaved types, rules 1.9; E13: the RCD)
public partial class turf_open_floor : turf_open
{
    // tg: code/game/turfs/open/floor.dm:245  rcd_vals' var/static/list/structure_costs (rules 10.6: no datum, a TgState field initializer)
    private static ref DmList rcd_vals__structure_costs => ref TgState.Current.turf_open_floor__rcd_vals__structure_costs;

    // tg: code/game/turfs/open/floor.dm:349  rcd_act's var/static/list/dir_types
    private static ref DmList rcd_act__dir_types => ref TgState.Current.turf_open_floor__rcd_act__dir_types;

    // tg: code/game/turfs/open/floor.dm:210
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        object sw__1 = the_rcd.mode;
        if (Dm.eq(sw__1, RCD_TURF))
        {
            obj_structure_girder girder = Dm.locate<obj_structure_girder>(this);
            if (Dm.truthy(girder))
                return girder.rcd_vals(user, the_rcd);

            return rcd_result_with_memory(
                Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 16)),
                this, RCD_MEMORY_WALL);
        }
        else if (Dm.eq(sw__1, RCD_WINDOWGRILLE))
        {
            obj_structure_grille grille = Dm.locate<obj_structure_grille>(this);
            if (Dm.truthy(grille))
                return grille.rcd_vals(user, the_rcd);

            //default cost for building a grill for fulltile windows
            float cost = 4;
            float delay = 1 * SECONDS;
            if (the_rcd.rcd_design_path == typeof(obj_structure_window))
            {
                cost = 4;
                delay = 2 * SECONDS;
            }
            else if (the_rcd.rcd_design_path == typeof(obj_structure_window_reinforced))
            {
                cost = 6;
                delay = 2.5f * SECONDS;
            }
            return rcd_result_with_memory(
                Dm.list(Dm.kv("delay", delay), Dm.kv("cost", cost)),
                this, RCD_MEMORY_WINDOWGRILLE);
        }
        else if (Dm.eq(sw__1, RCD_AIRLOCK))
        {
            if (Dm.ispath(the_rcd.rcd_design_path, typeof(obj_machinery_door_airlock_glass)))
                return Dm.list(Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 20));
            else
                return Dm.list(Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 16));
        }
        else if (Dm.eq(sw__1, RCD_STRUCTURE))
        {
            DmList design_data = rcd_vals__structure_costs[the_rcd.rcd_design_path] as DmList;
            if (design_data != null)
                return design_data;

            foreach (object structure in Dm.each(rcd_vals__structure_costs))
            {
                if (Dm.ispath(the_rcd.rcd_design_path, (Type)structure))
                    return rcd_vals__structure_costs[structure];
            }

            return FALSE;
        }
        else if (Dm.eq(sw__1, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("mode", RCD_DECONSTRUCT), Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 33));

        return FALSE;
    }

    // tg: code/game/turfs/open/floor.dm:272
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object sw__1 = rcd_data[RCD_DESIGN_MODE];
        if (Dm.eq(sw__1, RCD_TURF))
        {
            obj_structure_girder girder = Dm.locate<obj_structure_girder>(this);
            if (Dm.truthy(girder))
                return await girder.rcd_act(user, the_rcd, rcd_data);

            await place_on_top(typeof(turf_closed_wall));
            return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_WINDOWGRILLE))
        {
            obj_structure_grille grille = Dm.locate<obj_structure_grille>(this);
            if (Dm.truthy(grille))
                return await grille.rcd_act(user, the_rcd, rcd_data);

            //allow directional windows to be built without grills
            Type window_path = rcd_data[RCD_DESIGN_PATH] as Type;
            if (!Dm.truthy(Dm.initial(window_path, (obj_structure_window w) => w.fulltile)))
            {
                if (!Dm.truthy(valid_build_direction(this, user.dir, is_fulltile: FALSE)))
                {
                    balloon_alert(user, I18n.T("window already here!"));
                    return FALSE;
                }
                obj_structure_window WD = await Dm.new_async(window_path, this, user.dir) as obj_structure_window;
                await WD.set_anchored(TRUE);
                return TRUE;
            }

            //build grills to deal with full tile windows
            grille = Dm.@new<obj_structure_grille>(this);
            await grille.set_anchored(TRUE);
            return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_AIRLOCK))
        {
            Type airlock_type = rcd_data[RCD_DESIGN_PATH] as Type;

            if (Dm.ispath(airlock_type, typeof(obj_machinery_door_window)))
            {
                if (!Dm.truthy(valid_build_direction(this, user.dir, is_fulltile: FALSE)))
                {
                    balloon_alert(user, I18n.T("there's already a windoor!"));
                    return FALSE;
                }
                foreach (obj_machinery_door door in Dm.each<obj_machinery_door>(this))
                {
                    if (istype(door, typeof(obj_machinery_door_window)))
                        continue;
                    balloon_alert(user, I18n.T("there's already a door!"));
                    return FALSE;
                }
                //create the assembly and let it finish itself
                obj_structure_windoor_assembly assembly = Dm.@new<obj_structure_windoor_assembly>(this, user.dir);
                assembly.secure = Dm.ispath(airlock_type, typeof(obj_machinery_door_window_brigdoor)) ? TRUE : FALSE;
                assembly.electronics = the_rcd.airlock_electronics.create_copy(assembly);
                await assembly.finish_door();
                return TRUE;
            }

            foreach (obj_machinery_door door in Dm.each<obj_machinery_door>(this))
            {
                if (Dm.truthy(door.sub_door))
                    continue;
                balloon_alert(user, I18n.T("there's already a door!"));
                return FALSE;
            }
            //create the assembly and let it finish itself
            obj_structure_door_assembly assembly__1 = Dm.@new<obj_structure_door_assembly>(this);
            if (Dm.truthy(Dm.initial(airlock_type, (obj_machinery_door_airlock a) => a.glass)))
            {
                assembly__1.glass = TRUE;
                assembly__1.glass_type = airlock_type;
            }
            else
                assembly__1.airlock_type = airlock_type;
            assembly__1.electronics = the_rcd.airlock_electronics.create_copy(assembly__1);
            await assembly__1.finish_door();
            return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_STRUCTURE))
        {
            Type design_type = rcd_data[RCD_DESIGN_PATH] as Type;

            //map absolute types to basic subtypes
            Type locate_type = design_type;
            if (Dm.ispath(locate_type, typeof(obj_structure_frame_machine_secured)))
                locate_type = typeof(obj_structure_frame_machine);
            if (Dm.ispath(locate_type, typeof(obj_structure_frame_computer_rcd)))
                locate_type = typeof(obj_structure_frame_computer);
            if (Dm.ispath(locate_type, typeof(obj_structure_floodlight_frame_completed)))
                locate_type = typeof(obj_structure_floodlight_frame);
            if (Dm.truthy(Dm.locate_type(locate_type, this)))
                return FALSE;

            atom_movable design = await Dm.new_async(design_type, this) as atom_movable;
            if (Dm.truthy(is_path_in_list(locate_type, rcd_act__dir_types)))
                await design.setDir(user.dir);
            return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_DECONSTRUCT))
        {
            if (Dm.truthy(rcd_proof))
            {
                balloon_alert(user, I18n.T("it's too thick!"));
                return FALSE;
            }
            if (!Dm.truthy(await ScrapeAway(flags: CHANGETURF_INHERIT_AIR)))
                return FALSE;
            return TRUE;
        }
        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/turfs/open/floor.dm:245  /turf/open/floor/rcd_vals' var/static/list/structure_costs
    internal DmList turf_open_floor__rcd_vals__structure_costs = Dm.list(
        Dm.kv(typeof(obj_structure_reflector), Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 20))),
        Dm.kv(typeof(obj_structure_girder), Dm.list(Dm.kv("delay", 1.3f * SECONDS), Dm.kv("cost", 8))),
        Dm.kv(typeof(obj_structure_frame_machine_secured), Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 20))),
        Dm.kv(typeof(obj_structure_frame_computer_rcd), Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 20))),
        Dm.kv(typeof(obj_structure_floodlight_frame), Dm.list(Dm.kv("delay", 3 * SECONDS), Dm.kv("cost", 20))),
        Dm.kv(typeof(obj_structure_chair), Dm.list(Dm.kv("delay", 1 * SECONDS), Dm.kv("cost", 4))),
        Dm.kv(typeof(obj_structure_chair_stool_bar), Dm.list(Dm.kv("delay", 0.5f * SECONDS), Dm.kv("cost", 4))),
        Dm.kv(typeof(obj_structure_table), Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 8))),
        Dm.kv(typeof(obj_structure_bed), Dm.list(Dm.kv("delay", 2.5f * SECONDS), Dm.kv("cost", 8))),
        Dm.kv(typeof(obj_structure_rack), Dm.list(Dm.kv("delay", 2.5f * SECONDS), Dm.kv("cost", 4))));

    // tg: code/game/turfs/open/floor.dm:349  /turf/open/floor/rcd_act's var/static/list/dir_types
    internal DmList turf_open_floor__rcd_act__dir_types = Dm.list(
        typeof(obj_structure_chair),
        typeof(obj_structure_table),
        typeof(obj_structure_rack),
        typeof(obj_structure_bed));
}
