// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/transit_tubes/station.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Transit tubes (the owner, 2026-10-26): the whole file. The stations open and close (OPEN_DURATION, CLOSE_DURATION), take a mob that
// walks into the open side, launch the pod after their cooldown, stop an arriving one, reverse it at a terminus and equalize its air with the
// tile; the dispensers make a temporary pod around whoever walks in and delete arriving ones. The var blocks are generated.

// A place where tube pods stop, and people can get in or out.
// Mappers: use "Generate Instances from Directions" for this
// one.

// tg: code/game/objects/structures/transit_tubes/station.dm:10
public partial class obj_structure_transit_tube_station : obj_structure_transit_tube
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:1-2  #define OPEN_DURATION 6, #define CLOSE_DURATION 6 (file-local defines, rules 11.4)
    private const float OPEN_DURATION = 6;
    private const float CLOSE_DURATION = 6;

    // tg: code/game/objects/structures/transit_tubes/station.dm:25
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:29
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:33
    public override object should_stop_pod(object pod = null, object from_dir = null)
    {
        return TRUE;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:36
    public override object Bumped(atom_movable AM = null)
    {
        return Waiting.WaitforFalse(Bumped__body(AM, null), null); // the slot's set waitfor = FALSE (_atom.dm:407)
    }

    private async DmTask<object> Bumped__body(atom_movable AM, DotBox<object> dot)
    {
        if (!Dm.truthy(pod_moving) && open_status == STATION_TUBE_OPEN && ismob(AM) && Dm.eq(AM.dir, boarding_dir))
        {
            foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
            {
                if (!Dm.truthy(pod.moving))
                {
                    await AM.forceMove(pod);
                    pod.update_appearance();
                    return null;
                }
            }
        }
        return null;
    }

    //pod insertion
    // tg: code/game/objects/structures/transit_tubes/station.dm:45
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        obj_structure_c_transit_tube_pod R = dropped as obj_structure_c_transit_tube_pod;
        if (R == null || Dm.num(get_dist(user, this)) > 1 || Dm.num(get_dist(this, R)) > 1)
            return null;
        foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
            return null; //no fun allowed
        obj_structure_transit_tube_pod TP = Dm.@new<obj_structure_transit_tube_pod>(loc);
        R.transfer_fingerprints_to(TP);
        TP.add_fingerprint(user);
        await TP.setDir(turn(dir, -90));
        user.visible_message(span_notice(I18n.T("[] inserts [].", user, R)), span_notice(I18n.T("You insert [].", R)));
        qdel(R);
        return null;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:58
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(pod_moving))
        {
            if (Dm.truthy(user.pulling) && isliving(user.pulling))
            {
                if (open_status == STATION_TUBE_OPEN)
                {
                    mob_living GM = user.pulling as mob_living;
                    if (user.grab_state >= GRAB_AGGRESSIVE)
                    {
                        if (Dm.truthy(GM.buckled) || Dm.truthy(GM.has_buckled_mobs()))
                        {
                            to_chat(user, span_warning(I18n.T("[] is attached to something!", GM)));
                            return dot;
                        }
                        foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
                        {
                            pod.visible_message(span_warning(I18n.T("[] starts putting [] into the []!", user, GM, pod)));
                            if (Dm.truthy(await do_after(user, 1.5f * SECONDS, target: this)))
                            {
                                if (open_status == STATION_TUBE_OPEN && Dm.truthy(GM) && user.grab_state >= GRAB_AGGRESSIVE && user.pulling == GM && !Dm.truthy(GM.buckled) && !Dm.truthy(GM.has_buckled_mobs()))
                                {
                                    await GM.Paralyze(100);
                                    Bumped(GM);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
                {
                    if (!Dm.truthy(pod.moving) && Dm.@in(pod.dir, tube_dirs))
                    {
                        if (open_status == STATION_TUBE_CLOSED)
                            open_animation();

                        else if (open_status == STATION_TUBE_OPEN)
                        {
                            if (Dm.truthy(Dm.length(pod.contents)) && user.loc != pod)
                            {
                                user.visible_message(span_notice(I18n.T("[] starts emptying []'s contents onto the floor.", user, pod)), span_notice(I18n.T("You start emptying []'s contents onto the floor...", pod)));
                                if (Dm.truthy(await do_after(user, 1 * SECONDS, target: this))) //So it doesn't default to close_animation() on fail
                                {
                                    if (Dm.truthy(pod) && pod.loc == loc)
                                    {
                                        foreach (atom_movable AM in Dm.each<atom_movable>(pod))
                                            await AM.forceMove(get_turf(user));
                                    }
                                }
                            }

                            else
                                close_animation();
                        }
                    }
                    break;
                }
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:96
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        bool anything_done = false;
        foreach (obj_structure_transit_tube_pod victim in Dm.each<obj_structure_transit_tube_pod>(loc))
        {
            await victim.deconstruct(FALSE); // tg passes user as a second argument, which atom_deconstruct does not read
            anything_done = true;
        }
        to_chat(user, span_notice(anything_done ? I18n.T("You empty \\the [].", this) : I18n.T("\\The [] is already empty!", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:104
    public virtual void open_animation()
    {
        if (open_status == STATION_TUBE_CLOSED)
        {
            icon_state = Dm.text("opening_[]", base_icon_state);
            open_status = STATION_TUBE_OPENING;
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_animation)), OPEN_DURATION, file: "code/game/objects/structures/transit_tubes/station.dm", line: 108);
        }
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:110
    public virtual async DmTask finish_animation()
    {
        switch (open_status)
        {
            case STATION_TUBE_OPENING:
                icon_state = Dm.text("open_[]", base_icon_state);
                open_status = STATION_TUBE_OPEN;
                foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
                {
                    foreach (object thing in Dm.each(pod))
                    {
                        if (ismob(thing))
                        {
                            mob mob_content = (mob)thing;
                            if (Dm.truthy(mob_content.client) && !IS_UNCONSCIOUS(mob_content))
                                continue; // Let the mobs with clients decide what they want to do themselves.
                        }
                        atom_movable movable_content = thing as atom_movable;
                        await movable_content.forceMove(loc); //Everything else is moved out of.
                    }
                }
                break;
            case STATION_TUBE_CLOSING:
                icon_state = Dm.text("closed_[]", base_icon_state);
                open_status = STATION_TUBE_CLOSED;
                break;
        }
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:127
    public virtual void close_animation()
    {
        if (open_status == STATION_TUBE_OPEN)
        {
            icon_state = Dm.text("closing_[]", base_icon_state);
            open_status = STATION_TUBE_CLOSING;
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_animation)), CLOSE_DURATION, file: "code/game/objects/structures/transit_tubes/station.dm", line: 131);
        }
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:133
    public virtual async DmTask<object> launch_pod()
    {
        if (launch_cooldown >= world.time)
            return null;
        foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
        {
            if (!Dm.truthy(pod.moving))
            {
                pod_moving = TRUE;
                close_animation();
                await sleep(CLOSE_DURATION + 2);
                if (open_status == STATION_TUBE_CLOSED && Dm.truthy(pod) && pod.loc == loc)
                    await pod.follow_tube(this);
                pod_moving = FALSE;
                return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:147
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (!Dm.truthy(pod_moving))
            await launch_pod();
        return null;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:151
    public override void pod_stopped(object pod = null, object from_dir = null)
    {
        pod_moving = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_stopped), pod), 0.5f * SECONDS, file: "code/game/objects/structures/transit_tubes/station.dm", line: 153);
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:155
    public virtual void start_stopped(obj_structure_transit_tube_pod pod = null)
    {
        if (QDELETED(pod))
            return;
        if (Dm.truthy(reverse_launch))
            Waiting.NoSleep(pod.setDir(tube_dirs[1])); //turning the pod around for next launch.
        launch_cooldown = world.time + cooldown_delay;
        open_animation();
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_stopped), pod), OPEN_DURATION + 2, file: "code/game/objects/structures/transit_tubes/station.dm", line: 162);
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:164
    public virtual async DmTask finish_stopped(obj_structure_transit_tube_pod pod = null)
    {
        pod_moving = FALSE;
        if (QDELETED(pod))
            return;
        datum_gas_mixture floor_mixture = await loc.return_air() as datum_gas_mixture;
        if (Dm.truthy(pod.air_contents.equalize(floor_mixture))) //equalize the pod's mix with the tile it's on
            air_update_turf(FALSE, FALSE);
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:172
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(EAST, WEST);
                break;
            case SOUTH:
                tube_dirs = Dm.list(EAST, WEST);
                break;
            case EAST:
                tube_dirs = Dm.list(NORTH, SOUTH);
                break;
            case WEST:
                tube_dirs = Dm.list(NORTH, SOUTH);
                break;
        }
        boarding_dir = REVERSE_DIR(Dm.num(dir));
    }
}

// tg: code/game/objects/structures/transit_tubes/station.dm:186
public partial class obj_structure_transit_tube_station_flipped : obj_structure_transit_tube_station
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:190
    public override void init_tube_dirs()
    {
        base.init_tube_dirs();
        boarding_dir = dir;
    }
}

// Stations which will send the tube in the opposite direction after their stop.
// tg: code/game/objects/structures/transit_tubes/station.dm:197
public partial class obj_structure_transit_tube_station_reverse : obj_structure_transit_tube_station
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:202
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(EAST);
                break;
            case SOUTH:
                tube_dirs = Dm.list(WEST);
                break;
            case EAST:
                tube_dirs = Dm.list(SOUTH);
                break;
            case WEST:
                tube_dirs = Dm.list(NORTH);
                break;
        }
        boarding_dir = REVERSE_DIR(Dm.num(dir));
    }
}

