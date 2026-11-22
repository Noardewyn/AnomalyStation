// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/lathe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing lathe ("a heavily gutted autolathe"), whole since silicons SI3 (D326): its material container
// (Lavaland L2a) and SSresearch's design table with the autolathe's auto-unlocking techweb. Not here: the screentip context
// (live-partial row, D264). The var blocks are generated.

// tg: code/modules/manufactorio/machines/lathe.dm:1
public partial class obj_machinery_power_manufacturing_lathe : obj_machinery_power_manufacturing // this is a heavily gutted autolathe
{
    // tg: code/modules/manufactorio/machines/lathe.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        print_sound = Dm.@new<datum_looping_sound_lathe_print>(this, FALSE);
        materials = Dm.@new<datum_material_container>(
            this,
            SSmaterials.flat_materials,
            0,
            Dm.bitor(MATCONTAINER_EXAMINE, MATCONTAINER_NO_INSERT)
        );
        register_context();
        object dot = base.Initialize(mapload, __extra);
        GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_autolathe)] ??= Dm.@new<datum_techweb_autounlocking_autolathe>();
        stored_research = GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_autolathe)] as datum_techweb_autounlocking;
        return dot;
    }

    // The first definition of Destroy (tg defines it twice on this type; the second's ..() calls this one, rules 2.5).
    // tg: code/modules/manufactorio/machines/lathe.dm:34
    public virtual object Destroy__d0(float? force = null)
    {
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:38
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_CTRL_SHIFT_LMB] = "Dump all contained materials";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:44
    public override async DmTask<object> click_ctrl_shift(mob user = null)
    {
        balloon_alert_to_viewers("materials dumped");
        await materials.retrieve_all();
        return null;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:48
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        datum_stock_part_matter_bin bin = Dm.locate<datum_stock_part_matter_bin>(component_parts);
        materials.max_amount = Dm.num(bin.tier) * (SHEET_MATERIAL_AMOUNT * MAX_STACK_SIZE);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:53
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        datum_design design = SSresearch.techweb_designs[chosen_design_path] as datum_design;
        Dm.append(ref dot, span_notice(I18n.T("It is set to print [].", design != null ? design.name : I18n.T("nothing, set with a multitool"))));
        if (design == null)
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("It needs:")));
        foreach ((object valid_type, object amount) in Dm.pairs(design.materials))
        {
            Dm.append(ref dot, I18n.T("[] sheets of []", Dm.div(amount, SHEET_MATERIAL_AMOUNT), Dm.initial(valid_type, "name")));
        }
        return dot;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:65
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, generate_io_overlays(dir, COLOR_ORANGE)); // OUT - stuff in it
        Dm.append(ref dot, generate_io_overlays(REVERSE_DIR(dir), COLOR_MODERATE_BLUE)); // IN - to crush
        return dot;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:70
    public override object Destroy(float? force = null)
    {
        object dot = Destroy__d0(force);
        stored_research = null;
        QDEL_NULL(ref print_sound);
        materials = null;
        QDEL_NULL(ref withheld);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:77
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (withheld != null)
            await withheld.Move((atom)drop_location());
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:81
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        if (!isstack(receiving) || Dm.istype(receiving, typeof(obj_item_stack_ore)) || Dm.truthy(Dm.bitand(((obj)receiving).resistance_flags, INDESTRUCTIBLE)) || !Dm.eq(receive_dir, REVERSE_DIR(dir)))
            return MANUFACTURING_FAIL;
        await materials.insert_item((obj_item)receiving);
        return MANUFACTURING_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:87
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        DmList name_to_id = Dm.list();
        foreach (object design_path in Dm.each(stored_research.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            name_to_id[design.name] = design_path;
        }
        object result = await tgui_input_list(user, "Select Design", "Select Design", (DmList)sort_list(name_to_id));
        if (result == null)
            return ITEM_INTERACT_FAILURE;
        chosen_design_path = name_to_id[result];
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:99
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (withheld != null && !Dm.truthy(Waiting.NoSleep(send_resource(withheld, dir))))
            return null;

        datum_design design = SSresearch.techweb_designs[chosen_design_path] as datum_design;
        if (design == null || !Dm.truthy(Dm.bitand(design.build_type, AUTOLATHE)))
            return null;
        if (Dm.num(surplus()) < power_cost)
        {
            finalize_build();
            return null;
        }
        //check for materials required. For custom material items decode their required materials
        DmList materials_needed = Dm.list();
        DmList slots_chosen = null;
        foreach (object design_material in Dm.each(design.materials))
        {
            object material = design_material;
            object amount_needed = design.materials[design_material];
            Type requirement = null;
            datum_material_slot slot = null;
            if (ispath(material, typeof(datum_material_requirement)))
                requirement = (Type)material;
            else if (ispath(material, typeof(datum_material_slot)))
            {
                slot = SSmaterials.material_slots[material] as datum_material_slot;
                requirement = slot.requirement_type;
            }

            if (requirement != null) // Material requirement
            {
                foreach (datum_material valid_candidate in Dm.each<datum_material>(SSmaterials.get_materials_by_req(material)))
                {
                    if (Waiting.NoSleep(materials.get_material_amount(valid_candidate)) >= Dm.num(amount_needed))
                    {
                        material = valid_candidate;
                        break;
                    }
                }
            }
            if (material == null)
                return null;
            materials_needed[material] = amount_needed;
            if (slot != null)
            {
                datum_material proper_mat = material as datum_material;
                LAZYSET(ref slots_chosen, slot.type, proper_mat.id);
            }
        }

        if (!Waiting.NoSleep(materials.has_materials(materials_needed)))
            return null;

        float craft_time = (float)Math.Pow(design.construction_time * design.lathe_time_factor, 0.8);
        flick_overlay_view(mutable_appearance(icon, "lathe_printing"), craft_time);
        print_sound.start();
        add_load(power_cost);
        busy = (_addtimer(Dm.@new<datum_callback>(this, nameof(do_make_item), design, materials_needed, slots_chosen), craft_time, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/modules/manufactorio/machines/lathe.dm", line: 140));
        return null;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:142
    public virtual async DmTask do_make_item(datum_design design = null, DmList materials_needed = null, DmList slots_chosen = null)
    {
        finalize_build();
        if (Dm.num(surplus()) < power_cost)
            return;

        bool is_stack = ispath(design.build_path, typeof(obj_item_stack));
        if (!await materials.has_materials(materials_needed))
            return;
        await materials.use_materials(materials_needed);

        atom_movable created;
        if (is_stack)
        {
            Type stack_item = design.build_path; // initial(design.build_path)
            float max_stack_amount = Dm.num(Dm.initial(stack_item, (obj_item_stack st) => st.max_amount));
            float amount = Dm.num(Dm.initial(stack_item, (obj_item_stack st) => st.amount));
            while (amount > max_stack_amount)
            {
                obj_item_stack new_stack = Dm.@new(stack_item, drop_location(), max_stack_amount) as obj_item_stack;
                if (!Dm.truthy(await send_resource(new_stack, dir)))
                    withheld = new_stack;
                amount -= max_stack_amount;
            }
            created = Dm.@new(stack_item, drop_location(), amount) as atom_movable;
        }
        else
        {
            created = await design.create_result((atom)drop_location(), materials_needed) as atom_movable;
            if (Dm.truthy(length(slots_chosen)))
                await created.set_material_slots(slots_chosen);
            await design.transfer_materials(materials_needed, target_object: created);
        }

        if (isitem(created))
        {
            created.pixel_x = created.base_pixel_x + rand(-6, 6);
            created.pixel_y = created.base_pixel_y + rand(-6, 6);
        }
        SSblackbox.record_feedback("nested tally", "lathe_printed_items", 1, Dm.list(Dm.text("[]", type), Dm.text("[]", created.type)));

        if (!Dm.truthy(await send_resource(created, dir)))
            withheld = created;
    }

    // tg: code/modules/manufactorio/machines/lathe.dm:177
    public virtual void finalize_build()
    {
        print_sound.stop();
        deltimer(busy);
        busy = null;
    }
}
