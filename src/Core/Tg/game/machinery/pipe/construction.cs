// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/pipe/construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (pipes): the pipe fitting (/obj/item/pipe and its binary, trinary and quaternary kinds): made from an
// unwrenched atmospherics device (make_from_existing) or by the iron stack's "pipe" recipe (the crafted pipe), turned in hand
// and flipped, set to a piping layer with a right click, wrenched down into the device it stands for (the conflict check and
// the smart pipes' reconfiguration, build_pipe, on_construction joining the pipenet), welded in two. The RPD (M7 slice D172,
// game/objects/items/rcd/RPD.cs) dispenses these fittings; the pipe dispenser machine (pipe_dispenser.dm) is left out. M7 slice 4: the meter fitting (/obj/item/pipe_meter, with the meter machine). Generated live-partial stubs: the Invert Pipe verb (verbs, M9), suicide_act (M10), get_save_vars
// (M10). The examines are translated (X1). The var blocks are generated.

/*CONTENTS
Buildable pipes
Buildable meters
*/

//construction defines are in __defines/pipe_construction.dm
//update those defines ANY TIME an atmos path is changed...
//...otherwise construction will stop working

// tg: code/game/machinery/pipe/construction.dm:10
public partial class obj_item_pipe : obj_item
{
    // tg: code/game/machinery/pipe/construction.dm:34
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        if (current_recipe is not datum_crafting_recipe_spec_pipe)
            return;
        datum_crafting_recipe_spec_pipe pipe_recipe = (datum_crafting_recipe_spec_pipe)current_recipe;
        pipe_type = pipe_recipe.pipe_type;
        pipe_color = ATMOS_COLOR_OMNI;
        await setDir(crafter.dir);
        update();
    }

    // tg: code/game/machinery/pipe/construction.dm:155
    [DmParams("mapload", "_pipe_type", "_dir", "make_from", "device_color", "device_init_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object _pipe_type = Dm.arg(__extra, 0);
        object _dir = Dm.arg(__extra, 1);
        obj_machinery_atmospherics make_from = Dm.arg(__extra, 2) as obj_machinery_atmospherics;
        object device_color = Dm.arg(__extra, 3);
        object device_init_dir = Dm.arg(__extra, 4);
        device_init_dir ??= SOUTH;
        if (Dm.truthy(make_from))
            make_from_existing(make_from);
        else
        {
            p_init_dir = Dm.num(device_init_dir);
            pipe_type = _pipe_type as Type;
            pipe_color = device_color;
            Waiting.NoSleep(setDir(_dir));
        }

        update();
        pixel_x += rand(-5, 5);
        pixel_y += rand(-5, 5);

        //Flipping handled manually due to custom handling for trinary pipes
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_NO_FLIPPING));

        // Only 'normal' pipes
        if (GetType() != typeof(obj_item_pipe_quaternary))
            return base.Initialize(mapload, Dm.forward(__extra, _pipe_type, _dir, make_from, device_color, device_init_dir));

        // tg: code/game/machinery/pipe/construction.dm:174  var/static/list/slapcraft_recipe_list (a type-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_pipe__slapcraft_recipe_list));

        return base.Initialize(mapload, Dm.forward(__extra, _pipe_type, _dir, make_from, device_color, device_init_dir));
    }

    // tg: code/game/machinery/pipe/construction.dm:183
    public virtual void make_from_existing(obj_machinery_atmospherics make_from = null)
    {
        p_init_dir = Dm.num(make_from.get_init_directions());
        Waiting.NoSleep(setDir(make_from.dir));
        pipename = make_from.name;
        Waiting.NoSleep(add_atom_colour(make_from.color, FIXED_COLOUR_PRIORITY));
        pipe_type = make_from.GetType();
        paintable = make_from.paintable;
        pipe_color = make_from.pipe_color;
    }

    // tg: code/game/machinery/pipe/construction.dm:197
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        if (loc != null)
            set_piping_layer(piping_layer);
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/machinery/pipe/construction.dm:202
    public virtual void set_piping_layer(object new_layer = null)
    {
        new_layer ??= PIPING_LAYER_DEFAULT;
        // BYOND reads initial() of a typed var that holds null from its declared type (a fitting made without a pipe_type,
        // tg's create_and_destroy): /obj/machinery/atmospherics
        Type fakeA = pipe_type ?? typeof(obj_machinery_atmospherics);

        if (Dm.truthy(Dm.bitand(Dm.initial(fakeA, (obj_machinery_atmospherics p) => p.pipe_flags), PIPING_ALL_LAYER)))
            new_layer = PIPING_LAYER_DEFAULT;
        piping_layer = Dm.num(new_layer);

        PIPING_LAYER_SHIFT(this, piping_layer);
        layer = Dm.initial(this, (obj_item_pipe p) => p.layer) + ((piping_layer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_LCHANGE);
    }

    // tg: code/game/machinery/pipe/construction.dm:212
    public virtual void update()
    {
        // BYOND reads initial() of a typed var that holds null from its declared type (a fitting made without a pipe_type,
        // tg's create_and_destroy): /obj/machinery/atmospherics
        Type fakeA = pipe_type ?? typeof(obj_machinery_atmospherics);
        name = Dm.text("[] fitting", Dm.initial(fakeA, (obj_machinery_atmospherics p) => p.name));
        desc = Dm.initial(fakeA, (obj_machinery_atmospherics p) => p.desc);
        icon_state = Dm.initial(fakeA, (obj_machinery_atmospherics p) => p.pipe_state) as string;
        if (ispath(pipe_type, typeof(obj_machinery_atmospherics_pipe_heat_exchanging)))
            Dm.combine(ref resistance_flags, Dm.bitor(FIRE_PROOF, LAVA_PROOF));
    }

    // tg: code/game/machinery/pipe/construction.dm:220  GAME_VERB_SRC(/obj/item/pipe, flip, view(1), "Invert Pipe", null)
    public virtual async DmTask<object> __gvb_flip(DmList structured_args = null)
    {
        Dm.NotTranslated("code/game/machinery/pipe/construction.dm:220", "__gvb_flip: the Invert Pipe verb (verbs, M10+)");
        return default;
    }

    // tg: code/game/machinery/pipe/construction.dm:220
    public virtual void flip(params object[] __extra)
    {
        Dm.NotTranslated("code/game/machinery/pipe/construction.dm:220", "flip: the Invert Pipe verb (verbs, M10+)");
    }

    // tg: code/game/machinery/pipe/construction.dm:227
    public virtual void do_a_flip()
    {
        Waiting.NoSleep(setDir(REVERSE_DIR(Dm.num(dir))));
    }

    // tg: code/game/machinery/pipe/construction.dm:234
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object old_dir = dir;
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        await setDir(old_dir); //pipes changing direction when moved is just annoying and buggy
        return dot;
    }

    /// <summary>
    /// tg's conflict check of <c>wrench_act</c> (construction.dm:278-301) on <paramref name="place"/>: tg's warning when this fitting cannot
    /// be wrenched down there (a machine hogging the tile, a pipe on its layer with a direction in common), else null. A smart pipe may
    /// solve a conflict by reconfiguring; <paramref name="dry"/> (DEVIATION(DV352): the RPD's build preview) only asks whether it could.
    /// </summary>
    public string placement_conflict(atom place, mob_living user = null, bool dry = false)
    {
        // BYOND reads initial() of a typed var that holds null from its declared type (a fitting made without a pipe_type,
        // tg's create_and_destroy): /obj/machinery/atmospherics
        Type fakeA = pipe_type ?? typeof(obj_machinery_atmospherics);
        float flags = Dm.initial(fakeA, (obj_machinery_atmospherics p) => p.pipe_flags);
        DmList potentially_conflicting_machines = Dm.list();
        // Work out which machines we would potentially conflict with
        foreach (obj_machinery_atmospherics machine in Dm.each<obj_machinery_atmospherics>(place))
        {
            // Only one dense/requires density object per tile, eg connectors/cryo/heater/coolers.
            if (Dm.truthy(Dm.bitand(Dm.bitand(machine.pipe_flags, flags), PIPING_ONE_PER_TURF)))
            {
                return I18n.T("Something is hogging the tile!");
            }
            // skip checks if we don't overlap layers, either by being on the same layer or by something being on all layers
            if (!Dm.eq(machine.piping_layer, piping_layer) && !Dm.truthy(Dm.bitand(Dm.bitor(machine.pipe_flags, flags), PIPING_ALL_LAYER)))
                continue;
            Dm.append(ref potentially_conflicting_machines, machine);
        }

        // See if we would conflict with any of the potentially interacting machines
        foreach (obj_machinery_atmospherics machine in Dm.each<obj_machinery_atmospherics>(potentially_conflicting_machines))
        {
            // if the pipes have any directions in common, we can't place it that way.
            object our_init_dirs = SSair.get_init_dirs(pipe_type, fixed_dir(), p_init_dir);
            if (Dm.truthy(Dm.bitand(machine.get_init_directions(), our_init_dirs)))
            {
                // We have a conflict!
                if (Dm.length(potentially_conflicting_machines) != 1 || !Dm.truthy(try_smart_reconfiguration(machine, our_init_dirs, user, dry)))
                {
                    // No solutions found
                    return I18n.T("There is already a pipe at that location!");
                }
            }
        }
        return null;
    }

    // Convert dir of fitting into dir of built component
    // tg: code/game/machinery/pipe/construction.dm:240
    public virtual object fixed_dir()
    {
        return dir;
    }

    // tg: code/game/machinery/pipe/construction.dm:255
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await setDir(turn(dir, -90));
        return null;
    }

    ///Check if the pipe on the turf and our to be placed binary pipe are perpendicular to each other
    // tg: code/game/machinery/pipe/construction.dm:259
    public virtual object check_ninety_degree_dir(obj_machinery_atmospherics machine = null)
    {
        if (Dm.truthy(ISDIAGONALDIR(Dm.num(machine.dir))))
            return FALSE;
        if (Dm.truthy(EWCOMPONENT(Dm.num(machine.dir))) && Dm.truthy(EWCOMPONENT(Dm.num(dir))))
            return FALSE;
        if (Dm.truthy(NSCOMPONENT(Dm.num(machine.dir))) && Dm.truthy(NSCOMPONENT(Dm.num(dir))))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/pipe/construction.dm:268
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item wrench = null)
    {
        object dot = await base.wrench_act(user, wrench);
        if (!isturf(loc))
            return TRUE;

        // tg: code/game/machinery/pipe/construction.dm:273
        add_fingerprint(user);

        // DEVIATION(DV352): the conflict check is placement_conflict, which the RPD's build preview runs dry
        if (placement_conflict(loc, user) is string conflict)
        {
            to_chat(user, span_warning(conflict));
            return TRUE;
        }
        // no conflicts found

        obj_machinery_atmospherics built_machine = Dm.@new(pipe_type, loc, null, fixed_dir(), p_init_dir) as obj_machinery_atmospherics;
        build_pipe(built_machine);
        await built_machine.on_construction(user, pipe_color, piping_layer);
        // tg: code/game/machinery/pipe/construction.dm:303
        transfer_fingerprints_to(built_machine);

        wrench.play_tool_sound(this);
        user.visible_message(
            span_notice(I18n.T("[] fastens \\the [].", user, this)),
            span_notice(I18n.T("You fasten \\the [].", this)),
            span_hear(I18n.T("You hear ratcheting.")));

        qdel(this);
        _ = dot;
        return null;
    }

    // tg: code/game/machinery/pipe/construction.dm:314
    public override async DmTask<object> welder_act(mob_living user = null, obj_item welder = null)
    {
        object dot = await base.welder_act(user, welder);
        // tg tests istype(pipe_type, ...), an instance test of a typepath, which is never true (kept)
        if (istype(pipe_type, typeof(obj_machinery_atmospherics_components)))
            return TRUE;
        if (!Dm.truthy(welder.tool_start_check(user, amount: 2)))
            return TRUE;
        // tg: code/game/machinery/pipe/construction.dm:320
        add_fingerprint(user);

        if (Dm.truthy(await welder.use_tool(this, user, 2 * SECONDS, volume: 2)))
        {
            Dm.@new<obj_item_sliced_pipe>(drop_location());
            user.visible_message(
                I18n.T("[] welds \\the [] in two.", user, this),
                span_notice(I18n.T("You weld \\the [] in two.", this)),
                span_hear(I18n.T("You hear welding.")));

            qdel(this);
        }
        return dot;
    }

    /**
     * Attempt to automatically resolve a pipe conflict by reconfiguring any smart pipes involved.
     *
     * Constraints:
     *  - A smart pipe cannot have current connections reconfigured.
     *  - A smart pipe cannot have fewer than two directions in which it will connect.
     *  - A smart pipe, existing or new, will not automatically reconfigure itself to permit directions it was not previously permitting.
     */
    // tg: code/game/machinery/pipe/construction.dm:339
    public virtual object try_smart_reconfiguration(obj_machinery_atmospherics machine = null, object our_init_dirs = null, mob_living user = null, bool dry = false)
    {
        // If we're a smart pipe, we might be able to solve this by placing down a more constrained version of ourselves.
        obj_machinery_atmospherics_pipe_smart other_smart_pipe = machine as obj_machinery_atmospherics_pipe_smart;
        float our_dirs = Dm.num(our_init_dirs);
        if (ispath(pipe_type, typeof(obj_machinery_atmospherics_pipe_smart)))
        {
            // If we're conflicting with another smart pipe, see if we can negotiate.
            if (other_smart_pipe != null)
            {
                // Two smart pipes. This is going to get complicated.
                // Check to see whether the already placed pipe is bent or not.
                if (Dm.truthy(ISDIAGONALDIR(Dm.num(other_smart_pipe.dir))))
                {
                    // The other pipe is bent, with at least two current connections. See if we can bounce off it as a bent pipe in the other direction.
                    float opposing_dir = Dm.bitand(our_dirs, Dm.bitnot(Dm.num(other_smart_pipe.connections)));
                    if (Dm.truthy(ISNOTSTUB__s7(opposing_dir)))
                    {
                        // We only get here if both smart pipes have two directions.
                        if (!dry) p_init_dir = opposing_dir;
                        if (!dry) other_smart_pipe.set_init_directions(other_smart_pipe.connections);
                        if (!dry) other_smart_pipe.update_pipe_icon();
                        return TRUE;
                    }
                    // We're left with one or no available directions if we look at the complement of the other smart pipe's live connections.
                    // There's nothing further we can do.
                    return FALSE;
                }
                else
                {
                    // The other pipe is straight. See if we can go over it in a perpindicular direction.
                    // Note that the other pipe cannot be unconnected, since we have a conflict.
                    if (Dm.truthy(EWCOMPONENT(Dm.num(other_smart_pipe.dir))))
                    {
                        if (Dm.truthy(Dm.bitand(Dm.bitor(NORTH, SOUTH), Dm.bitnot(p_init_dir))))
                            // Not allowed to connect this way
                            return FALSE;
                        if (Dm.truthy(Dm.bitand(Dm.bitnot(Dm.num(other_smart_pipe.get_init_directions())), Dm.bitor(EAST, WEST))))
                            // Not allowed to reconfigure the other pipe this way
                            return FALSE;
                        if (!dry) p_init_dir = Dm.bitor(NORTH, SOUTH);
                        if (!dry) other_smart_pipe.set_init_directions(Dm.bitor(EAST, WEST));
                        if (!dry) other_smart_pipe.update_pipe_icon();
                        return TRUE;
                    }
                    if (Dm.truthy(NSCOMPONENT(Dm.num(other_smart_pipe.dir))))
                    {
                        if (Dm.truthy(Dm.bitand(Dm.bitor(EAST, WEST), Dm.bitnot(p_init_dir))))
                            // Not allowed to connect this way
                            return FALSE;
                        if (Dm.truthy(Dm.bitand(Dm.bitnot(Dm.num(other_smart_pipe.get_init_directions())), Dm.bitor(NORTH, SOUTH))))
                            // Not allowed to reconfigure the other pipe this way
                            return FALSE;
                        if (!dry) p_init_dir = Dm.bitor(EAST, WEST);
                        if (!dry) other_smart_pipe.set_init_directions(Dm.bitor(NORTH, SOUTH));
                        if (!dry) other_smart_pipe.update_pipe_icon();
                        return TRUE;
                    }
                }
                return FALSE;
            }
            // We're not dealing with another smart pipe. See if we can become the complement of the conflicting machine.
            float opposing = Dm.bitand(our_dirs, Dm.bitnot(Dm.num(machine.get_init_directions())));
            if (Dm.truthy(ISNOTSTUB__s7(opposing)))
            {
                // We have at least two permitted directions in the complement. Use them.
                if (!dry) p_init_dir = opposing;
                return TRUE;
            }
            return FALSE;
        }
        else if (other_smart_pipe != null)
        {
            // We're not a smart pipe ourselves, but we are conflicting with a smart pipe. We might be able to solve this by constraining the smart pipe.
            if (Dm.truthy(Dm.bitand(our_dirs, Dm.num(other_smart_pipe.connections))))
                // We needed to go where a smart pipe already had connections, nothing further we can do
                return FALSE;
            float opposing_dir = Dm.bitand(Dm.num(other_smart_pipe.get_init_directions()), Dm.bitnot(our_dirs));
            if (Dm.truthy(ISNOTSTUB__s7(opposing_dir)))
            {
                // At least two directions remain for that smart pipe, reconfigure it
                if (!dry) other_smart_pipe.set_init_directions(opposing_dir);
                if (!dry) other_smart_pipe.update_pipe_icon();
                return TRUE;
            }
            return FALSE;
        }
        // No smart pipes involved, the conflict can't be solved this way.
        return FALSE;
    }

    // tg: code/__DEFINES/atmospherics/atmos_piping.dm:21  #define ISNOTSTUB(bits) ((bits) & ((bits) - 1))
    private static float ISNOTSTUB__s7(float bits) => Dm.bitand(bits, bits - 1);

    // tg: code/game/machinery/pipe/construction.dm:408
    public virtual void build_pipe(obj_machinery_atmospherics A = null)
    {
        if (Dm.truthy(pipename))
            A.name = pipename as string;
        if (Dm.truthy(A.on))
        {
            // Certain pre-mapped subtypes are on by default, we want to preserve
            // every other aspect of these subtypes (name, pre-set filters, etc.)
            // but they shouldn't turn on automatically when wrenched.
            A.on = FALSE;
        }
    }

    // tg: code/game/machinery/pipe/construction.dm:433
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The pipe layer is set to [].", piping_layer)));
        Dm.append(ref dot, span_notice(I18n.T("You can change the pipe layer by Right-Clicking the device.")));
        return dot;
    }

    // tg: code/game/machinery/pipe/construction.dm:438
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        float layer_to_set = (piping_layer >= PIPING_LAYER_MAX) ? PIPING_LAYER_MIN : (piping_layer + 1);
        set_piping_layer(layer_to_set);
        balloon_alert(user, I18n.T("pipe layer set to []", piping_layer));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/game/machinery/pipe/construction.dm:85
