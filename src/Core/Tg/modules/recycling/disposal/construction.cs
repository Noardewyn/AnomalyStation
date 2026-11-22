// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system).

// Disposal pipe construction
// This is the pipe that you drag around, not the attached ones.

// tg: code/modules/recycling/disposal/construction.dm:4
public partial class obj_structure_disposalconstruct : obj_structure
{
    // tg: code/modules/recycling/disposal/construction.dm:16
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if ((dot == null))
            return dot;
        set_density(Dm.truthy(anchorvalue) ? Dm.num(Dm.initial(pipe_type, (obj p) => p.density)) : FALSE);
        return dot;
    }

    // tg: code/modules/recycling/disposal/construction.dm:22
    [DmParams("mapload", "_pipe_type", "_dir", "flip", "make_from")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object _pipe_type = Dm.arg<object>(__extra, 0);
        object _dir = Dm.arg<object>(__extra, 1) ?? SOUTH;
        object flip = Dm.arg<object>(__extra, 2) ?? FALSE;
        obj make_from = Dm.arg<obj>(__extra, 3);
        object dot = base.Initialize(mapload, Dm.forward(__extra, _pipe_type, _dir, flip, make_from));
        if (make_from != null)
        {
            pipe_type = make_from.type;
            Waiting.NoSleep(setDir(make_from.dir));
            Waiting.NoSleep(set_anchored(TRUE));
        }
        else
        {
            if (Dm.truthy(_pipe_type))
                pipe_type = (Type)_pipe_type;
            Waiting.NoSleep(setDir(_dir));
        }

        pipename = Dm.initial(pipe_type, (obj p) => p.name);

        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), Dm.named("post_rotation_proccall", nameof(post_rotation))));
        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE));

        // this only gets used by pipes created by RPDs or pipe dispensers
        if (Dm.truthy(flip))
        {
            // Rotate, bypassing simple_rotation for 180 degrees at once
            Waiting.NoSleep(setDir(turn(dir, ROTATION_FLIP)));
            post_rotation(usr, ROTATION_FLIP);
        }

        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/recycling/disposal/construction.dm:48
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object old_dir = dir;
        object dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        await setDir(old_dir); //pipes changing direction when moved is just annoying and buggy
        return dot;
    }

    // tg: code/modules/recycling/disposal/construction.dm:53
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (ispath(pipe_type, typeof(obj_machinery_disposal_bin)))
        {
            // Disposal bins receive special icon treating
            icon_state = Dm.text("[]disposal", Dm.truthy(anchored) ? "con" : null);
            return await base.update_icon_state(__extra);
        }

        icon_state = Dm.text("[][]", Dm.truthy(is_pipe()) ? "con" : null, Dm.initial(pipe_type, (atom p) => p.icon_state));
        return await base.update_icon_state(__extra);
    }

    // Extra layer handling
    // tg: code/modules/recycling/disposal/construction.dm:63
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        if (!Dm.truthy(is_pipe()))
            return dot;

        layer = Dm.truthy(anchored) ? Dm.num(Dm.initial(pipe_type, (atom p) => p.layer)) : Dm.num(Dm.initial(this, nameof(layer)));
        return dot;
    }

    // tg: code/modules/recycling/disposal/construction.dm:70
    public virtual object get_disposal_dir()
    {
        if (!Dm.truthy(is_pipe()))
            return NONE;

        Type temp = pipe_type;
        object initialize_dirs = Dm.initial(temp, (obj_structure_disposalpipe p) => p.initialize_dirs);
        object dpdir = NONE;

        if (Dm.truthy(ISDIAGONALDIR(dir))) // Bent pipes
            return dir;

        if (!Dm.eq(initialize_dirs, DISP_DIR_NONE))
        {
            dpdir = dir;

            if (Dm.truthy(Dm.bitand(initialize_dirs, DISP_DIR_LEFT)))
                dpdir = Dm.bitor(dpdir, turn(dir, 90));
            if (Dm.truthy(Dm.bitand(initialize_dirs, DISP_DIR_RIGHT)))
                dpdir = Dm.bitor(dpdir, turn(dir, -90));
            if (Dm.truthy(Dm.bitand(initialize_dirs, DISP_DIR_FLIP)))
                dpdir = Dm.bitor(dpdir, REVERSE_DIR(dir));
        }
        return dpdir;
    }

    // tg: code/modules/recycling/disposal/construction.dm:91
    public virtual void post_rotation(mob user = null, object degrees = null)
    {
        if (Dm.eq(degrees, ROTATION_FLIP))
        {
            Type temp = pipe_type;
            if (Dm.truthy(Dm.initial(temp, (obj_structure_disposalpipe p) => p.flip_type)))
            {
                if (Dm.truthy(ISDIAGONALDIR(dir))) // Fix RPD-induced diagonal turning
                    Waiting.NoSleep(setDir(turn(dir, 45)));
                pipe_type = Dm.initial(temp, (obj_structure_disposalpipe p) => p.flip_type);
            }
        }
        update_appearance();
    }

    // construction/deconstruction
    // wrench: (un)anchor
    // weldingtool: convert to real pipe
    // tg: code/modules/recycling/disposal/construction.dm:105
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        obj_item I = tool;
        await base.wrench_act(user, I);
        if (Dm.truthy(anchored))
        {
            await set_anchored(FALSE);
            to_chat(user, span_notice(I18n.T("You detach the [] from the underfloor.", pipename)));
        }
        else
        {
            object ispipe = is_pipe(); // Indicates if we should change the level of this pipe

            turf T = get_turf(this);
            if (Dm.num(T.underfloor_accessibility) < UNDERFLOOR_INTERACTABLE && isfloorturf(T))
            {
                obj_item_crowbar held_crowbar = user.is_holding_tool_quality(TOOL_CROWBAR) as obj_item_crowbar;
                if (held_crowbar == null || !Dm.truthy(await T.crowbar_act(user, held_crowbar)))
                {
                    to_chat(user, span_warning(I18n.T("You can only attach the [] if the floor plating is removed!", pipename)));
                    return TRUE;
                }
            }

            if (!Dm.truthy(ispipe) && iswallturf(T))
            {
                to_chat(user, span_warning(I18n.T("You can't build []s on walls, only disposal pipes!", pipename)));
                return TRUE;
            }

            if (Dm.truthy(ispipe))
            {
                object dpdir = get_disposal_dir();
                foreach (obj_structure_disposalpipe CP in Dm.each<obj_structure_disposalpipe>(T.contents.Copy()))
                {
                    object pdir = CP.dpdir;
                    if (istype(CP, typeof(obj_structure_disposalpipe_broken)))
                        pdir = CP.dir;
                    if (Dm.truthy(Dm.bitand(pdir, dpdir)))
                    {
                        if (istype(CP, typeof(obj_structure_disposalpipe_broken)))
                            qdel(CP);
                        else
                        {
                            to_chat(user, span_warning(I18n.T("There is already a disposal pipe at that location!")));
                            return TRUE;
                        }
                    }
                }
            }
            else // Disposal or outlet
            {
                obj_structure_disposalpipe_trunk found_trunk = Dm.locate<obj_structure_disposalpipe_trunk>(T);

                if (found_trunk == null)
                {
                    to_chat(user, span_warning(I18n.T("The [] requires a trunk underneath it in order to work!", pipename)));
                    return TRUE;
                }
            }

            await set_anchored(TRUE);
            to_chat(user, span_notice(I18n.T("You attach the [] to the underfloor.", pipename)));
        }
        I.play_tool_sound(this, 100);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/construction.dm:150
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        obj_item I = tool;
        await base.welder_act(user, I);
        if (Dm.truthy(anchored))
        {
            turf T = get_turf(this);
            if (!Dm.truthy(is_pipe()) && (Dm.locate<obj_machinery_disposal>(T) != null || Dm.locate<obj_structure_disposaloutlet>(T) != null))
            {
                to_chat(user, span_warning(I18n.T("A disposals machine already exists here!")));
                return TRUE;
            }

            if (!Dm.truthy(I.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                return TRUE;

            to_chat(user, span_notice(I18n.T("You start welding the [] in place...", pipename)));
            if (Dm.truthy(await I.use_tool(this, user, 8, volume: 50)))
            {
                to_chat(user, span_notice(I18n.T("The [] has been welded in place.", pipename)));
                obj O = Dm.@new(pipe_type, loc, this) as obj;
                transfer_fingerprints_to(O);
            }
        }
        else
            to_chat(user, span_warning(I18n.T("You need to attach it to the plating first!")));
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/construction.dm:171
    public virtual object is_pipe()
    {
        return ispath(pipe_type, typeof(obj_structure_disposalpipe)) ? TRUE : FALSE;
    }

    //helper proc that makes sure you can place the construct (i.e no dense objects stacking)
    // tg: code/modules/recycling/disposal/construction.dm:175
    public virtual object can_place()
    {
        if (Dm.truthy(is_pipe()))
            return TRUE;

        foreach (obj_structure_disposalconstruct DC in Dm.each<obj_structure_disposalconstruct>(get_turf(this).contents))
        {
            if (DC == this)
                continue;

            if (!Dm.truthy(DC.is_pipe())) //there's already a chute/outlet/bin there
                return FALSE;
        }

        return TRUE;
    }
}
