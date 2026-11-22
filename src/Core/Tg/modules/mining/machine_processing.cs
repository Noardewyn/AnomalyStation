// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/machine_processing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2a and L2b (D304): the whole file, /obj/machinery/mineral (the base of the ore redemption, processing, stacking and
// unloading machines), the processing unit (the furnace) and its console; the furnace's alloys (the smelter's auto-unlocking
// techweb, SSresearch's designs) since silicons SI3 (D326). Its window sends each material's sheet and each alloy's icon and state
// for tg's icon2base64 (DV257). The var block is generated (D111).

/**********************Mineral processing unit console**************************/

// tg: code/modules/mining/machine_processing.dm:7
public partial class obj_machinery_mineral : obj_machinery
{
    // tg: code/modules/mining/machine_processing.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(needs_item_input) && Dm.truthy(anchored))
            register_input_turf();
        return dot;
    }

    /// Gets the turf in the `input_dir` direction adjacent to the machine, and registers signals for ATOM_ENTERED and ATOM_CREATED. Calls the `pickup_item()` proc when it receives these signals.
    // tg: code/modules/mining/machine_processing.dm:24
    public virtual void register_input_turf()
    {
        input_turf = get_step(this, Dm.num(input_dir)) as turf;
        if (input_turf != null) // make sure there is actually a turf
            RegisterSignals(input_turf, Dm.list(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, COMSIG_ATOM_ENTERED), nameof(pickup_item));
    }

    /// Unregisters signals that are registered the machine's input turf, if it has one.
    // tg: code/modules/mining/machine_processing.dm:30
    public virtual void unregister_input_turf()
    {
        if (input_turf != null)
            UnregisterSignal(input_turf, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
    }

    // tg: code/modules/mining/machine_processing.dm:34
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!Dm.truthy(needs_item_input) || !Dm.truthy(anchored))
            return dot;
        unregister_input_turf();
        register_input_turf();
        return dot;
    }

    // tg: code/modules/mining/machine_processing.dm:41
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await base.shuttleRotate(rotation, @params);
        input_dir = Dm.num(angle2dir(Dm.num(rotation) + Dm.num(dir2angle(input_dir))));
        output_dir = Dm.num(angle2dir(Dm.num(rotation) + Dm.num(dir2angle(output_dir))));
    }

    /**
        Base proc for all `/mineral` subtype machines to use. Place your item pickup behavior in this proc when you override it for your specific machine.

        Called when the COMSIG_ATOM_ENTERED and COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON signals are sent.

        Arguments:
        * source - the turf that is listening for the signals.
        * target - the atom that just moved onto the `source` turf.
        * oldLoc - the old location that `target` was at before moving onto `source`.
    */
    // tg: code/modules/mining/machine_processing.dm:56
    public virtual void pickup_item(datum source = null, atom_movable target = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER
        return;
    }

    /// Generic unloading proc. Takes an atom as an argument and forceMove's it to the turf adjacent to this machine in the `output_dir` direction.
    // tg: code/modules/mining/machine_processing.dm:62
    public virtual async DmTask unload_mineral(atom_movable unloaded_mineral = null)
    {
        await unloaded_mineral.forceMove(drop_location() as atom);
        turf unload_turf = get_step(this, Dm.num(output_dir)) as turf;
        if (unload_turf != null)
            await unloaded_mineral.forceMove(unload_turf);
    }
}

// tg: code/modules/mining/machine_processing.dm:68
public partial class obj_machinery_mineral_processing_unit_console : obj_machinery_mineral
{
    // tg: code/modules/mining/machine_processing.dm:77
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        processing_machine = Dm.locate<obj_machinery_mineral_processing_unit>(range(2, this));
        if (processing_machine != null)
            processing_machine.mineral_machine = this;
        else
            return INITIALIZE_HINT_QDEL;
        return dot;
    }

    // tg: code/modules/mining/machine_processing.dm:85
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ProcessingConsole");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mining/machine_processing.dm:91
    public override async DmTask<object> ui_static_data(mob user = null)
        => await processing_machine.ui_static_data();

    // tg: code/modules/mining/machine_processing.dm:94
    public override async DmTask<object> ui_data(mob user = null)
        => await processing_machine.ui_data();

    // tg: code/modules/mining/machine_processing.dm:97
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "setMaterial":
            {
                datum_material new_material = Dm.locate_ref(@params["value"]) as datum_material;
                if (new_material == null)
                    return dot;

                processing_machine.selected_material = new_material;
                processing_machine.selected_alloy = null;
                return TRUE;
            }
            case "setAlloy":
                processing_machine.selected_material = null;
                processing_machine.selected_alloy = text2path(@params["new_alloy"] as string);
                return TRUE;
            case "toggle":
                processing_machine.on = Dm.num(!Dm.truthy(processing_machine.on));
                if (Dm.truthy(processing_machine.on))
                    processing_machine.begin_processing();
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mining/machine_processing.dm:122
    public override object Destroy(float? force = null)
    {
        processing_machine = null;
        return base.Destroy(force);
    }
}

/**********************Mineral processing unit**************************/

// tg: code/modules/mining/machine_processing.dm:130
public partial class obj_machinery_mineral_processing_unit : obj_machinery_mineral
{
    /// Smelt amount per second
    // tg: code/modules/mining/machine_processing.dm:2  #define SMELT_AMOUNT 5
    private const float SMELT_AMOUNT = 5;

    // tg: code/modules/mining/machine_processing.dm:149
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        proximity_monitor = Dm.@new<datum_proximity_monitor>(this, 1);

