// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/machine_redemption.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2a (D304): the whole file; the alloys (the smelter's auto-unlocking techweb, SSresearch's designs) since silicons SI3
// (D326). The emissive overlays are DV12's. The var block is generated (D111).

/**********************Ore Redemption Unit (ORM)**************************/
//Turns all the various mining machines into a single unit to speed up mining and establish a point system

// tg: code/modules/mining/machine_redemption.dm:4
public partial class obj_machinery_mineral_ore_redemption : obj_machinery_mineral
{
    /// Details how many credits each smelted ore is worth
    // tg: code/modules/mining/machine_redemption.dm:28  var/static/list/ore_values
    private static DmList ore_values => TgState.Current.obj_machinery_mineral_ore_redemption__ore_values ??= Dm.list(
        Dm.kv(typeof(datum_material_iron), 1),
        Dm.kv(typeof(datum_material_glass), 1),
        Dm.kv(typeof(datum_material_plasma), 15),
        Dm.kv(typeof(datum_material_silver), 16),
        Dm.kv(typeof(datum_material_gold), 18),
        Dm.kv(typeof(datum_material_titanium), 30),
        Dm.kv(typeof(datum_material_uranium), 30),
        Dm.kv(typeof(datum_material_diamond), 50),
        Dm.kv(typeof(datum_material_bluespace), 50),
        Dm.kv(typeof(datum_material_bananium), 60));

