// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/circuitboards/machines/machine_circuitboards.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 2 (the PACMAN): the PACMAN generator's board, whose mode (plasma or uranium) its on_construction reads. M7 slice 4 (the
// cooling loop): the temperature control unit's board, whose piping layer its on_construction reads. Silicons SI4 (D326): the holopad's
// board, whose security a multitool toggles. Gaps H2 (D333): the smart fridge's board, which a screwdriver switches between the
// fridge kinds. Gaps MD1: the medical kiosk's board (a multitool sets its price) and the first aid station's (an emag fries its
// safety chip; it is built against a wall). The processor's board, which a screwdriver switches between food and slimes. The other machine boards of
// the file are var-only and live with the machine board (circuitboard.cs); their procs come with their machines. The var blocks are
// generated (D111).

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:291
public partial class obj_item_circuitboard_machine_pacman : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:302
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        string message = Dm.truthy(high_production_profile) ? I18n.T("high-power uranium mode") : I18n.T("medium-power plasma mode");
        Dm.append(ref dot, span_notice(I18n.T("It's set to [].", message)));
        Dm.append(ref dot, span_notice(I18n.T("You can switch the mode by using a screwdriver on [].", this)));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:308
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        high_production_profile = Dm.truthy(high_production_profile) ? FALSE : TRUE;
        string message = Dm.truthy(high_production_profile) ? I18n.T("high-power uranium mode") : I18n.T("medium-power plasma mode");
        to_chat(user, span_notice(I18n.T("You set the board for []", message)));
        return null;
    }
}

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:417
public partial class obj_item_circuitboard_machine_thermomachine : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:427
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.multitool_act(user, tool);
        pipe_layer = (pipe_layer >= PIPING_LAYER_MAX) ? PIPING_LAYER_MIN : (pipe_layer + 1);
        to_chat(user, span_notice(I18n.T("You change the circuitboard to layer [].", pipe_layer)));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:432
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is set to layer [].", pipe_layer)));
        return dot;
    }
}

