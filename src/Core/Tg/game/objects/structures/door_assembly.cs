// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/door_assembly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 6 (airlocks): the airlock assembly's construction steps (anchoring, wiring, the electronics, glass and mineral
// plating, the screwdriver finishing the airlock), their reversals, the overlays and name of each state, the transfer to a
// mineral assembly and the deconstruction. Generated live-partial stubs (docs/ai/specs/live-partial.txt, "M5 slice 6"): examine
// (M9); E13: rcd_vals and rcd_act (the RCD). The integrated circuit shell of finish_door stays Dm.NotTranslated (wiremod, left
// out of M5). The var blocks are generated.

// tg: code/game/objects/structures/door_assembly.dm:1
public partial class obj_structure_door_assembly : obj_structure
{
    // tg: code/game/objects/structures/door_assembly.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_flags = Dm.bitor(obj_flags, Dm.bitor(UNIQUE_RENAME, RENAME_NO_DESC));
        update_appearance();
        update_name();
        return dot;
    }

    // tg: code/game/objects/structures/door_assembly.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_WIRES))
        {
            if (Dm.truthy(anchored))
                Dm.append(ref dot, span_notice(I18n.T("The anchoring bolts are <b>wrenched</b> in place, but the maintenance panel lacks <i>wiring</i>.")));
            else
                Dm.append(ref dot, span_notice(I18n.T("The assembly is <b>welded together</b>, but the anchoring bolts are <i>unwrenched</i>.")));
        }
        else if (Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS))
            Dm.append(ref dot, span_notice(I18n.T("The maintenance panel is <b>wired</b>, but the circuit slot is <i>empty</i>.")));
        else if (Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER))
            Dm.append(ref dot, span_notice(I18n.T("The circuit is <b>connected loosely</b> to its slot, but the maintenance panel is <i>unscrewed and open</i>.")));
        if (!Dm.truthy(mineral) && !Dm.truthy(nomineral) && !Dm.truthy(glass) && !Dm.truthy(noglass))
            Dm.append(ref dot, span_notice(I18n.T("There are <i>empty</i> slots for glass windows and mineral covers.")));
        else if (!Dm.truthy(mineral) && !Dm.truthy(nomineral) && Dm.truthy(glass) && !Dm.truthy(noglass))
            Dm.append(ref dot, span_notice(I18n.T("There are <i>empty</i> slots for mineral covers.")));
        else if (!Dm.truthy(glass) && !Dm.truthy(noglass))
            Dm.append(ref dot, span_notice(I18n.T("There are <i>empty</i> slots for glass windows.")));
        if (Dm.truthy(created_name))
            Dm.append(ref dot, span_notice(I18n.T("There is a small <i>paper</i> placard on the assembly, written on it is '[]'.", created_name)));
        return dot;
    }

    // tg: code/game/objects/structures/door_assembly.dm:86
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(anchored) && Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_WIRES) && istype(tool, typeof(obj_item_stack_cable_coil)))
        {
            if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
                return ITEM_INTERACT_BLOCKING;

            user.visible_message(span_notice(I18n.T("[] wires the airlock assembly.", user)),
                span_notice(I18n.T("You start to wire the airlock assembly...")));
            if (!Dm.truthy(await tool.use_tool(this, user, 40, amount: 1)))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_WIRES))
                return ITEM_INTERACT_BLOCKING;

            state = AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS;
            to_chat(user, span_notice(I18n.T("You wire the airlock assembly.")));
            name = "wired airlock assembly";
            update_name();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS) && istype(tool, typeof(obj_item_electronics_airlock)))
        {
            tool.play_tool_sound(this, 100);
            user.visible_message(span_notice(I18n.T("[] installs the electronics into the airlock assembly.", user)),
                span_notice(I18n.T("You start to install electronics into the airlock assembly...")));
            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You install the airlock electronics.")));
            state = AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER;
            name = "near finished airlock assembly";
            electronics = (obj_item_electronics_airlock)tool;
            update_name();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_sheet)))
        {
            obj_item_stack_sheet sheet = (obj_item_stack_sheet)tool;
            if (!Dm.truthy(glass) && (istype(sheet, typeof(obj_item_stack_sheet_rglass)) || istype(sheet, typeof(obj_item_stack_sheet_glass))))
            {
                if (Dm.truthy(noglass))
                {
                    to_chat(user, span_warning(I18n.T("You cannot add [] to []!", sheet, this)));
                    return ITEM_INTERACT_BLOCKING;
                }
                playsound(this, Dm.resource("sound/items/tools/crowbar.ogg"), 100, TRUE);
                user.visible_message(span_notice(I18n.T("[] adds [] to the airlock assembly.", user, sheet.name)),
                    span_notice(I18n.T("You start to install [] into the airlock assembly...", sheet.name)));
                if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
                    return ITEM_INTERACT_BLOCKING;
                if (Dm.num(sheet.get_amount()) < 1 || Dm.truthy(glass))
                    return ITEM_INTERACT_BLOCKING;
                if (sheet.GetType() == typeof(obj_item_stack_sheet_rglass))
                {
                    to_chat(user, span_notice(I18n.T("You install [] windows into the airlock assembly.", sheet.name)));
                    heat_proof_finished = TRUE; //reinforced glass makes the airlock heat-proof
                    name = "near finished heat-proofed window airlock assembly";
                }
                else
                {
                    to_chat(user, span_notice(I18n.T("You install regular glass windows into the airlock assembly.")));
                    name = "near finished window airlock assembly";
                }
                await sheet.use(1);
                glass = TRUE;
                update_name();
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }

            if (istype(sheet, typeof(obj_item_stack_sheet_mineral)) && Dm.truthy(sheet.construction_path_type))
            {
                if (Dm.truthy(nomineral) || Dm.truthy(mineral))
                {
                    to_chat(user, span_warning(I18n.T("You cannot add [] to []!", sheet, this)));
                    return ITEM_INTERACT_BLOCKING;
                }

                object type_path_extension = sheet.construction_path_type;
                Type mineralassembly = text2path(Dm.text("/obj/structure/door_assembly/door_assembly_[]", type_path_extension));
                if (!ispath(mineralassembly))
                {
                    to_chat(user, span_warning(I18n.T("You cannot add [] to []!", sheet, this)));
                    return ITEM_INTERACT_BLOCKING;
                }

                if (Dm.num(sheet.get_amount()) < 2)
                {
                    to_chat(user, span_warning(I18n.T("You need at least two sheets add a mineral cover!")));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/items/tools/crowbar.ogg"), 100, TRUE);
                user.visible_message(span_notice(I18n.T("[] adds [] to the airlock assembly.", user, sheet.name)),
                    span_notice(I18n.T("You start to install [] into the airlock assembly...", sheet.name)));
                if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)) || Dm.num(sheet.get_amount()) < 2 || Dm.truthy(mineral))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice(I18n.T("You install [] plating into the airlock assembly.", type_path_extension)));
                await sheet.use(2);
                obj_structure_door_assembly replacement_assembly = (obj_structure_door_assembly)Dm.@new(mineralassembly, loc);

                if (Dm.truthy(replacement_assembly.noglass) && Dm.truthy(glass)) //in case the new door doesn't support glass. prevents the new one from reverting to a normal airlock after being constructed.
                {
                    obj_item_stack_sheet dropped_glass;
                    if (Dm.truthy(heat_proof_finished))
                    {
                        dropped_glass = Dm.@new<obj_item_stack_sheet_rglass>(drop_location());
                        heat_proof_finished = FALSE;
                    }
                    else
                        dropped_glass = Dm.@new<obj_item_stack_sheet_glass>(drop_location());
                    glass = FALSE;
                    to_chat(user, span_notice(I18n.T("As you finish, a [] falls out of []'s frame.", dropped_glass.singular_name, replacement_assembly)));
                }

                await transfer_assembly_vars(this, replacement_assembly, TRUE);
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return NONE;
    }

    // tg: code/game/objects/structures/door_assembly.dm:193
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(mineral) && !Dm.truthy(glass) && Dm.truthy(anchored))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(mineral))
        {
            Type mineral_path = text2path(Dm.text("/obj/item/stack/sheet/mineral/[]", mineral));
            user.visible_message(span_notice(I18n.T("[] welds the [] plating off the airlock assembly.", user, mineral)), span_notice(I18n.T("You start to weld the [] plating off the airlock assembly...", mineral)));
            if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You weld the [] plating off.", mineral)));
            Dm.@new(mineral_path, loc, 2);
            obj_structure_door_assembly PA = (obj_structure_door_assembly)Dm.@new(previous_assembly, loc);
            await transfer_assembly_vars(this, PA);
            update_name();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(glass))
        {
            user.visible_message(span_notice(I18n.T("[] welds the glass panel out of the airlock assembly.", user)),
                span_notice(I18n.T("You start to weld the glass panel out of the airlock assembly...")));
            if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You weld the glass panel out.")));
            if (Dm.truthy(heat_proof_finished))
            {
                Dm.@new<obj_item_stack_sheet_rglass>(get_turf(this));
                heat_proof_finished = FALSE;
            }
            else
                Dm.@new<obj_item_stack_sheet_glass>(get_turf(this));
            glass = FALSE;
            update_name();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(anchored))
        {
            user.visible_message(span_warning(I18n.T("[] disassembles the airlock assembly.", user)),
                span_notice(I18n.T("You start to disassemble the airlock assembly...")));
            if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You disassemble the airlock assembly.")));
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        // no return NONE at end because it's not possible we end up here
        return null;
    }

    // tg: code/game/objects/structures/door_assembly.dm:240
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
        {
            user.visible_message(span_notice(I18n.T("[] unsecures the airlock assembly from the floor.", user)),
                span_notice(I18n.T("You start to unsecure the airlock assembly from the floor...")),
                span_hear(I18n.T("You hear wrenching.")));
            if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 100)))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(anchored))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You unsecure the airlock assembly.")));
            name = "airlock assembly";
            await set_anchored(FALSE);
            update_name();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        float door_check = FALSE;
        foreach (obj_machinery_door competitor in Dm.each<obj_machinery_door>(loc))
        {
            if (!Dm.truthy(competitor.sub_door))
            {
                door_check = TRUE;
                break;
            }
        }

        if (Dm.truthy(door_check))
        {
            to_chat(user, I18n.T("There is another door here!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] secures the airlock assembly to the floor.", user)),
            span_notice(I18n.T("You start to secure the airlock assembly to the floor...")),
            span_hear(I18n.T("You hear wrenching.")));

        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 100)))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(anchored))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You secure the airlock assembly.")));
        name = "secured airlock assembly";
        await set_anchored(TRUE);
        update_name();
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/door_assembly.dm:285
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        user.visible_message(span_notice(I18n.T("[] cuts the wires from the airlock assembly.", user)),
            span_notice(I18n.T("You start to cut the wires from the airlock assembly...")));

        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 100)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You cut the wires from the airlock assembly.")));
        Dm.@new<obj_item_stack_cable_coil>(get_turf(user), 1);
        state = AIRLOCK_ASSEMBLY_NEEDS_WIRES;
        name = "secured airlock assembly";
        update_name();
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/door_assembly.dm:303
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        user.visible_message(span_notice(I18n.T("[] removes the electronics from the airlock assembly.", user)),
            span_notice(I18n.T("You start to remove electronics from the airlock assembly...")));

        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 100)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You remove the airlock electronics.")));
        state = AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS;
        name = "wired airlock assembly";
        obj_item_electronics_airlock ae;
        if (electronics == null)
            ae = Dm.@new<obj_item_electronics_airlock>(loc);
        else
        {
            ae = electronics;
            electronics = null;
            await ae.forceMove(loc);
        }
        update_name();
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/door_assembly.dm:329
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        user.visible_message(span_notice(I18n.T("[] finishes the airlock.", user)),
            span_notice(I18n.T("You start finishing the airlock...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 100)))
            return ITEM_INTERACT_BLOCKING;

        if (loc == null || !Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You finish the airlock.")));
        await finish_door();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/door_assembly.dm:344
    public virtual async DmTask<obj_machinery_door_airlock> finish_door()
    {
        obj_machinery_door_airlock door;
        if (Dm.truthy(glass))
            door = (obj_machinery_door_airlock)Dm.@new(glass_type, loc);
        else
            door = (obj_machinery_door_airlock)Dm.@new(airlock_type, loc);
        await door.setDir(dir);
        door.unres_sides = electronics.unres_sides;
        door.electronics = electronics;
        door.heat_proof = heat_proof_finished;
        door.security_level = 0;
        if (Dm.truthy(electronics.shell))
        {
            // door.AddComponent(/datum/component/shell, unremovable_circuit_components = list(new /obj/item/circuit_component/airlock,
            // new /obj/item/circuit_component/airlock_access_event, new /obj/item/circuit_component/remotecam/airlock),
            // capacity = SHELL_CAPACITY_LARGE, shell_flags = SHELL_FLAG_ALLOW_FAILURE_ACTION|SHELL_FLAG_REQUIRE_ANCHOR)
            Dm.NotTranslated("code/game/objects/structures/door_assembly.dm:356", "finish_door: the integrated circuit shell (circuits and wiremod, left out of M5; M10+)");
        }
        if (Dm.truthy(electronics.one_access))
            door.req_one_access = electronics.accesses;
        else
            door.req_access = electronics.accesses;
        if (Dm.truthy(created_name))
            door.name = (string)created_name;
        else if (Dm.truthy(electronics.passed_name))
            door.name = (string)sanitize(electronics.passed_name);
        else
            door.name = base_name;
        if (Dm.truthy(electronics.passed_cycle_id))
        {
            door.closeOtherId = electronics.passed_cycle_id;
            door.update_other_id();
        }
        if (Dm.truthy(door.unres_sides))
            door.unres_latch = TRUE;
        door.previous_airlock = previous_assembly;
        await electronics.forceMove(door);
        door.autoclose = TRUE;
        await door.close();
        door.update_appearance();

        qdel(this);
        return door;
    }

    // tg: code/game/objects/structures/door_assembly.dm:386
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(glass))
            Dm.append(ref dot, get_airlock_overlay("fill_construction", icon, this, TRUE));
        else
            Dm.append(ref dot, get_airlock_overlay("glass_construction", overlays_file, this, TRUE));
        Dm.append(ref dot, get_airlock_overlay(Dm.text("panel_c[]", Dm.num(state) + 1), overlays_file, this, TRUE));
        return dot;
    }

    // tg: code/game/objects/structures/door_assembly.dm:394
    public override object update_name(float? updates = null)
    {
        name = "";
        if (Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_WIRES))
        {
            if (Dm.truthy(anchored))
                name = "secured ";
        }
        else if (Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS))
            name = "wired ";
        else if (Dm.eq(state, AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER))
            name = "near finished ";
        name += Dm.text("[][][] assembly", Dm.truthy(heat_proof_finished) ? "heat-proofed " : "", Dm.truthy(glass) ? "window " : "", base_name);
        return base.update_name(updates);
    }

    // tg: code/game/objects/structures/door_assembly.dm:407
    public virtual async DmTask transfer_assembly_vars(obj_structure_door_assembly source = null, obj_structure_door_assembly target = null, float? previous = null)
    {
        previous ??= FALSE;
        target.glass = source.glass;
        target.heat_proof_finished = source.heat_proof_finished;
        target.created_name = source.created_name;
        target.state = source.state;
        await target.set_anchored(source.anchored);
        if (Dm.truthy(previous))
            target.previous_assembly = source.GetType();
        if (electronics != null)
        {
            target.electronics = source.electronics;
            await source.electronics.forceMove(target);
        }
        target.update_appearance();
        target.update_name();
        qdel(source);
    }

    // tg: code/game/objects/structures/door_assembly.dm:422
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        turf target_turf = get_turf(this);
        if (!Dm.truthy(disassembled))
            material_amt = rand(2, 4);
        Dm.@new(material_type, target_turf, material_amt);
        if (Dm.truthy(glass))
        {
            if (Dm.truthy(disassembled))
            {
                if (Dm.truthy(heat_proof_finished))
                    Dm.@new<obj_item_stack_sheet_rglass>(target_turf);
                else
                    Dm.@new<obj_item_stack_sheet_glass>(target_turf);
            }
            else
                Dm.@new<obj_item_shard>(target_turf);
        }
        if (Dm.truthy(mineral))
        {
            Type mineral_path = text2path(Dm.text("/obj/item/stack/sheet/mineral/[]", mineral));
            Dm.@new(mineral_path, target_turf, 2);
        }
    }

    // tg: code/game/objects/structures/door_assembly.dm:450
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        created_name = input;
        return input;
    }

    // tg: code/game/objects/structures/door_assembly.dm:454
    public override async DmTask rename_reset()
    {
        created_name = null;
    }
}

// tg: code/game/objects/structures/door_assembly.dm:37
public partial class obj_structure_door_assembly_multi_tile : obj_structure_door_assembly
{
    // tg: code/game/objects/structures/door_assembly.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_bounds();
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/structures/door_assembly.dm:61
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        set_bounds();
        return dot;
    }
}

// tg: code/game/objects/structures/door_assembly.dm:439 (E13: the RCD)
public partial class obj_structure_door_assembly : obj_structure
{
    // tg: code/game/objects/structures/door_assembly.dm:439
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 16));
        return FALSE;
    }

    // tg: code/game/objects/structures/door_assembly.dm:444
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }
}
