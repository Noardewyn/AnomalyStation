// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/transit_tubes/transit_tube_pod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Transit tubes (the owner, 2026-10-26): the whole file. The pod rides a force_move_dir loop from tube to tube (get_exit, has_entrance,
// the tubes' delays), stops at a station, leaves the tubes with a collision where they end and throws its contents out when it lands
// outside one; its own air, the occupant's moves (relaymove: out of an open station, a launch, a junction's exit) and its breakout.
// The var blocks are generated. MOVE_ANIMATION_STAGE_ONE and _TWO (transit_tube_pod.dm:1-2) are defined and never used.

// tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:5
public partial class obj_structure_transit_tube_pod : obj_structure
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:15
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_oxygen), MOLES_O2STANDARD), Dm.kv(typeof(datum_gas_nitrogen), MOLES_N2STANDARD));
        air_contents.adjust_multiple_gases(new_gases);
        air_contents.temperature = T20C;
        return dot;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:21
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(empty_pod());
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:25
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(Dm.length(contents)) ? occupied_icon_state : Dm.initial<obj_structure_transit_tube_pod, string>(this, p => p.icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:29
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(moving))		// can you even click on one of these while they're in a tube?
            return NONE;	// for that matter can you click them when they're outside of a tube?

        tool.play_tool_sound(this);
        if (Dm.truthy(Dm.length(contents)))
        {
            user.visible_message(span_notice(I18n.T("[] empties \\the [].", user, this)),
                span_notice(I18n.T("You empty \\the [].", this)));
            await empty_pod();
        }
        else
            await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:42
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        atom location = get_turf(this);
        obj_structure_c_transit_tube_pod tube_pod = Dm.@new<obj_structure_c_transit_tube_pod>(location);
        transfer_fingerprints_to(tube_pod);
        await tube_pod.setDir(dir);
        await empty_pod(location);
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:49
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object dot = base.ex_act(severity, target, __extra);
        if (QDELETED(this))
            return TRUE;

        Waiting.NoSleep(empty_pod());
        return TRUE;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:57
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, contents);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, contents);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, contents);
                break;
        }
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:66
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct(FALSE);
        return null;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:71
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (!Dm.truthy(user.incapacitated))
        {
            await empty_pod();
            return null;
        }
        if (!Dm.truthy(moving))
        {
            user.change_next_special_move(CLICK_CD_BREAKOUT);
            to_chat(user, span_notice(I18n.T("You start trying to escape from the pod...")));
            if (Dm.truthy(await do_after(user, 1 * MINUTES, target: this)))
            {
                to_chat(user, span_notice(I18n.T("You manage to open the pod.")));
                await empty_pod();
            }
        }
        return null;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:82
    public virtual async DmTask empty_pod(atom location = null)
    {
        if (!Dm.truthy(location))
            location = get_turf(this);
        foreach (atom_movable M in Dm.each<atom_movable>(contents))
            await M.forceMove(location);
        update_appearance();
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:89
    public virtual async DmTask follow_tube(obj_structure_transit_tube tube = null)
    {
        if (Dm.truthy(moving) || !Dm.truthy(tube.has_exit(dir)))
            return;

        moving = TRUE;
        current_tube = tube;

        datum_move_loop engine = GLOB.move_manager.force_move_dir(this, dir, 0, priority: MOVEMENT_ABOVE_SPACE_PRIORITY) as datum_move_loop;
        RegisterSignal(engine, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(before_pipe_transfer));
        RegisterSignal(engine, COMSIG_MOVELOOP_POSTPROCESS, nameof(after_pipe_transfer));
        RegisterSignal(engine, COMSIG_QDELETING, nameof(engine_finish));
        await calibrate_engine(engine as datum_move_loop_move);
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:102
    [SignalHandler]
    public virtual void before_pipe_transfer(datum_move_loop_move source = null)
    {
        // SIGNAL_HANDLER
        Waiting.NoSleep(setDir(source.direction));
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:106
    [SignalHandler]
    public virtual void after_pipe_transfer(datum_move_loop_move source = null)
    {
        // SIGNAL_HANDLER

        set_density(current_tube.density);
        if (Dm.truthy(current_tube.should_stop_pod(this, source.direction)))
        {
            current_tube.pod_stopped(this, dir);
            qdel(source);
            return;
        }

        Waiting.NoSleep(calibrate_engine(source));
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:117
    public virtual async DmTask calibrate_engine(datum_move_loop_move engine = null)
    {
        object next_dir = current_tube.get_exit(dir);

        if (!Dm.truthy(next_dir))
        {
            qdel(engine);
            return;
        }

        object exit_delay = current_tube.exit_delay__proc(this, dir);
        atom next_loc = get_step(loc, Dm.num(next_dir));

        current_tube = null;
        foreach (obj_structure_transit_tube tube in Dm.each<obj_structure_transit_tube>(next_loc))
        {
            if (Dm.truthy(tube.has_entrance(next_dir)))
            {
                current_tube = tube;
                break;
            }
        }

        if (current_tube == null)
        {
            await setDir(next_dir);
            // Allow collisions when leaving the tubes.
            await Move(get_step(loc, Dm.num(dir)), dir, DELAY_TO_GLIDE_SIZE(Dm.num(exit_delay)));
            qdel(this);
            return;
        }

        object enter_delay = current_tube.enter_delay__proc(this, next_dir);
        engine.direction = next_dir;
        engine.set_delay(Dm.num(enter_delay) + Dm.num(exit_delay));
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:144
    public virtual async DmTask engine_finish()
    {
        set_density(TRUE);
        moving = FALSE;

        obj_structure_transit_tube TT = Dm.locate<obj_structure_transit_tube>(loc);
        //landed on a turf without transit tube or not in our direction
        if (!Dm.truthy(TT) || (!Dm.@in(dir, TT.tube_dirs) && !Dm.@in(REVERSE_DIR(Dm.num(dir)), TT.tube_dirs)))
            await outside_tube();
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:153
    public virtual async DmTask outside_tube()
    {
        DmList savedcontents = contents.Copy();
        object saveddir = dir;
        turf destination = get_edge_target_turf(this, saveddir) as turf;
        visible_message(span_warning(I18n.T("[] ejects its insides out!", this)));
        await deconstruct(FALSE); //we automatically deconstruct the pod
        foreach (object i in Dm.each(savedcontents))
        {
            atom_movable AM = (atom_movable)i;
            await AM.throw_at(destination, rand(1, 3), 5);
        }
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:163
    public override async DmTask<object> return_air()
    {
        return air_contents;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:166
    public override object return_analyzable_air()
    {
        return air_contents;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:169
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        return air_contents.merge(giver);
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:172
    public override object remove_air(object amount = null)
    {
        return air_contents.remove(amount);
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:176
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(user.client) || Dm.truthy(moving))
            return null;

        foreach (obj_structure_transit_tube_station station in Dm.each<obj_structure_transit_tube_station>(loc))
        {
            if (Dm.truthy(station.pod_moving))
                return null;
            if (Dm.eq(direction, REVERSE_DIR(Dm.num(station.boarding_dir))))
            {
                if (station.open_status == STATION_TUBE_OPEN)
                {
                    await user.forceMove(loc);
                    update_appearance();
                }
                else
                    station.open_animation();
            }
            else if (Dm.@in(direction, station.tube_dirs))
            {
                await setDir(direction);
                await station.launch_pod();
            }
            return null;
        }

        foreach (obj_structure_transit_tube transit_tube in Dm.each<obj_structure_transit_tube>(loc))
        {
            if (!Dm.@in(dir, transit_tube.tube_dirs))
                continue;
            if (!Dm.truthy(transit_tube.has_exit(direction)))
                continue;
            await setDir(direction);
            return null;
        }
        return null;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:203
    public override async DmTask<object> return_temperature()
    {
        return air_contents.temperature;
    }
}

//special pod made by the dispenser, it fizzles away when reaching a station.

// tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:209
public partial class obj_structure_transit_tube_pod_dispensed : obj_structure_transit_tube_pod
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube_pod.dm:214
    public override async DmTask outside_tube()
    {
        if (!QDELETED(this))
            qdel(this);
    }
}