// tg: code/game/objects/structures/transit_tubes/station.dm:215
public partial class obj_structure_transit_tube_station_reverse_flipped : obj_structure_transit_tube_station_reverse
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:219
    public override void init_tube_dirs()
    {
        base.init_tube_dirs();
        boarding_dir = dir;
    }
}

//special dispenser station, it creates a pod for you to enter when you bump into it.

// tg: code/game/objects/structures/transit_tubes/station.dm:226
public partial class obj_structure_transit_tube_station_dispenser : obj_structure_transit_tube_station
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:236
    public override void close_animation()
    {
        return;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:239
    public override async DmTask<object> launch_pod()
    {
        foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
        {
            if (!Dm.truthy(pod.moving))
            {
                pod_moving = TRUE;
                await pod.follow_tube(this);
                pod_moving = FALSE;
                return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:248
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("This station will create a pod for you to ride, no need to wait for one.")));
        return dot;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:252
    public override object Bumped(atom_movable AM = null)
    {
        return Waiting.WaitforFalse(Bumped__body(AM, null), null); // the slot's set waitfor = FALSE (_atom.dm:407)
    }

    private async DmTask<object> Bumped__body(atom_movable AM, DotBox<object> dot)
    {
        if (!(AM != null && Dm.eq(AM.dir, boarding_dir)) || Dm.truthy(AM.anchored))
            return null;
        if (!isliving(AM))
        {
            // COOLDOWN_FINISHED(src, freight_output) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            if (!(freight_output <= world.time))
            {
                // COOLDOWN_FINISHED(src, freight_message)
                if (freight_message <= world.time)
                {
                    AM.visible_message(span_notice(I18n.T("Freight pod dispenser is recharging. Please wait.")));
                    // COOLDOWN_START(src, freight_message, 10 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
                    freight_message = world.time + (10 * SECONDS);
                }
                return null;
            }
            // COOLDOWN_START(src, freight_output, 2 SECONDS)
            freight_output = world.time + (2 * SECONDS);
        }

        obj_structure_transit_tube_pod_dispensed pod = Dm.@new<obj_structure_transit_tube_pod_dispensed>(loc);
        AM.visible_message(span_notice(I18n.T("[] forms around [].", pod, AM)), span_notice(I18n.T("[] materializes around you.", pod)));
        playsound(this, Dm.resource("sound/items/weapons/emitter2.ogg"), 50, TRUE);
        await pod.setDir(turn(dir, -90));
        await AM.forceMove(pod);
        pod.update_appearance();
        await launch_pod();
        return null;
    }

    // tg: code/game/objects/structures/transit_tubes/station.dm:271
    public override void pod_stopped(object pod = null, object from_dir = null)
    {
        playsound(this, Dm.resource("sound/machines/ding.ogg"), 50, TRUE);
        qdel(pod as datum);
    }
}

// tg: code/game/objects/structures/transit_tubes/station.dm:276
public partial class obj_structure_transit_tube_station_dispenser_flipped : obj_structure_transit_tube_station_dispenser
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:280
    public override void init_tube_dirs()
    {
        base.init_tube_dirs();
        boarding_dir = dir;
    }
}

// tg: code/game/objects/structures/transit_tubes/station.dm:286
public partial class obj_structure_transit_tube_station_dispenser_reverse : obj_structure_transit_tube_station_dispenser
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:291
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(EAST);
                break;
            case SOUTH:
                tube_dirs = Dm.list(WEST);
                break;
            case EAST:
                tube_dirs = Dm.list(SOUTH);
                break;
            case WEST:
                tube_dirs = Dm.list(NORTH);
                break;
        }
        boarding_dir = REVERSE_DIR(Dm.num(dir));
    }
}

// tg: code/game/objects/structures/transit_tubes/station.dm:304
public partial class obj_structure_transit_tube_station_dispenser_reverse_flipped : obj_structure_transit_tube_station_dispenser_reverse
{
    // tg: code/game/objects/structures/transit_tubes/station.dm:308
    public override void init_tube_dirs()
    {
        base.init_tube_dirs();
        boarding_dir = dir;
    }
}