        materials = Dm.@new<datum_material_container>(
            this,
            SSmaterials.get_materials_by_flag(MATERIAL_SILO_STORED),
            INFINITY,
            MATCONTAINER_EXAMINE,
            Dm.named("allowed_items", accepted_type));
        GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_smelter)] ??= Dm.@new<datum_techweb_autounlocking_smelter>();
        stored_research = GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_smelter)] as datum_techweb;
        selected_material = (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron)));
        return dot;
    }

    // tg: code/modules/mining/machine_processing.dm:164
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref proximity_monitor);
        QDEL_NULL(ref materials);
        mineral_machine = null;
        stored_research = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/machine_processing.dm:171
    public virtual async DmTask process_ore(obj_item_stack O = null)
    {
        if (QDELETED(O))
            return;
        float material_amount = await materials.get_item_material_amount(O);
        if (!await materials.has_space(material_amount))
            await unload_mineral(O);
        else
            await materials.insert_item(O);
    }

    // tg: code/modules/mining/machine_processing.dm:180
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList material_icons = Dm.list();
        data["materialIcons"] = material_icons;
        foreach (datum_material material in Dm.each<datum_material>(materials.materials))
        {
            Type display = material.sheet_type as Type;
            // DEVIATION(DV257): the sheet's icon and state for icon2base64(icon(initial(display.icon), initial(display.icon_state), frame = 1))
            material_icons.Add(Dm.list(Dm.list(
                Dm.kv("id", REF(material)),
                Dm.kv("icon", display == null ? null : Dm.initial(display, (atom a) => a.icon)),
                Dm.kv("icon_state", display == null ? null : Dm.initial(display, (atom a) => a.icon_state)))));
        }

        DmList alloy_icons = Dm.list();
        data["alloyIcons"] = alloy_icons;
        foreach (object design_path in Dm.each(stored_research.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            Type display = design.build_path;
            // DEVIATION(DV257): the alloy's icon and state for icon2base64(icon(display::icon, display::icon_state, frame = 1))
            alloy_icons.Add(Dm.list(Dm.list(
                Dm.kv("id", Dm.text("[]", design_path)),
                Dm.kv("icon", Dm.initial(display, (atom a) => a.icon)),
                Dm.kv("icon_state", Dm.initial(display, (atom a) => a.icon_state)))));
        }

        Dm.append(ref data, await materials.ui_static_data());

        return data;
    }

    // tg: code/modules/mining/machine_processing.dm:205
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["materials"] = await materials.ui_data();
        data["selectedMaterial"] = selected_material?.name;

        DmList alloys = Dm.list();
        data["alloys"] = alloys;
        foreach (object design_path in Dm.each(stored_research.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            alloys.Add(Dm.list(Dm.list(
                Dm.kv("name", design.name),
                Dm.kv("path", design_path))));
        }
        data["selectedAlloy"] = selected_alloy;

        data["state"] = on;

        return data;
    }

    // tg: code/modules/mining/machine_processing.dm:224
    public override void pickup_item(datum source = null, atom_movable target = null, atom old_loc = null, DmList old_locs = null)
    {
        if (QDELETED(target))
            return;
        if (istype(target, accepted_type))
            Waiting.NoSleep(process_ore((obj_item_stack)target));
    }

    // tg: code/modules/mining/machine_processing.dm:230
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(on))
            return PROCESS_KILL;

        if (selected_material != null)
            Waiting.NoSleep(smelt_ore(Dm.num(seconds_per_tick)));
        else if (Dm.truthy(selected_alloy))
            Waiting.NoSleep(smelt_alloy(Dm.num(seconds_per_tick)));
        return null;
    }

    // tg: code/modules/mining/machine_processing.dm:239
    public virtual async DmTask smelt_ore(float seconds_per_tick = 2)
    {
        datum_material mat = selected_material;
        if (mat == null)
            return;
        float stored = Dm.num(materials.materials[mat]);
        float sheets_to_remove = (stored >= (SHEET_MATERIAL_AMOUNT * SMELT_AMOUNT * seconds_per_tick)) ? SMELT_AMOUNT * seconds_per_tick : round(stored / SHEET_MATERIAL_AMOUNT);
        if (!Dm.truthy(sheets_to_remove))
            on = FALSE;
        else
        {
            atom @out = get_step(this, output_dir) as atom;
            await materials.retrieve_stack(sheets_to_remove, mat, @out);
        }
    }

    // tg: code/modules/mining/machine_processing.dm:251
    public virtual async DmTask smelt_alloy(float seconds_per_tick = 2)
    {
        if (!Dm.truthy(stored_research.researched_designs[selected_alloy])) //check if it's a valid design
        {
            on = FALSE;
            return;
        }
        datum_design alloy = SSresearch.techweb_designs[selected_alloy] as datum_design;

        object amount = can_smelt(alloy, seconds_per_tick);
        if (!Dm.truthy(amount))
        {
            on = FALSE;
            return;
        }

        await materials.use_materials(alloy.materials, multiplier: Dm.num(amount));

        await generate_mineral(alloy.build_path);
    }

    // tg: code/modules/mining/machine_processing.dm:266
    public virtual object can_smelt(datum_design design = null, float seconds_per_tick = 2)
    {
        if (Dm.truthy(design.make_reagent))
            return FALSE;

        float build_amount = SMELT_AMOUNT * seconds_per_tick;

        foreach (object mat_cat in Dm.each<object>(design.materials))
        {
            float required_amount = Dm.num(design.materials[mat_cat]);
            float amount = Dm.num(materials.materials[mat_cat]);
            build_amount = min(build_amount, round(amount / required_amount));
        }

        return build_amount;
    }

    // tg: code/modules/mining/machine_processing.dm:278
    public virtual async DmTask generate_mineral(Type P = null)
    {
        atom_movable O = Dm.@new(P, this) as atom_movable;
        await unload_mineral(O);
    }
}
