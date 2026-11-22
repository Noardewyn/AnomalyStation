// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RPLD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. blueprint holds a typepath, so its var is
// hand-declared (the generator types it as an object of the type), with the constructor and the list initializer; the var/static
// lists are per-world state (rules 10.6). The mouse wheel that changes the layer (mouse_wheeled) is translated; the client sends
// no wheel input yet (mob/MouseWheelOn), as for the RPD.

///The plumbing RCD. All the blueprints are located in _globalvars > lists > construction.dm
// tg: code/game/objects/items/rcd/RPLD.dm:2
public partial class obj_item_construction_plumbing : obj_item_construction
{
    ///type of the plumbing machine
    // tg: code/game/objects/items/rcd/RPLD.dm:23
    // The generated field would be an obj_machinery (tg declares var/obj/machinery/blueprint for initial()), which a typepath does
    // not fit: the hand-written member holds the path, and the constructor with tg's overrides (RPLD.dm:3-18) is here.
    public Type blueprint;

    ///maps layer name to layer number value. didnt make this global cause only this class needs it
    // tg: code/game/objects/items/rcd/RPLD.dm:31  var/static/list/name_to_number (rules 10.6)
    public static ref DmList name_to_number => ref TgState.Current.obj_item_construction_plumbing__name_to_number;
    ///Design types for general plumbing constructor
    // tg: code/game/objects/items/rcd/RPLD.dm:39  var/static/list/general_design_types (rules 10.6)
    public static ref DmList general_design_types => ref TgState.Current.obj_item_construction_plumbing__general_design_types;

