// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/pipe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system): the pipes' init (M3 P4, D115), the network a holder travels through, the expel at a
// break, the welder's deconstruction, the junctions, the trunk to a bin or an outlet, the broken pipe and the rotator.

// Disposal pipes

// tg: code/modules/recycling/disposal/pipe.dm:3
public partial class obj_structure_disposalpipe : obj_structure
{
    // tg: code/modules/recycling/disposal/pipe.dm:33
    [DmParams("mapload", "make_from")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_disposalconstruct make_from = Dm.arg<obj_structure_disposalconstruct>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, make_from));

        if (!QDELETED(make_from))
        {
            Waiting.NoSleep(setDir(make_from.dir));
            Waiting.NoSleep(make_from.forceMove(this));
            stored = make_from;
        }

        if (Dm.truthy(ISDIAGONALDIR(dir))) // Bent pipes already have all the dirs set
            initialize_dirs = NONE;

        if (!Dm.eq(initialize_dirs, DISP_DIR_NONE))
        {
            dpdir = dir;

            if (Dm.truthy(Dm.bitand(initialize_dirs, DISP_DIR_LEFT)))
                Dm.combine(ref dpdir, turn(dir, 90));
            if (Dm.truthy(Dm.bitand(initialize_dirs, DISP_DIR_RIGHT)))
                Dm.combine(ref dpdir, turn(dir, -90));
            if (Dm.truthy(Dm.bitand(initialize_dirs, DISP_DIR_FLIP)))
                Dm.combine(ref dpdir, REVERSE_DIR(dir));
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE));
        if (isturf(loc))
        {
            turf turf_loc = loc as turf;
            turf_loc.add_blueprints_preround(this);
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/pipe.dm:59
    public override object Destroy(float? force = null)
    {
        spawn_pipe = FALSE;
        QDEL_NULL(ref stored);
        return base.Destroy(force);
    }

    /**
     * Expels the pipe's contents.
     *
     * This proc checks through src's contents for holder objects,
     * and then tells each one to empty onto the tile. Called when
     * the pipe is deconstructed or someone struggles out.
     */
    // tg: code/modules/recycling/disposal/pipe.dm:71
    public virtual async DmTask spew_forth()
    {
        foreach (obj_structure_disposalholder holdplease in Dm.each<obj_structure_disposalholder>(contents.Copy()))
        {
            if (!istype(holdplease, typeof(obj_structure_disposalholder)))
                continue;
            holdplease.active = FALSE;
            await expel(holdplease, get_turf(this), 0);
        }
        stored = null; // It gets dumped out in expel()
    }

    // tg: code/modules/recycling/disposal/pipe.dm:79
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != stored || QDELETED(this))
            return dot;
        spawn_pipe = FALSE;
        stored = null;
        if (QDELETED(gone))
            await deconstruct(FALSE); //pipe has broken.
        return dot;
    }

    // returns the direction of the next pipe object, given the entrance dir
    // by default, returns the bitmask of remaining directions
    // tg: code/modules/recycling/disposal/pipe.dm:90
    public virtual object nextdir(obj_structure_disposalholder H = null)
    {
        return Dm.bitand(dpdir, Dm.bitnot(REVERSE_DIR(H.dir)));
    }

    // transfer the holder through this pipe segment
    // overridden for special behaviour
    // tg: code/modules/recycling/disposal/pipe.dm:95
    public virtual async DmTask<object> transfer(obj_structure_disposalholder H = null)
    {
        return await transfer_to_dir(H, nextdir(H));
    }

    // tg: code/modules/recycling/disposal/pipe.dm:98
    public virtual async DmTask<object> transfer_to_dir(obj_structure_disposalholder H = null, object nextdir = null)
    {
        await H.setDir(nextdir);
        turf T = H.nextloc() as turf;
        obj_structure_disposalpipe P = H.findpipe(T) as obj_structure_disposalpipe;

        if (P == null) // if there wasn't a pipe, then they'll be expelled.
            return null;
        // find other holder in next loc, if inactive merge it with current
        obj_structure_disposalholder H2 = Dm.locate<obj_structure_disposalholder>(P);
        if (H2 != null && !Dm.truthy(H2.active))
        {
            if (Dm.truthy(H2.hasmob)) //If it's stopped and there's a mob, add to the pile
            {
                await H2.merge(H);
                return null;
            }
            await H.merge(H2); //Otherwise, we push it along through.
        }
        await H.forceMove(P);
        return P;
    }

    // expel the held objects into a turf
    // called when there is a break in the pipe
    // tg: code/modules/recycling/disposal/pipe.dm:117
    public virtual async DmTask expel(obj_structure_disposalholder H = null, turf T = null, object direction = null)
    {
        if (T == null)
            T = get_turf(this);
        turf target = null;
        float eject_range = 5;
        turf_open_floor floorturf = null;

        if (isfloorturf(T) && Dm.truthy(T.overfloor_placed)) // pop the tile if present
        {
            floorturf = T as turf_open_floor;
            await floorturf.spawn_tile();
            await floorturf.make_plating(TRUE);
        }

        if (Dm.truthy(direction)) // direction is specified
        {
            if (isspaceturf(T)) // if ended in space, then range is unlimited
                target = get_edge_target_turf(T, direction) as turf;
            else // otherwise limit to 10 tiles
                target = get_ranged_target_turf(T, direction, 10) as turf;

            eject_range = 10;
        }
        else if (floorturf != null)
            target = get_offset_target_turf(T, rand(5) - rand(5), rand(5) - rand(5)) as turf;

        playsound(this, Dm.resource("sound/machines/hiss.ogg"), 50, FALSE, FALSE);
        await pipe_eject(H, direction, TRUE, target, eject_range);
        await H.vent_gas(T);
        qdel(H);
    }

    // pipe affected by explosion
    // tg: code/modules/recycling/disposal/pipe.dm:147  (M5x slice 1: explosions)
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        obj_structure_disposalholder H = Dm.locate<obj_structure_disposalholder>(this);
        if (H != null)
            await H.contents_explosion(severity, target);
    }

    //welding tool: unfasten and convert to obj/disposalconstruct
    // tg: code/modules/recycling/disposal/pipe.dm:153
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        obj_item I = tool;
        await base.welder_act(user, I);
        if (!Dm.truthy(can_be_deconstructed(user)))
            return TRUE;

        if (!Dm.truthy(I.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return TRUE;

        to_chat(user, span_notice(I18n.T("You start slicing []...", this)));
        if (Dm.truthy(await I.use_tool(this, user, 30, volume: 50)))
        {
            await deconstruct();
            to_chat(user, span_notice(I18n.T("You slice [].", this)));
        }
        return TRUE;
    }

    //checks if something is blocking the deconstruction (e.g. trunk with a bin still linked to it)
    // tg: code/modules/recycling/disposal/pipe.dm:168
    public virtual object can_be_deconstructed(params object[] __extra)
    {
        return TRUE;
    }

    // called when pipe is cut with welder
    // tg: code/modules/recycling/disposal/pipe.dm:172
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(disassembled))
        {
            if (Dm.truthy(spawn_pipe))
            {
                obj_structure_disposalconstruct construct = stored;
                if (construct == null) // Don't have something? Make one now
                    construct = Dm.@new<obj_structure_disposalconstruct>(this, null, SOUTH, FALSE, this);
                stored = null;
                await construct.forceMove(loc);
                transfer_fingerprints_to(construct);
                await construct.setDir(dir);
                spawn_pipe = FALSE;
            }
        }
        else
        {
            turf location = get_turf(this);
            foreach (object d in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(d, dpdir)))
                {
                    obj_structure_disposalpipe_broken pipe = Dm.@new<obj_structure_disposalpipe_broken>(location);
                    await pipe.setDir(d);
                }
            }
        }
        await spew_forth();
    }
}