public partial class obj_item_pipe_binary : obj_item_pipe
{
    // tg: code/game/machinery/pipe/construction.dm:243
    public override object fixed_dir()
    {
        object dot = dir;
        if (Dm.eq(dir, SOUTH))
            dot = NORTH;
        else if (Dm.eq(dir, WEST))
            dot = EAST;
        return dot;
    }
}

// tg: code/game/machinery/pipe/construction.dm:118
public partial class obj_item_pipe_trinary_flippable : obj_item_pipe_trinary
{
    // tg: code/game/machinery/pipe/construction.dm:192
    public override void make_from_existing(obj_machinery_atmospherics make_from = null)
    {
        base.make_from_existing(make_from);
        if (Dm.truthy(((obj_machinery_atmospherics_components_trinary)make_from).flipped))
            do_a_flip();
    }

    // tg: code/game/machinery/pipe/construction.dm:230
    public override void do_a_flip()
    {
        Waiting.NoSleep(setDir(turn(dir, Dm.truthy(flipped) ? 45 : -45)));
        flipped = Dm.truthy(flipped) ? FALSE : TRUE;
    }

    // tg: code/game/machinery/pipe/construction.dm:250
    public override object fixed_dir()
    {
        object dot = dir;
        if (Dm.truthy(ISDIAGONALDIR(Dm.num(dir))))
            dot = turn(dir, 45);
        return dot;
    }