    // tg: code/game/objects/items/rcd/RPLD.dm:2
    public obj_item_construction_plumbing()
    {
        name = "Plumbing Constructor";
        desc = "An expertly modified RCD outfitted to construct plumbing machinery.";
        icon_state = "plumberer2";
        inhand_icon_state = "plumberer";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/tools_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/tools_righthand.dmi");
        worn_icon_state = "plumbing";
        icon = Dm.resource("icons/obj/tools.dmi");
        slot_flags = ITEM_SLOT_BELT;
        banned_upgrades = Dm.bitand(RCD_ALL_UPGRADES, Dm.bitnot(RCD_UPGRADE_SILO_LINK));
        matter = 200;
        max_matter = 200;
        drop_sound = Dm.resource("sound/items/handling/tools/rcd_drop.ogg");
        pickup_sound = Dm.resource("sound/items/handling/tools/rcd_pickup.ogg");
        sound_vary = TRUE;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:18  (a hand-written var block has its own dm_init_vars, types-converter.md 3.4)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT * 38), Dm.kv(typeof(datum_material_glass), SHEET_MATERIAL_AMOUNT * 18), Dm.kv(typeof(datum_material_plastic), HALF_SHEET_MATERIAL_AMOUNT));
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:72
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (plumbing_design_types == null)
            plumbing_design_types = general_design_types;

        selected_category = plumbing_design_types[1];
        /**
         * plumbing_design_types["Synthesizers"] = <list of designs under synthesizers>
         * <list of designs under synthesizers>[1] = <first design in this list>
         */
        blueprint = (plumbing_design_types[selected_category] as DmList)[1] as Type;
        return dot;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:85
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(Dm.num(slot), ITEM_SLOT_HANDS)))
            RegisterSignal(user, COMSIG_MOUSE_SCROLL_ON, nameof(mouse_wheeled));
        else
            UnregisterSignal(user, COMSIG_MOUSE_SCROLL_ON);
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:92
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        UnregisterSignal(user, COMSIG_MOUSE_SCROLL_ON);
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:96
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await ui_interact(user);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:100
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("You can scroll your mouse wheel to change the piping layer."));
        Dm.append(ref dot, I18n.T("You can right click a fluid duct to set the Plumbing RPD to its color and layer."));
        return dot;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:105
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RapidPlumbingDevice", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:111
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/plumbing)): the asset transport is the client's (DV44); the client
        // draws the designs from its own copy of their .dmi files (TguiSpritesheets).
        return Dm.list();
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:116
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await base.ui_static_data(user) as DmList;

        data["paint_colors"] = GLOB.pipe_paint_colors;

        DmList category_list = Dm.list();
        foreach (object category_name in Dm.each(plumbing_design_types))
        {
            DmList designs = plumbing_design_types[category_name] as DmList;

            //Create category
            DmList item_list = Dm.list();
            item_list["cat_name"] = category_name; //used by RapidPipeDispenser.js
            item_list["recipes"] = Dm.list(); //used by RapidPipeDispenser.js
            category_list[category_name] = item_list;

            //Add designs to category
            foreach (Type recipe in Dm.each_cast<Type>(designs))
            {
                DmList category_item = category_list[category_name] as DmList;
                category_item["recipes"] = Dm.add(category_item["recipes"], Dm.list(Dm.list(
                    Dm.kv("icon", Dm.initial(recipe, (atom r) => r.icon_state)),
                    Dm.kv("name", Dm.initial(recipe, (atom r) => r.name)))));
            }
        }

        data["categories"] = Dm.list();
        foreach (object category_name in Dm.each(category_list))
            data["categories"] = Dm.add(data["categories"], Dm.list(category_list[category_name]));

        return data;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:144
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;

        data["piping_layer"] = name_to_number[current_layer]; //maps layer name to layer number's 1,2,3,4,5
        data["selected_color"] = current_color;
        data["layer_icon"] = Dm.text("plumbing_layer[]", GLOB.plumbing_layers[current_layer]);
        data["selected_category"] = selected_category;
        data["selected_recipe"] = blueprint != null ? Dm.initial(blueprint, (atom r) => r.name) : null;

        return data;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:155
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        playsound(this, SFX_TOOL_SWITCH, 20, TRUE);

        switch (action)
        {
            case "color":
            {
                object color = @params["paint_color"];
                if (GLOB.pipe_paint_colors[color] != null) //validate if the color is in the allowed list of values
                    current_color = color as string;
                break;
            }
            case "piping_layer":
            {
                float? bitflag = text2num(@params["piping_layer"]); //convert from layer number back to layer string
                bitflag = Dm.shl(1, Dm.num(bitflag) - 1);
                object layer = GLOB.plumbing_layer_names[Dm.text("[]", bitflag)];
                if (layer != null) //validate if this value exists in the list
                    current_layer = layer as string;
                break;
            }
            case "recipe":
            {
                object category = @params["category"];

                DmList designs = plumbing_design_types[category] as DmList;
                if (!Dm.truthy(length(designs)))
                    return FALSE;
                selected_category = category;

                object design = designs[Dm.num(text2num(@params["id"])) + 1];
                if (!Dm.truthy(design))
                    return FALSE;
                blueprint = design as Type;
                blueprint_changed = TRUE;
                break;
            }
        }

        return TRUE;
    }

    ///pretty much rcd_create, but named differently to make myself feel less bad for copypasting from a sibling-type
    // tg: code/game/objects/items/rcd/RPLD.dm:187
    public virtual async DmTask<object> create_machine(atom destination = null, mob user = null)
    {
        if (!isopenturf(destination))
            return FALSE;

        object cost = 0;
        foreach (object category in Dm.each(plumbing_design_types))
        {
            cost = (plumbing_design_types[category] as DmList)[blueprint];
            if (Dm.truthy(cost))
                break;
        }
        if (!Dm.truthy(cost))
            return FALSE;

        //resource & placement sanity check before & after delay
        object is_allowed = TRUE;
        if (!Dm.truthy(await useResource(cost, user, TRUE)) || !Dm.truthy(is_allowed = await canPlace(destination as turf)))
        {
            if (!Dm.truthy(is_allowed))
                balloon_alert(user, "tile is blocked!");
            return FALSE;
        }
        if (!Dm.truthy(await build_delay(user, cost, target: destination)))
            return FALSE;
        if (!Dm.truthy(await useResource(cost, user, TRUE)) || !Dm.truthy(is_allowed = await canPlace(destination as turf)))
        {
            if (!Dm.truthy(is_allowed))
                balloon_alert(user, "tile is blocked!");
            return FALSE;
        }

        playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        if (ispath(blueprint, typeof(obj_machinery_duct)))
            Dm.@new(blueprint, destination, GLOB.pipe_paint_colors[current_color], GLOB.plumbing_layers[current_layer]);
        else
            Dm.@new(blueprint, destination, GLOB.plumbing_layers[current_layer]);
        await useResource(cost, user);
        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:220
    public virtual async DmTask<object> canPlace(turf destination = null)
    {
        if (!isopenturf(destination))
            return FALSE;
        if (Dm.truthy(Dm.initial(blueprint, (atom b) => b.density)) && Dm.truthy(await destination.is_blocked_turf(exclude_mobs: FALSE, source_atom: null, ignore_atoms: null)))
            return FALSE;
        return ducting_layer_check(destination, (ispath(blueprint, typeof(obj_machinery_duct)) ? 1 : -1) * Dm.num(GLOB.plumbing_layers[current_layer])) == null ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:227
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;

        foreach (object category_name in Dm.each(plumbing_design_types))
        {
            DmList designs = plumbing_design_types[category_name] as DmList;

            foreach (Type recipe in Dm.each_cast<Type>(designs))
            {
                if (interacting_with.type != recipe)
                    continue;

                obj_machinery machine_target = (obj_machinery)interacting_with;
                if (Dm.truthy(machine_target.anchored))
                {
                    balloon_alert(user, "unanchor first!");
                    return ITEM_INTERACT_BLOCKING;
                }
                if (Dm.truthy(await do_after(user, 2 * SECONDS, target: interacting_with)))
                {
                    float design_cost = Dm.num(designs[machine_target.type]);
                    float to_return = min(design_cost, max_matter - matter); // Give back matter was used to create smth
                    if (to_return < design_cost)
                        balloon_alert(user, "storage full!");
                    matter += to_return;
                    await machine_target.deconstruct();
                    playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE); //this is just such a great sound effect
                }
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (!isopenturf(interacting_with))
            return NONE;
        if (Dm.truthy(await create_machine(interacting_with, user)))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:261
    [DmParams("target", "user", "modifiers")]
    public override async DmTask<object> interact_with_atom_secondary(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        if (!istype(target, typeof(obj_machinery_duct)))
            return NONE;

        obj_machinery_duct duct = (obj_machinery_duct)target;
        if (Dm.truthy(duct.duct_layer) && Dm.truthy(duct.duct_color))
        {
            current_color = GLOB.pipe_color_name[duct.duct_color] as string;
            current_layer = GLOB.plumbing_layer_names[Dm.text("[]", duct.duct_layer)] as string;
            balloon_alert(user, Dm.text("using [], layer []", current_color, current_layer));
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:273
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await ui_interact(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/rcd/RPLD.dm:277
    [SignalHandler]
    public virtual void mouse_wheeled(mob source = null, atom A = null, object delta_x = null, object delta_y = null, object @params = null)
    {
        if (Dm.truthy(INCAPACITATED_IGNORING(source, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_STASIS))))
            return;
        if (Dm.eq(delta_y, 0))
            return;

        if (Dm.num(delta_y) < 0)
        {
            float current_loc = GLOB.plumbing_layers.Find(current_layer) + 1;
            if (current_loc > GLOB.plumbing_layers.len)
                current_loc = 1;
            current_layer = GLOB.plumbing_layers[current_loc] as string;
        }
        else
        {
            float current_loc = GLOB.plumbing_layers.Find(current_layer) - 1;
            if (current_loc < 1)
                current_loc = GLOB.plumbing_layers.len;
            current_layer = GLOB.plumbing_layers[current_loc] as string;
        }
        to_chat(source, span_notice(I18n.T("You set the layer to [].", current_layer)));
    }
}

// tg: code/game/objects/items/rcd/RPLD.dm:296
public partial class obj_item_construction_plumbing_service : obj_item_construction_plumbing
{
    ///Design types for plumbing service constructor
    // tg: code/game/objects/items/rcd/RPLD.dm:304  var/static/list/service_design_types (rules 10.6)
    public static ref DmList service_design_types => ref TgState.Current.obj_item_construction_plumbing_service__service_design_types;

    // tg: code/game/objects/items/rcd/RPLD.dm:335
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        plumbing_design_types = service_design_types;

        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/rcd/RPLD.dm:31
    internal DmList obj_item_construction_plumbing__name_to_number = Dm.list(
        Dm.kv("First Layer", 1),
        Dm.kv("Second Layer", 2),
        Dm.kv("Default Layer", 3),
        Dm.kv("Fourth Layer", 4),
        Dm.kv("Fifth Layer", 5));

    // tg: code/game/objects/items/rcd/RPLD.dm:39
    internal DmList obj_item_construction_plumbing__general_design_types = Dm.list(
        //category 1 Synthesizers i.e devices which creates , reacts & destroys chemicals
        Dm.kv("Synthesizers", Dm.list(
                Dm.kv(typeof(obj_machinery_plumbing_synthesizer), 15),
                Dm.kv(typeof(obj_machinery_plumbing_reaction_chamber_chem), 15),
                Dm.kv(typeof(obj_machinery_plumbing_grinder_chemical), 30),
                Dm.kv(typeof(obj_machinery_plumbing_fermenter), 30),
                Dm.kv(typeof(obj_machinery_plumbing_liquid_pump), 35),
                Dm.kv(typeof(obj_machinery_plumbing_disposer), 10))),

        //category 2 distributors i.e devices which inject , move around , remove chemicals from the network
        Dm.kv("Distributors", Dm.list(
                Dm.kv(typeof(obj_machinery_duct), 1),
                Dm.kv(typeof(obj_machinery_plumbing_layer_manifold), 5),
                Dm.kv(typeof(obj_machinery_plumbing_input), 5),
                Dm.kv(typeof(obj_machinery_plumbing_filter), 5),
                Dm.kv(typeof(obj_machinery_plumbing_splitter), 5),
                Dm.kv(typeof(obj_machinery_plumbing_sender), 20),
                Dm.kv(typeof(obj_machinery_plumbing_output), 5))),

        //category 3 Storage i.e devices which stores & makes the processed chemicals ready for consumption
        Dm.kv("Storage", Dm.list(
                Dm.kv(typeof(obj_machinery_plumbing_tank), 20),
                Dm.kv(typeof(obj_machinery_plumbing_acclimator), 10),
                Dm.kv(typeof(obj_machinery_plumbing_buffer), 10),
                Dm.kv(typeof(obj_machinery_plumbing_bottler), 50),
                Dm.kv(typeof(obj_machinery_plumbing_pill_press), 20),
                Dm.kv(typeof(obj_machinery_iv_drip_plumbing), 20))));

    // tg: code/game/objects/items/rcd/RPLD.dm:304
    internal DmList obj_item_construction_plumbing_service__service_design_types = Dm.list(
        //Category 1 synthesizers
        Dm.kv("Synthesizers", Dm.list(
                Dm.kv(typeof(obj_machinery_plumbing_synthesizer_soda), 15),
                Dm.kv(typeof(obj_machinery_plumbing_synthesizer_beer), 15),
                Dm.kv(typeof(obj_machinery_plumbing_reaction_chamber), 15),
                Dm.kv(typeof(obj_machinery_plumbing_buffer), 10),
                Dm.kv(typeof(obj_machinery_plumbing_fermenter), 30),
                Dm.kv(typeof(obj_machinery_plumbing_grinder_chemical), 30),
                Dm.kv(typeof(obj_machinery_plumbing_disposer), 10))),

        //Category 2 distributors
        Dm.kv("Distributors", Dm.list(
                Dm.kv(typeof(obj_machinery_duct), 1),
                Dm.kv(typeof(obj_machinery_plumbing_layer_manifold), 5),
                Dm.kv(typeof(obj_machinery_plumbing_input), 5),
                Dm.kv(typeof(obj_machinery_plumbing_filter), 5),
                Dm.kv(typeof(obj_machinery_plumbing_splitter), 5),
                Dm.kv(typeof(obj_machinery_plumbing_output_tap), 5),
                Dm.kv(typeof(obj_machinery_plumbing_sender), 20))),

        //category 3 storage
        Dm.kv("Storage", Dm.list(
                Dm.kv(typeof(obj_machinery_plumbing_bottler), 50),
                Dm.kv(typeof(obj_machinery_plumbing_tank), 20),
                Dm.kv(typeof(obj_machinery_plumbing_acclimator), 10))));
}