    // tg: code/modules/mining/machine_redemption.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_smelter)] ??= Dm.@new<datum_techweb_autounlocking_smelter>();
        stored_research = GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_smelter)] as datum_techweb;

        //mat_container_signals is for reedeming points from local storage if silo is not required
        DmList local_signals = null;
        if (!Dm.truthy(requires_silo))
            local_signals = Dm.list(Dm.kv(COMSIG_MATCONTAINER_ITEM_CONSUMED, "/obj/machinery/mineral/ore_redemption/proc/" + nameof(local_redeem_points)));
        materials = Dm.@new<datum_remote_materials>(
            this,
            mapload,
            Dm.named("mat_container_signals", local_signals));

        //for reedeming points from items inserted into ore silo
        RegisterSignal(this, COMSIG_SILO_ITEM_CONSUMED, "/obj/machinery/mineral/ore_redemption/proc/" + nameof(silo_redeem_points));
        return dot;
    }

    // tg: code/modules/mining/machine_redemption.dm:71
    public override object Destroy(float? force = null)
    {
        stored_research = null;
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/machine_redemption.dm:76
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(panel_open))
            dot.Add(span_notice("Alt-click to rotate the input and output direction."));
        return dot;
    }

    // tg: code/modules/mining/machine_redemption.dm:82
    public virtual void silo_redeem_points(obj_machinery_mineral_ore_redemption machine = null, object container = null, obj_item_stack_ore gathered_ore = null)
    {
        // SIGNAL_HANDLER
        local_redeem_points(container, gathered_ore);
    }

    // tg: code/modules/mining/machine_redemption.dm:87
    public virtual void local_redeem_points(object container = null, obj_item_stack_ore gathered_ore = null)
    {
        // SIGNAL_HANDLER
        if (istype(gathered_ore, typeof(obj_item_stack_ore)) && gathered_ore.refined_type != null)
            points += gathered_ore.points * point_upgrade * Dm.num(gathered_ore.amount);
    }

    /// Returns the amount of a specific alloy design, based on the accessible materials
    // tg: code/modules/mining/machine_redemption.dm:94
    public virtual object can_smelt_alloy(datum_design design = null)
    {
        datum_material_container mat_container = materials.mat_container;
        if (mat_container == null || Dm.truthy(design.make_reagent))
            return FALSE;

        float build_amount = 0;

        foreach (object mat in Dm.each<object>(design.materials))
        {
            object amount = design.materials[mat];
            object redemption_mat_amount = mat_container.materials[mat];

            if (!Dm.truthy(amount) || !Dm.truthy(redemption_mat_amount))
                return FALSE;

            float smeltable_sheets = floor(Dm.num(redemption_mat_amount) / Dm.num(amount));

            if (!Dm.truthy(smeltable_sheets))
                return FALSE;

            if (!Dm.truthy(build_amount))
                build_amount = smeltable_sheets;

            build_amount = min(build_amount, smeltable_sheets);
        }

        return build_amount;
    }

    /// Sends a message to the request consoles that signed up for ore updates
    // tg: code/modules/mining/machine_redemption.dm:120
    public virtual void send_console_message()
    {
        datum_material_container mat_container = materials.mat_container;
        if (mat_container == null || !Dm.truthy(is_station_level(z)))
            return;

        console_notify_timer = null;

        area our_area = get_area(this) as area;
        string message = Dm.text("Now available in []:", our_area);

        bool has_minerals = false;
        DmList appended_list = Dm.list();

        foreach (object current_material in Dm.each<object>(mat_container.materials))
        {
            datum_material material_datum = (datum_material)current_material;
            float mineral_amount = Dm.num(mat_container.materials[current_material]) / SHEET_MATERIAL_AMOUNT;
            if (Dm.truthy(mineral_amount))
                has_minerals = true;
            appended_list[Dm.text("[]", capitalize(material_datum.name))] = Dm.text("[] sheets", mineral_amount);
        }

        if (!has_minerals)
            return;

        datum_signal_subspace_messaging_rc signal = Dm.@new<datum_signal_subspace_messaging_rc>(this, Dm.list(
            Dm.kv("ore_update", TRUE),
            Dm.kv("sender_department", "Ore Redemption Machine"),
            Dm.kv("message", message),
            Dm.kv("verified", "Ore Redemption Machine"),
            Dm.kv("priority", REQ_NORMAL_MESSAGE_PRIORITY),
            Dm.kv("appended_list", appended_list)));
        signal.send_to_receivers();
    }

    // tg: code/modules/mining/machine_redemption.dm:153
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (materials.mat_container == null || Dm.truthy(panel_open) || !Dm.truthy(powered__proc()))
            return await base.base_item_interaction(user, tool, modifiers);

        DmList gathered_ores = Dm.list();
        if (istype(tool, typeof(obj_item_stack_ore)))
            gathered_ores.Add(tool);
        else if (tool.atom_storage != null && !Dm.truthy(tool.atom_storage.locked))
            await tool.atom_storage.remove_type(typeof(obj_item_stack_ore), this, check_adjacent: TRUE, user: user, inserted: gathered_ores);

        if (!Dm.truthy(gathered_ores.Count))
            return await base.base_item_interaction(user, tool, modifiers);

        foreach (obj_item_stack_ore gathered_ore in Dm.each<obj_item_stack_ore>(gathered_ores.Copy()))
        {
            obj_item smelted_ore = gathered_ore.on_orm_collection() as obj_item;
            if (smelted_ore == null)
                continue;

            if (Dm.num(await materials.insert_item(smelted_ore, ore_multiplier, ID_DATA(user))) <= 0)
                await unload_mineral(smelted_ore);
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/machine_redemption.dm:176
    public override void pickup_item(datum source = null, atom_movable target = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER: the insertion's stacks and the dropped sheets are made without a wait
        Waiting.NoSleep(pickup_item__body(target));
    }

    private async DmTask pickup_item__body(atom_movable target)
    {
        if (QDELETED(target))
            return;

        if (materials.mat_container == null || Dm.truthy(panel_open) || !Dm.truthy(powered__proc()))
            return;

        DmList user_data = null;
        if (isliving(target.pulledby))
            user_data = ID_DATA(target.pulledby);
        //gethering the ore
        DmList ore_list = Dm.list();
        if (target is obj_structure_ore_box box)
        {
            foreach (obj_item_stack_ore ore_item in Dm.each<obj_item_stack_ore>(box.contents))
                ore_list.Add(ore_item);
        }
        else if (istype(target, typeof(obj_item_stack_ore)))
            ore_list.Add(target);
        else
            return;

        //smelting the ore
        foreach (obj_item_stack_ore gathered_ore in Dm.each<obj_item_stack_ore>(ore_list))
        {
            obj_item smelted_ore = gathered_ore.on_orm_collection() as obj_item;
            if (smelted_ore == null)
                continue;

            if (Dm.num(await materials.insert_item(smelted_ore, ore_multiplier, user_data)) <= 0)
                await unload_mineral(smelted_ore); //if rejected unload
        }

        if (!Dm.truthy(console_notify_timer))
        {
            // gives 5 seconds for a load of ores to be sucked up by the ORM before it sends out request console notifications. This should be enough time for most deposits that people make
            console_notify_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(send_console_message)), 5 * SECONDS,
                file: "code/modules/mining/machine_redemption.dm", line: 207);
        }
    }

    // tg: code/modules/mining/machine_redemption.dm:210
    public override async DmTask<object> default_unfasten_wrench(mob user = null, obj_item wrench = null, float? time = null)
    {
        object dot = await base.default_unfasten_wrench(user, wrench, time);
        if (!Dm.eq(dot, SUCCESSFUL_UNFASTEN))
            return dot;
        if (Dm.truthy(anchored))
            register_input_turf(); // someone just wrenched us down, re-register the turf
        else
            unregister_input_turf(); // someone just un-wrenched us, unregister the turf
        return dot;
    }

    // tg: code/modules/mining/machine_redemption.dm:219
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/mining/machine_redemption.dm:222
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
        => await default_deconstruction_crowbar(user, tool);

    // tg: code/modules/mining/machine_redemption.dm:225
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/machine_redemption.dm:229
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(panel_open))
            return CLICK_ACTION_BLOCKING;
        input_dir = Dm.num(turn(input_dir, -90));
        output_dir = Dm.num(turn(output_dir, -90));
        to_chat(user, span_notice(I18n.T("You change []'s I/O settings, setting the input to [] and the output to [].", this, dir2text(input_dir), dir2text(output_dir))));
        unregister_input_turf(); // someone just rotated the input and output directions, unregister the old turf
        register_input_turf(); // register the new one
        update_appearance(UPDATE_OVERLAYS);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/mining/machine_redemption.dm:240
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "OreRedemptionMachine");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mining/machine_redemption.dm:246
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["unclaimedPoints"] = points;
        DmList material_rows = Dm.list();
        data["materials"] = material_rows;
        datum_material_container mat_container = materials.mat_container;
        if (mat_container != null)
        {
            foreach (datum_material material in Dm.each<datum_material>(mat_container.materials))
            {
                float amount = Dm.num(mat_container.materials[material]);
                float sheet_amount = amount / SHEET_MATERIAL_AMOUNT;
                Type sheet_type = material.sheet_type as Type;
                material_rows.Add(Dm.list(Dm.list(
                    Dm.kv("name", material.name),
                    Dm.kv("id", REF(material)),
                    Dm.kv("amount", sheet_amount),
                    Dm.kv("category", "material"),
                    Dm.kv("value", ore_values[material.GetType()]),
                    Dm.kv("icon", sheet_type == null ? null : Dm.initial(sheet_type, (atom a) => a.icon)),
                    Dm.kv("icon_state", sheet_type == null ? null : Dm.initial(sheet_type, (atom a) => a.icon_state)))));
            }

            foreach (object design_path in Dm.each(stored_research.researched_designs))
            {
                datum_design alloy = SSresearch.techweb_designs[design_path] as datum_design;
                Type alloy_type = alloy.build_path;
                material_rows.Add(Dm.list(Dm.list(
                    Dm.kv("name", alloy.name),
                    Dm.kv("id", design_path),
                    Dm.kv("category", "alloy"),
                    Dm.kv("amount", can_smelt_alloy(alloy)),
                    Dm.kv("icon", Dm.initial(alloy_type, (atom a) => a.icon)),
                    Dm.kv("icon_state", Dm.initial(alloy_type, (atom a) => a.icon_state)))));
            }
        }

        data["disconnected"] = null;
        if (mat_container == null)
            data["disconnected"] = "Local mineral storage is unavailable";
        else if (materials.silo == null && Dm.truthy(requires_silo))
            data["disconnected"] = "No ore silo connection is available; storing locally";
        else if (!materials.check_z_level() && Dm.truthy(requires_silo))
            data["disconnected"] = "Unable to connect to ore silo, too far away";
        else if (materials.on_hold())
            data["disconnected"] = "Mineral withdrawal is on hold";

        obj_item_card_id card;
        if (isliving(user))
        {
            mob_living customer = (mob_living)user;
            card = customer.get_idcard(hand_first: TRUE) as obj_item_card_id;
            if (card?.registered_account != null)
            {
                data["user"] = Dm.list(
                    Dm.kv("name", card.registered_account.account_holder),
                    Dm.kv("cash", card.registered_account.mining_points));
            }
            else if (issilicon(user))
            {
                mob_living_silicon silicon_player = (mob_living_silicon)user;
                data["user"] = Dm.list(
                    Dm.kv("name", silicon_player.name),
                    Dm.kv("cash", "No valid account"));
            }
        }
        return data;
    }

    // tg: code/modules/mining/machine_redemption.dm:306
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        datum_material_container mat_container = materials.mat_container;
        mob usr = GlobalProcs.usr;
        switch (action as string)
        {
            case "Claim":
            {
                //requires silo but silo not in range
                if (Dm.truthy(requires_silo) && !materials.check_z_level())
                    return FALSE;

                //no ID
                obj_item_card_id user_id_card = null;
                if (isliving(usr))
                {
                    mob_living user = (mob_living)usr;
                    user_id_card = user.get_idcard(TRUE) as obj_item_card_id;
                }
                if (user_id_card == null)
                {
                    await say("No ID card found.");
                    return FALSE;
                }

                //we have points
                if (Dm.truthy(points))
                {
                    user_id_card.registered_account.mining_points += points;
                    points = 0;
                    return TRUE;
                }

                return FALSE;
            }
            case "Release":
            {
                if (mat_container == null)
                    return dot;
                if (!materials.can_use_resource(user_data: ID_DATA(usr)))
                    return dot;
                else if (!Dm.truthy(await allowed__proc(usr))) //Check the ID inside, otherwise check the user
                {
                    to_chat(usr, span_warning("Required access not found."));
                    return dot;
                }

                datum_material mat = Dm.locate_ref(@params["material_ref"]) as datum_material;

                object amount = mat_container.materials[mat];
                if (!Dm.truthy(amount))
                    return dot;

                float stored_amount = CEILING(Dm.num(amount) / SHEET_MATERIAL_AMOUNT, 0.1f);
                if (!Dm.truthy(stored_amount))
                    return dot;

                object desired = text2num(@params["sheets"]);
                float sheets_to_remove = round(min(Dm.num(desired), 50, stored_amount));
                await materials.eject_sheets(mat, sheets_to_remove, get_step(this, output_dir) as atom, user_data: ID_DATA(usr));
                return TRUE;
            }
            case "Smelt":
            {
                if (mat_container == null)
                    return dot;
                if (!materials.can_use_resource(user_data: ID_DATA(usr)))
                    return dot;
                object alloy_path = Dm.text2path(Dm.str(@params["path"]));
                if (!Dm.truthy(stored_research.researched_designs[alloy_path]))
                    return dot;
                datum_design alloy = SSresearch.techweb_designs[alloy_path] as datum_design;

                obj_item_card_id user_id_card = null;
                if (isliving(usr))
                {
                    mob_living user = usr as mob_living;
                    user_id_card = user.get_idcard(TRUE) as obj_item_card_id;
                }
                if ((Dm.truthy(check_access(user_id_card)) || Dm.truthy(await allowed__proc(usr as mob))) && alloy != null)
                {
                    float amount = round(min(Dm.num(text2num(@params["sheets"])), 50, Dm.num(can_smelt_alloy(alloy))));
                    if (amount < 1) //no negative mats
                        return dot;
                    await materials.use_materials(alloy.materials, multiplier: amount, action: "withdrawn", name: "sheets", user_data: ID_DATA(usr));
                    atom_movable output;
                    if (ispath(alloy.build_path, typeof(obj_item_stack_sheet)))
                        output = await alloy.create_result(this, amount: amount) as atom_movable;
                    else
                        output = await alloy.create_result(this) as atom_movable;
                    await unload_mineral(output);
                }
                else
                    to_chat(usr, span_warning(I18n.T("Required access not found.")));
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/mining/machine_redemption.dm:385
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        do_sparks(5, TRUE, this);
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/mining/machine_redemption.dm:389
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(panel_open) ? "-open" : Dm.truthy(powered__proc()) ? null : "-off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mining/machine_redemption.dm:393
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return dot;
        image ore_input = Dm.image(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), Dm.text("unres_[]", input_dir));
        image ore_output = Dm.image(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), Dm.text("unres_[]", REVERSE_DIR(input_dir)));

        switch (input_dir)
        {
            case NORTH:
                ore_input.pixel_z = 32;
                ore_output.pixel_z = -32;
                break;
            case SOUTH:
                ore_input.pixel_z = -32;
                ore_output.pixel_z = 32;
                break;
            case EAST:
                ore_input.pixel_w = 32;
                ore_output.pixel_w = -32;
                break;
            case WEST:
                ore_input.pixel_w = -32;
                ore_output.pixel_w = 32;
                break;
        }

        ore_input.color = COLOR_MODERATE_BLUE;
        ore_output.color = COLOR_SECURITY_RED;
        // DEVIATION(DV12): light_in and light_out, the two arrows' emissive_appearance copies (their glow in the dark)
        Dm.append(ref dot, ore_input);
        Dm.append(ref dot, ore_output);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/machine_redemption.dm:28  /obj/machinery/mineral/ore_redemption's var/static/list/ore_values
    internal DmList obj_machinery_mineral_ore_redemption__ore_values;
}