// tg: code/modules/recycling/disposal/pipe.dm:191 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_structure_disposalpipe : obj_structure
{
    // tg: code/modules/recycling/disposal/pipe.dm:191
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct();
        return null;
    }
}

// A three-way junction with dir being the dominant direction

// tg: code/modules/recycling/disposal/pipe.dm:203
public partial class obj_structure_disposalpipe_junction : obj_structure_disposalpipe
{
    // next direction to move
    // if coming in from secondary dirs, then next is primary dir
    // if coming in from primary dir, then next is equal chance of other dirs
    // tg: code/modules/recycling/disposal/pipe.dm:212
    public override object nextdir(obj_structure_disposalholder H = null)
    {
        object flipdir = REVERSE_DIR(H.dir);
        if (!Dm.eq(flipdir, dir)) // came from secondary dir, so exit through primary
            return dir;

        else // came from primary, so need to choose a secondary exit
        {
            object mask = Dm.bitand(dpdir, Dm.bitnot(dir)); // get a mask of secondary dirs

            // find one secondary dir in mask
            object secdir = NONE;
            foreach (object D in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(D, mask)))
                {
                    secdir = D;
                    break;
                }
            }

            if (prob(50)) // 50% chance to choose the found secondary dir
                return secdir;
            else // or the other one
                return Dm.bitand(mask, Dm.bitnot(secdir));
        }
    }
}