    // tg: code/game/machinery/pipe/construction.dm:417
    public override void build_pipe(obj_machinery_atmospherics T = null)
    {
        base.build_pipe(T);
        ((obj_machinery_atmospherics_components_trinary)T).flipped = flipped;
    }

    // tg: code/game/machinery/pipe/construction.dm:448
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can flip the device by Right-Clicking it.")));
        return dot;
    }

    // tg: code/game/machinery/pipe/construction.dm:452
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        do_a_flip();
        balloon_alert(user, I18n.T("pipe was flipped"));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/game/machinery/pipe/construction.dm:139
public partial class obj_item_pipe_quaternary_pipe_crafted : obj_item_pipe_quaternary_pipe
{
    // tg: code/game/machinery/pipe/construction.dm:142
    [DmParams("mapload", "_pipe_type", "_dir", "make_from", "device_color", "device_init_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pipe_type = typeof(obj_machinery_atmospherics_pipe_smart);
        pipe_color = ATMOS_COLOR_OMNI;
        p_init_dir = ALL_CARDINALS;
        Waiting.NoSleep(setDir(SOUTH));
        update();
        return dot;
    }
}

// M7 slice 4 (the engine's cooling loop): the meter fitting.
// tg: code/game/machinery/pipe/construction.dm:461
public partial class obj_item_pipe_meter : obj_item
{
    // tg: code/game/machinery/pipe/construction.dm:469
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.wrench_act(user, tool);
        obj_machinery_atmospherics_pipe pipe = null;
        foreach (obj_machinery_atmospherics_pipe P in Dm.each<obj_machinery_atmospherics_pipe>(loc))
        {
            if (Dm.eq(P.piping_layer, piping_layer))
            {
                pipe = P;
                break;
            }
        }
        if (pipe == null)
        {
            to_chat(user, span_warning(I18n.T("You need to fasten it to a pipe!")));
            return TRUE;
        }
        Dm.@new<obj_machinery_meter>(loc, piping_layer);
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You fasten the meter to the pipe.")));
        qdel(this);
        return dot;
    }