// Gaps package S2: the vendor's board (the brand selection dial, the parts it gives its vendor: the vendor's refill canister).

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:669
public partial class obj_item_circuitboard_machine_vendor : obj_item_circuitboard_machine
{
    ///Assoc list (machine name = machine typepath) of all vendors that can be chosen when the circuit is screwdrivered
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:677  var/static/list/valid_vendor_names_paths (rules 10.6)
    public static ref DmList valid_vendor_names_paths => ref TgState.Current.obj_item_circuitboard_machine_vendor__valid_vendor_names_paths;

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:679
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(valid_vendor_names_paths))
        {
            valid_vendor_names_paths = Dm.list();
            foreach (Type vendor_type in Dm.each_cast<Type>(subtypesof(typeof(obj_machinery_vending))))
            {
                if (Dm.truthy(Dm.initial(vendor_type, (obj_machinery_vending p) => p.allow_custom)) && Dm.initial(vendor_type, (obj_machinery_vending p) => p.refill_canister) != null)
                    valid_vendor_names_paths[Dm.initial(vendor_type, (atom p) => p.name)] = vendor_type;
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:687
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_FAILURE;
        if (Dm.truthy(all_products_free))
            return dot;
        object choice = await tgui_input_list(user, "Choose a new brand", "Select an Item", sort_list(valid_vendor_names_paths) as DmList);
        if (choice == null)
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return dot;
        set_type(valid_vendor_names_paths[choice] as Type);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Sets circuitboard details based on the vending machine type to create
     *
     * Arguments
     * * obj/machinery/vending/typepath - the vending machine type to create
    */
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:705
    public virtual void set_type(Type typepath = null)
    {
        build_path = typepath;
        name = I18n.T("[] Vendor", Dm.initial(typepath, (atom p) => p.name));
        req_components = Dm.list(Dm.kv(Dm.initial(typepath, (obj_machinery_vending p) => p.refill_canister), 1));
        flatpack_components = Dm.list(Dm.initial(typepath, (obj_machinery_vending p) => p.refill_canister));
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:711
    public override async DmTask<object> apply_default_parts(obj_machinery machine = null)
    {
        set_type(machine.GetType());
        return await base.apply_default_parts(machine);
    }
}

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:1320
// Gaps P2 (hydroponics): the tray's board, whose status lights' place a screwdriver or a plant analyzer switches.
public partial class obj_item_circuitboard_machine_hydroponics : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:1330
    public virtual void changeindicators(mob_living user = null, obj_item I = null)
    {
        if (build_path == typeof(obj_machinery_hydroponics_constructable_oldstyle))
        {
            name = I18n.T("Hydroponics Tray []", name_extension);
            build_path = typeof(obj_machinery_hydroponics_constructable);
            balloon_alert(user, I18n.T("defaulting indicator location"));
        }
        else
        {
            name = I18n.T("Hydroponics Tray (Alt) []", name_extension);
            build_path = typeof(obj_machinery_hydroponics_constructable_oldstyle);
            balloon_alert(user, I18n.T("moved indicators location"));
        }
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:1340
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item I = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(I, typeof(obj_item_plant_analyzer)))
        {
            changeindicators(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, I, modifiers, __extra);
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:1346
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        changeindicators(user);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:677  the vendor board's brands, built by the first board
    internal DmList obj_item_circuitboard_machine_vendor__valid_vendor_names_paths;
}

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:528
public partial class obj_item_circuitboard_machine_holopad : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:536
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(secure))
        {
            build_path = typeof(obj_machinery_holopad);
            secure = FALSE;
        }
        else
        {
            build_path = typeof(obj_machinery_holopad_secure);
            secure = TRUE;
        }
        to_chat(user, span_notice(I18n.T("You []able the security on []", Dm.truthy(secure) ? "en" : "dis", this)));
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:546
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, I18n.T("There is a connection port on this board that could be <b>pulsed</b>"));
        if (Dm.truthy(secure))
            Dm.append(ref dot, I18n.T("There is a red light flashing next to the word \"secure\""));
        return dot;
    }
}

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:589
public partial class obj_item_circuitboard_machine_smartfridge : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:593  var/static/list/fridges_name_paths (rules 10.6)
    public static DmList fridges_name_paths => TgState.Current.obj_item_circuitboard_machine_smartfridge__fridges_name_paths ??= Dm.list(
        Dm.kv(typeof(obj_machinery_smartfridge), "plant produce"),
        Dm.kv(typeof(obj_machinery_smartfridge_food), "food"),
        Dm.kv(typeof(obj_machinery_smartfridge_drinks), "drinks"),
        Dm.kv(typeof(obj_machinery_smartfridge_extract), "slimes"),
        Dm.kv(typeof(obj_machinery_smartfridge_petri), "petri"),
        Dm.kv(typeof(obj_machinery_smartfridge_organ), "organs"),
        Dm.kv(typeof(obj_machinery_smartfridge_chemistry), "chems"),
        Dm.kv(typeof(obj_machinery_smartfridge_chemistry_virology), "viruses"),
        Dm.kv(typeof(obj_machinery_smartfridge_disks), "disks"));

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:605
    public override async DmTask<object> apply_default_parts(obj_machinery machine = null)
    {
        obj_machinery_smartfridge smartfridge = (obj_machinery_smartfridge)machine;
        build_path = smartfridge.base_build_path;
        if (!Dm.truthy(fridges_name_paths.Find(build_path)))
        {
            name = Dm.text("[]", Dm.initial(smartfridge, (atom a) => a.name)); //if it's a unique type, give it a unique name.
            is_special_type = TRUE;
        }
        return await base.apply_default_parts(machine);
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:612
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(is_special_type))
            return FALSE;
        float position = fridges_name_paths.Find(build_path); // tg passes the list itself as Find's start, a non-number that BYOND reads as the default 1
        position = (position == Dm.length(fridges_name_paths)) ? 1 : (position + 1);
        build_path = fridges_name_paths[position] as Type;
        to_chat(user, span_notice(I18n.T("You set the board to [].", fridges_name_paths[build_path])));
        return TRUE;
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:621
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(is_special_type))
            return dot;
        Dm.append(ref dot, span_info(I18n.T("[] is set to []. You can use a screwdriver to reconfigure it.", this, fridges_name_paths[build_path])));
        return dot;
    }
}

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:875
public partial class obj_item_circuitboard_machine_medical_kiosk : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:884
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        object new_cost = await tgui_input_number(user, I18n.T("New cost for using this medical kiosk"), I18n.T("Pricing"), custom_cost, 1000, 10);
        if (!Dm.truthy(new_cost) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return dot;
        if (!ReferenceEquals(loc, user))
        {
            to_chat(user, span_warning(I18n.T("You must hold the circuitboard to change its cost!")));
            return dot;
        }
        custom_cost = Dm.num(new_cost);
        to_chat(user, span_notice(I18n.T("The cost is now set to [].", custom_cost)));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:895
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("The cost to use this kiosk is set to [].", custom_cost));
        return dot;
    }
}

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:1993
public partial class obj_item_circuitboard_machine_wall_healer : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:2004
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(ref dot, span_warning(I18n.T("The safety chip looks fried.")));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:2009
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        visible_message(span_warning(I18n.T("Sparks fly out of []!", this)));
        balloon_alert(user, I18n.T("safeties disabled"));
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // Someone please add generic support for constructing wall mounted objects thanks
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:2020
    public override object completion_requirements(obj_structure_frame install_frame = null, mob_living user = null)
    {
        if (Dm.locate<obj_machinery_wall_healer>(install_frame.loc) != null) // for subtypes support
        {
            install_frame.balloon_alert(user, I18n.T("identical machine present!"));
            return FALSE;
        }

        turf facing_wall = get_step(install_frame, user.dir);
        if (!Dm.truthy(is_mountable_turf(facing_wall)))
        {
            install_frame.balloon_alert(user, I18n.T("no wall to install on!"));
            return FALSE;
        }

        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:593  /obj/item/circuitboard/machine/smartfridge's var/static/list/fridges_name_paths
    internal DmList obj_item_circuitboard_machine_smartfridge__fridges_name_paths;
}

// tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:1383
public partial class obj_item_circuitboard_machine_processor : obj_item_circuitboard_machine
{
    // tg: code/game/objects/items/circuitboards/machines/machine_circuitboards.dm:1391
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (build_path == typeof(obj_machinery_processor))
        {
            name = "Slime Processor";
            build_path = typeof(obj_machinery_processor_slime);
            to_chat(user, span_notice(I18n.T("Name protocols successfully updated.")));
        }
        else
        {
            name = "Food Processor";
            build_path = typeof(obj_machinery_processor);
            to_chat(user, span_notice(I18n.T("Defaulting name protocols.")));
        }
        return TRUE;
    }
}