//a trunk joining to a disposal bin or outlet on the same turf

// tg: code/modules/recycling/disposal/pipe.dm:244
public partial class obj_structure_disposalpipe_trunk : obj_structure_disposalpipe
{
    // tg: code/modules/recycling/disposal/pipe.dm:248
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        getlinked();
        return dot;
    }

    // tg: code/modules/recycling/disposal/pipe.dm:252
    public override object Destroy(float? force = null)
    {
        null_linked_ref_to_us();
        linked = null;
        return base.Destroy(force);
    }

    // tg: code/modules/recycling/disposal/pipe.dm:257
    public virtual void null_linked_ref_to_us()
    {
        if (Dm.truthy(linked))
        {
            if (istype(linked, typeof(obj_structure_disposaloutlet)))
            {
                obj_structure_disposaloutlet D = linked as obj_structure_disposaloutlet;
                D.trunk = null;
            }
            else if (istype(linked, typeof(obj_machinery_disposal)))
            {
                obj_machinery_disposal D = linked as obj_machinery_disposal;
                D.trunk = null;
            }
        }
    }

    // tg: code/modules/recycling/disposal/pipe.dm:266
    public virtual void set_linked(obj to_link = null)
    {
        null_linked_ref_to_us();
        linked = to_link;
    }

    // tg: code/modules/recycling/disposal/pipe.dm:270
    public virtual void getlinked()
    {
        null_linked_ref_to_us();
        linked = null;
        turf T = get_turf(this);
        obj_machinery_disposal D = Dm.locate<obj_machinery_disposal>(T);
        if (Dm.truthy(D))
            set_linked(D);
        obj_structure_disposaloutlet O = Dm.locate<obj_structure_disposaloutlet>(T);
        if (Dm.truthy(O))
            set_linked(O);
    }

    // tg: code/modules/recycling/disposal/pipe.dm:281
    public override object can_be_deconstructed(params object[] __extra)
    {
        mob user = Dm.arg<mob>(__extra, 0);
        if (Dm.truthy(linked))
        {
            to_chat(user, span_warning(I18n.T("You need to deconstruct disposal machinery above this pipe!")));
            return FALSE;
        }
        return TRUE;
    }

    // would transfer to next pipe segment, but we are in a trunk
    // if not entering from disposal bin,
    // transfer to linked object (outlet or bin)
    // tg: code/modules/recycling/disposal/pipe.dm:290
    public override async DmTask<object> transfer(obj_structure_disposalholder H = null)
    {
        if (Dm.eq(H.dir, DOWN)) // we just entered from a disposer
            return await base.transfer(H); // so do base transfer proc
        // otherwise, go to the linked object
        if (Dm.truthy(linked))
        {
            if (linked is obj_structure_disposaloutlet O)
                O.expel(H); // expel at outlet
            else
            {
                obj_machinery_disposal D = linked as obj_machinery_disposal;
                await D.expel(H); // expel at disposal
            }
        }

        // Returning null without expelling holder makes the holder expell itself
        return null;
    }

    // tg: code/modules/recycling/disposal/pipe.dm:306
    public override object nextdir(obj_structure_disposalholder H = null)
    {
        if (Dm.eq(H.dir, DOWN))
            return dir;
        else
            return NONE;
    }
}

// tg: code/modules/recycling/disposal/pipe.dm:323
public partial class obj_structure_disposalpipe_rotator : obj_structure_disposalpipe
{
    // tg: code/modules/recycling/disposal/pipe.dm:329
    public override object nextdir(obj_structure_disposalholder H = null)
    {
        obj_structure_disposalholder holder = H;
        return turn(holder.dir, direction_angle);
    }
}
