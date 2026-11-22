// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/windoor_assembly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 6 (airlocks): the windoor assembly a windoor's crowbar leaves (windowdoor.dm:407) and the "windoor frame" of the
// rglass recipes builds: its face blocking and atmos, the plasteel, cable and electronics steps, the wrench (and its flip),
// welder, wirecutter, screwdriver and crowbar acts, finish_door and the renaming. Generated live-partial stub
// (docs/ai/specs/live-partial.txt, "M5 slice 6"): examine (M9). The var block is generated.

// tg: code/game/objects/structures/windoor_assembly.dm:4
public partial class obj_structure_windoor_assembly : obj_structure
{
    // tg: code/game/objects/structures/windoor_assembly.dm:1  #define WINDOOR_LEFT "l" (file-local, #undef at :410)
    private const string WINDOOR_LEFT = "l";
    // tg: code/game/objects/structures/windoor_assembly.dm:2  #define WINDOOR_RIGHT "r" (file-local, #undef at :411)
    private const string WINDOOR_RIGHT = "r";

    // tg: code/game/objects/structures/windoor_assembly.dm:43
    [DmParams("mapload", "set_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object set_dir = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, set_dir));
        if (Dm.truthy(set_dir))
            Waiting.NoSleep(setDir(set_dir));
        air_update_turf(TRUE, TRUE);

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_NEEDS_ROOM));
        return dot;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:48  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_windoor_assembly__Initialize__loc_connections;