    // tg: code/game/machinery/pipe/construction.dm:484
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.screwdriver_act(user, tool);
        if (Dm.truthy(dot))
            return TRUE;

        if (!isturf(loc))
        {
            to_chat(user, span_warning(I18n.T("You need to fasten it to the floor!")));
            return TRUE;
        }

        Dm.@new<obj_machinery_meter_turf>(loc, piping_layer);
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You fasten the meter to \\the [].", loc)));
        qdel(this);
        return dot;
    }

    // tg: code/game/machinery/pipe/construction.dm:498
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        if (loc != null)
            setAttachLayer(piping_layer);
        return dot;
    }

    // tg: code/game/machinery/pipe/construction.dm:503
    public virtual void setAttachLayer(object new_layer = null)
    {
        new_layer ??= PIPING_LAYER_DEFAULT;
        piping_layer = Dm.num(new_layer);
        PIPING_LAYER_DOUBLE_SHIFT(this, piping_layer);
    }
}

public partial class datum_controller_subsystem_air
{
    // tg: code/controllers/subsystem/air.dm:757
    public virtual object get_init_dirs(Type type = null, object dir = null, object init_dir = null)
    {
        if (!Dm.truthy(pipe_init_dirs_cache[type]))
            pipe_init_dirs_cache[type] = Dm.list();

        if (!Dm.truthy(((DmList)pipe_init_dirs_cache[type])[Dm.text("[]", init_dir)]))
            ((DmList)pipe_init_dirs_cache[type])[Dm.text("[]", init_dir)] = Dm.list();

        if (!Dm.truthy(((DmList)((DmList)pipe_init_dirs_cache[type])[Dm.text("[]", init_dir)])[Dm.text("[]", dir)]))
        {
            obj_machinery_atmospherics temp = Dm.@new(type, null, FALSE, dir, init_dir) as obj_machinery_atmospherics;
            ((DmList)((DmList)pipe_init_dirs_cache[type])[Dm.text("[]", init_dir)])[Dm.text("[]", dir)] = temp.get_init_directions();
            qdel(temp);
        }

        return ((DmList)((DmList)pipe_init_dirs_cache[type])[Dm.text("[]", init_dir)])[Dm.text("[]", dir)];
    }
}