    // tg: code/game/objects/structures/windoor_assembly.dm:55
    public override object Destroy(float? force = null)
    {
        set_density(FALSE);
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:60
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf T = loc as turf;
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        move_update_air(T);
        return dot;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:65
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]windoor_assembly[]", facing, Dm.truthy(secure) ? "secure_" : "", Dm.truthy(cables_added) ? "02" : "01");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:69
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);

        if (Dm.eq(border_dir, dir))
            return dot;

        if (mover is obj_structure_window)
        {
            obj_structure_window moved_window = mover as obj_structure_window;
            return valid_build_direction(loc as turf, moved_window.dir, is_fulltile: moved_window.fulltile);
        }

        if (mover is obj_structure_windoor_assembly || mover is obj_machinery_door_window)
            return valid_build_direction(loc as turf, mover.dir, is_fulltile: FALSE);
        return dot;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:82
    public override object can_atmos_pass__proc(turf target_turf = null, float? vertical = null)
    {
        vertical ??= FALSE;
        if (Dm.eq(get_dir(loc as atom, target_turf), dir))
            return Dm.num(!Dm.truthy(density));
        else
            return TRUE;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:88
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand(leaving.movement_type, PHASING)))
            return null;

        if (leaving == this)
            return null; // Let's not block ourselves.

        if (Dm.truthy(Dm.bitand(leaving.pass_flags, pass_flags_self)))
            return null;

        if (Dm.eq(direction, dir) && Dm.truthy(density))
        {
            Waiting.NoSleep(leaving.Bump(this));
            return COMPONENT_ATOM_BLOCK_EXIT;
        }
        return null;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:104
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        //I really should have spread this out across more states but thin little windoors are hard to sprite.
        // tg: code/game/objects/structures/windoor_assembly.dm:106
        add_fingerprint(user);
        if (!Dm.truthy(cables_added))
        {
            //Adding plasteel makes the assembly a secure windoor assembly. Step 2 (optional) complete.
            if (istype(tool, typeof(obj_item_stack_sheet_plasteel)) && !Dm.truthy(secure))
            {
                obj_item_stack_sheet_plasteel reinforcement = (obj_item_stack_sheet_plasteel)tool;
                if (Dm.num(reinforcement.get_amount()) < 2)
                {
                    to_chat(user, span_warning(I18n.T("You need more plasteel to do this!")));
                    return ITEM_INTERACT_BLOCKING;
                }

                to_chat(user, span_notice(I18n.T("You start to reinforce the windoor with plasteel...")));

                if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
                    return ITEM_INTERACT_BLOCKING;

                if (QDELETED(this) || Dm.truthy(secure) || Dm.num(reinforcement.get_amount()) < 2)
                    return ITEM_INTERACT_BLOCKING;

                await reinforcement.use(2);
                to_chat(user, span_notice(I18n.T("You reinforce the windoor.")));
                secure = TRUE;
                if (Dm.truthy(anchored))
                    name = "secure anchored windoor assembly";
                else
                    name = "secure windoor assembly";
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }

            //Adding cable to the assembly. Step 5 complete.
            if (istype(tool, typeof(obj_item_stack_cable_coil)) && Dm.truthy(anchored))
            {
                user.visible_message(span_notice(I18n.T("[] wires the windoor assembly.", user)), span_notice(I18n.T("You start to wire the windoor assembly...")));

                if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
                    return ITEM_INTERACT_BLOCKING;

                if (!Dm.truthy(anchored) || Dm.truthy(cables_added))
                    return ITEM_INTERACT_BLOCKING;

                obj_item_stack_cable_coil wiring = (obj_item_stack_cable_coil)tool;
                if (!Dm.truthy(await wiring.use(1)))
                {
                    to_chat(user, span_warning(I18n.T("You need more cable to do this!")));
                    return ITEM_INTERACT_BLOCKING;
                }

                to_chat(user, span_notice(I18n.T("You wire the windoor.")));
                cables_added = TRUE;
                if (Dm.truthy(secure))
                    name = "secure wired windoor assembly";
                else
                    name = "wired windoor assembly";
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }

            return NONE;
        }

        //cables_added TRUE beyond this point

        //Adding airlock electronics for access. Step 6 complete.
        if (istype(tool, typeof(obj_item_electronics_airlock)))
        {

            tool.play_tool_sound(this, 100);
            user.visible_message(span_notice(I18n.T("[] installs the electronics into the airlock assembly.", user)),
                span_notice(I18n.T("You start to install electronics into the airlock assembly...")));

            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            if (QDELETED(this) || electronics != null)
            {
                await tool.forceMove(drop_location() as atom);
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You install the airlock electronics.")));
            name = "near finished windoor assembly";
            electronics = (obj_item_electronics_airlock)tool;
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    //dissasemble entirely unworked assembly
    // tg: code/game/objects/structures/windoor_assembly.dm:181
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(cables_added))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (!Dm.truthy(anchored))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(span_notice(I18n.T("[] disassembles the windoor assembly.", user)),
            span_notice(I18n.T("You start to disassemble the windoor assembly...")));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You disassemble the windoor assembly.")));
        obj_item_stack_sheet_rglass dropped_glass = Dm.@new<obj_item_stack_sheet_rglass>(get_turf(this), 5);
        if (!QDELETED(dropped_glass))
            // tg: code/game/objects/structures/windoor_assembly.dm:199
            dropped_glass.add_fingerprint(user);
        if (Dm.truthy(secure))
        {
            obj_item_stack_rods dropped_rods = Dm.@new<obj_item_stack_rods>(get_turf(this), 4);
            if (!QDELETED(dropped_rods))
                // tg: code/game/objects/structures/windoor_assembly.dm:203
                dropped_rods.add_fingerprint(user);
        }
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    //secure or unsecure unworked assembly
    // tg: code/game/objects/structures/windoor_assembly.dm:208
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(cables_added))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (!Dm.truthy(anchored))
        {
            foreach (obj_machinery_door_window competitor in Dm.each<obj_machinery_door_window>(loc))
            {
                if (Dm.eq(competitor.dir, dir))
                {
                    to_chat(user, span_warning(I18n.T("There is already a windoor in that location!")));
                    return ITEM_INTERACT_BLOCKING;
                }
            }

            user.visible_message(span_notice(I18n.T("[] secures the windoor assembly to the floor.", user)),
                span_notice(I18n.T("You start to secure the windoor assembly to the floor...")));
            if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 100)))
                return ITEM_INTERACT_BLOCKING;

            if (Dm.truthy(anchored))
                return ITEM_INTERACT_BLOCKING;

            foreach (obj_machinery_door_window competitor in Dm.each<obj_machinery_door_window>(loc))
            {
                if (Dm.eq(competitor.dir, dir))
                {
                    to_chat(user, span_warning(I18n.T("There is already a windoor in that location!")));
                    return ITEM_INTERACT_BLOCKING;
                }
            }

            to_chat(user, span_notice(I18n.T("You secure the windoor assembly.")));
            await set_anchored(TRUE);
            if (Dm.truthy(secure))
                name = "secure anchored windoor assembly";
            else
                name = "anchored windoor assembly";
            return ITEM_INTERACT_SUCCESS;
        }

        //Unwrenching an unsecure assembly un-anchors it. Step 4 undone
        user.visible_message(span_notice(I18n.T("[] unsecures the windoor assembly to the floor.", user)),
            span_notice(I18n.T("You start to unsecure the windoor assembly to the floor...")));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 100)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(anchored))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You unsecure the windoor assembly.")));
        await set_anchored(FALSE);
        if (Dm.truthy(secure))
            name = "secure windoor assembly";
        else
            name = "windoor assembly";
        return ITEM_INTERACT_SUCCESS;
    }

    //Flips the windoor assembly, determines whether the door opens to the left or the right
    // tg: code/game/objects/structures/windoor_assembly.dm:252
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
            return NONE;
        if (facing == WINDOOR_LEFT)
        {
            to_chat(usr, span_notice(I18n.T("The windoor will now slide to the right.")));
            facing = WINDOOR_RIGHT;
        }
        else
        {
            facing = WINDOOR_LEFT;
            to_chat(usr, span_notice(I18n.T("The windoor will now slide to the left.")));
        }

        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    //remove cabling
    // tg: code/game/objects/structures/windoor_assembly.dm:266
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(cables_added))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        user.visible_message(span_notice(I18n.T("[] cuts the wires from the airlock assembly.", user)),
            span_notice(I18n.T("You start to cut the wires from airlock assembly...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 100)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(cables_added))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You cut the windoor wires.")));
        Dm.@new<obj_item_stack_cable_coil>(get_turf(user), 1);
        cables_added = FALSE;
        if (Dm.truthy(secure))
            name = "secure anchored windoor assembly";
        else
            name = "anchored windoor assembly";
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    //remove airlock electronics
    // tg: code/game/objects/structures/windoor_assembly.dm:289
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(cables_added))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (electronics == null)
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        user.visible_message(span_notice(I18n.T("[] removes the electronics from the airlock assembly.", user)),
            span_notice(I18n.T("You start to uninstall electronics from the airlock assembly...")));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 100)) && electronics != null)
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You remove the airlock electronics.")));
        name = "wired windoor assembly";
        obj_item_electronics_airlock scrap;
        scrap = electronics;
        electronics = null;
        await scrap.forceMove(drop_location() as atom);
        return ITEM_INTERACT_SUCCESS;
    }

    //finishes door
    // tg: code/game/objects/structures/windoor_assembly.dm:310
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(cables_added))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (electronics == null)
        {
            to_chat(usr, span_warning(I18n.T("The assembly is missing electronics!")));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] pries the windoor into the frame.", user)),
            span_notice(I18n.T("You start prying the windoor into the frame...")));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 100)) || electronics == null)
            return ITEM_INTERACT_BLOCKING;
        set_density(TRUE); //Shouldn't matter but just incase <-- in case what?
        to_chat(user, span_notice(I18n.T("You finish the windoor.")));
        await finish_door();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:338
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(anchored))
        {
            Dm.append(ref dot, span_notice(I18n.T("\\The [] can be [] down.", this, span_boldnotice(I18n.T("wrenched")))));
            Dm.append(ref dot, span_notice(I18n.T("\\The [] could also be [] with a [].", this, span_boldnotice(I18n.T("cut apart")), span_boldnotice(I18n.T("welder")))));
            return dot;
        }

        if (!Dm.truthy(cables_added))
        {
            Dm.append(ref dot, span_notice(I18n.T("\\The [] needs [], or could be [] from the floor.", this, span_boldnotice(I18n.T("wiring")), span_boldnotice(I18n.T("un-wrenched")))));
            return dot;
        }

        if (electronics == null)
        {
            Dm.append(ref dot, span_notice(I18n.T("\\The [] needs [] to continue installation, or [] to take apart.", this, span_boldnotice(I18n.T("airlock electronics")), span_boldnotice(I18n.T("wirecutters")))));
            return dot;
        }

        Dm.append(ref dot, span_notice(I18n.T("\\The [] is ready to be [] into place with a [].", this, span_boldnotice(I18n.T("levered")), span_boldnotice(I18n.T("crowbar")))));
        return dot;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:347
    public virtual async DmTask finish_door()
    {
        obj_machinery_door_window windoor;
        if (Dm.truthy(secure))
        {
            windoor = Dm.@new<obj_machinery_door_window_brigdoor>(loc);
            if (facing == WINDOOR_LEFT)
            {
                windoor.icon_state = "leftsecureopen";
                windoor.base_state = "leftsecure";
            }
            else
            {
                windoor.icon_state = "rightsecureopen";
                windoor.base_state = "rightsecure";
            }

        }
        else
        {
            windoor = Dm.@new<obj_machinery_door_window>(loc);
            if (facing == WINDOOR_LEFT)
            {
                windoor.icon_state = "leftopen";
                windoor.base_state = "left";
            }
            else
            {
                windoor.icon_state = "rightopen";
                windoor.base_state = "right";
            }
        }

        await windoor.setDir(dir);
        windoor.set_density(FALSE);
        if (Dm.truthy(created_name))
            windoor.name = (string)created_name;
        else if (Dm.truthy(electronics.passed_name))
            windoor.name = (string)sanitize(electronics.passed_name);
        if (Dm.truthy(electronics.one_access))
            windoor.req_one_access = electronics.accesses;
        else
            windoor.req_access = electronics.accesses;
        if (Dm.truthy(electronics.unres_sides))
        {
            windoor.unres_sides = electronics.unres_sides;
            if (Dm.eq(dir, NORTH) || Dm.eq(dir, SOUTH))
            {
                windoor.unres_sides = Dm.num(Dm.bitand(windoor.unres_sides, Dm.bitnot(EAST)));
                windoor.unres_sides = Dm.num(Dm.bitand(windoor.unres_sides, Dm.bitnot(WEST)));
            }
            else if (Dm.eq(dir, EAST) || Dm.eq(dir, WEST))
            {
                windoor.unres_sides = Dm.num(Dm.bitand(windoor.unres_sides, Dm.bitnot(NORTH)));
                windoor.unres_sides = Dm.num(Dm.bitand(windoor.unres_sides, Dm.bitnot(SOUTH)));
            }
            windoor.unres_latch = TRUE;
        }
        await electronics.forceMove(windoor);
        windoor.electronics = electronics;
        windoor.autoclose = TRUE;
        await windoor.close();
        windoor.update_appearance();

        qdel(this);
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:403
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        created_name = input;
        return input;
    }

    // tg: code/game/objects/structures/windoor_assembly.dm:407
    public override async DmTask rename_reset()
    {
        created_name = Dm.initial(this, (obj_structure_windoor_assembly p) => p.created_name);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/windoor_assembly.dm:48  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_structure_windoor_assembly__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_structure_windoor_assembly.on_exit)));
}
