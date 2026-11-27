// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/pipe/pipe_dispenser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/machinery/pipe/pipe_dispenser.dm:5
public partial class obj_machinery_pipedispenser : obj_machinery
{
    // tg: code/game/machinery/pipe/pipe_dispenser.dm:1  #define ATMOS_PIPEDISPENSER 0
    private const float ATMOS_PIPEDISPENSER = 0;
    // tg: code/game/machinery/pipe/pipe_dispenser.dm:2  #define DISPOSAL_PIPEDISPENSER 1
    private const float DISPOSAL_PIPEDISPENSER = 1;
    // tg: code/game/machinery/pipe/pipe_dispenser.dm:3  #define TRANSIT_PIPEDISPENSER 2
    private const float TRANSIT_PIPEDISPENSER = 2;

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:23
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:26
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list(Dm.kv("paint_colors", GLOB.pipe_paint_colors));
        return data;
    }

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:30
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("category", category),
            Dm.kv("piping_layer", piping_layer),
            Dm.kv("categories", Dm.list()),
            Dm.kv("selected_color", paint_color));

        // The get the recipies for this dispenser
        DmList recipes = null;
        object sw__1 = category;
        if (Dm.eq(sw__1, ATMOS_PIPEDISPENSER))
            recipes = GLOB.atmos_pipe_recipes;
        else if (Dm.eq(sw__1, DISPOSAL_PIPEDISPENSER))
            recipes = GLOB.disposal_pipe_recipes;
        else if (Dm.eq(sw__1, TRANSIT_PIPEDISPENSER))
            recipes = GLOB.transit_tube_recipes;
        // Generate pipe categories
        foreach (object c in Dm.each(recipes))
        {
            DmList cat = recipes[c] as DmList;
            DmList r = Dm.list();
            for (float i = 1, i__end = cat.len; i <= i__end; i++)
            {
                datum_pipe_info info = cat[i] as datum_pipe_info;
                Dm.append(ref r, Dm.list(Dm.list(Dm.kv("pipe_name", info.name), Dm.kv("pipe_index", i), Dm.kv("all_layers", info.all_layers), Dm.kv("dir", NORTH))));
                // if this is bendable, add the bent version of the pipe (disposals)
                if (Dm.eq(info.dirtype, PIPE_BENDABLE))
                    Dm.append(ref r, Dm.list(Dm.list(Dm.kv("pipe_name", Dm.add("Bent ", info.name)), Dm.kv("pipe_index", i), Dm.kv("all_layers", info.all_layers), Dm.kv("dir", NORTHEAST))));
            }
            Dm.append_at(data, "categories", Dm.list(Dm.list(Dm.kv("cat_name", c), Dm.kv("recipes", r))));
        }
        DmList init_directions = Dm.list(Dm.kv("north", FALSE), Dm.kv("south", FALSE), Dm.kv("east", FALSE), Dm.kv("west", FALSE));
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(p_init_dir, Dm.num(direction))))
                init_directions[dir2text(direction)] = TRUE;
        }
        data["init_directions"] = init_directions;
        return data;
    }

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:65
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.truthy(await base.ui_act(action, @params, ui, state)))
            return null;
        object sw__1 = action;
        if (Dm.eq(sw__1, "color"))
        {
            paint_color = @params["paint_color"] as string;
        }
        else if (Dm.eq(sw__1, "pipe_type"))
        {
            object sw__2 = category;
            if (Dm.eq(sw__2, ATMOS_PIPEDISPENSER))
            {
                if (wait < world.time)
                {
                    datum_pipe_info info = (GLOB.atmos_pipe_recipes[@params["category"]] as DmList)[@params["pipe_type"]] as datum_pipe_info;
                    Type recipe_type = info.type;
                    object p_type = info.id;

                    // No spawning arbitrary paths (literally 1984)
                    if (!Dm.truthy(verify_recipe(GLOB.atmos_pipe_recipes, p_type)))
                        return null;

                    // If this is a meter, make that.
                    if (recipe_type == typeof(datum_pipe_info_meter))
                    {
                        Dm.@new<obj_item_pipe_meter>(loc);
                        wait = world.time + 1 * SECONDS;
                        return null;
                    }

                    // Otherwise, make a pipe/device
                    object p_dir = @params["pipe_dir"];
                    obj_item_pipe pipe_out = await Dm.new_async(typeof(obj_item_pipe), loc, p_type, p_dir) as obj_item_pipe;
                    pipe_out.p_init_dir = p_init_dir;
                    pipe_out.pipe_color = GLOB.pipe_paint_colors[paint_color];
                    await pipe_out.add_atom_colour(GLOB.pipe_paint_colors[paint_color], FIXED_COLOUR_PRIORITY);
                    pipe_out.set_piping_layer(piping_layer);
                    pipe_out.add_fingerprint(usr);
                    wait = world.time + 1 * SECONDS;
                }
            }
            else if (Dm.eq(sw__2, DISPOSAL_PIPEDISPENSER))
            {
                if (wait < world.time)
                {
                    datum_pipe_info info = (GLOB.disposal_pipe_recipes[@params["category"]] as DmList)[@params["pipe_type"]] as datum_pipe_info;
                    object p_type = info.id;

                    // No spawning arbitrary paths (literally 1984)
                    if (!Dm.truthy(verify_recipe(GLOB.disposal_pipe_recipes, p_type)))
                        return null;

                    obj_structure_disposalconstruct disposal_out = Dm.@new<obj_structure_disposalconstruct>(loc, p_type);
                    if (!Dm.truthy(disposal_out.can_place()))
                    {
                        to_chat(usr, span_warning(I18n.T("There's not enough room to build that here!")));
                        qdel(disposal_out);
                        return null;
                    }

                    disposal_out.add_fingerprint(usr);
                    disposal_out.update_appearance();
                    await disposal_out.setDir(@params["pipe_dir"]);
                    wait = world.time + 1 * SECONDS;
                }
            }
            else if (Dm.eq(sw__2, TRANSIT_PIPEDISPENSER))
            {
                if (wait < world.time)
                {
                    datum_pipe_info info = (GLOB.transit_tube_recipes[@params["category"]] as DmList)[@params["pipe_type"]] as datum_pipe_info;
                    object p_type = info.id;

                    // No spawning arbitrary paths (literally 1984)
                    if (!Dm.truthy(verify_recipe(GLOB.transit_tube_recipes, p_type)))
                        return null;

                    obj_structure_c_transit_tube tube_out = Dm.@new(p_type as Type, loc) as obj_structure_c_transit_tube;
                    tube_out.add_fingerprint(usr);
                    tube_out.update_appearance();
                    await tube_out.setDir(@params["pipe_dir"]);
                    wait = world.time + 1 * SECONDS;
                }
            }
        }
        else if (Dm.eq(sw__1, "piping_layer"))
        {
            piping_layer = Dm.num(text2num(@params["piping_layer"]));
        }
        else if (Dm.eq(sw__1, "init_dir_setting"))
        {
            float target_dir = Dm.bitxor(p_init_dir, Dm.num(text2dir(@params["dir_flag"])));
            // Refuse to create a smart pipe that can only connect in one direction (it would act weirdly and lack an icon)
            if (Dm.truthy(ISNOTSTUB(target_dir)))
                p_init_dir = target_dir;
            else
                to_chat(usr, span_warning(I18n.T("\\The []'s screen flashes a warning: Can't configure a pipe to only connect in one direction.", this)));
        }
        else if (Dm.eq(sw__1, "init_reset"))
        {
            p_init_dir = ALL_CARDINALS;
        }

        return TRUE;
    }

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:148
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PipeDispenser", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:154
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!istype(tool, typeof(obj_item_pipe)) && !istype(tool, typeof(obj_item_pipe_meter)))
            return NONE;
        to_chat(usr, span_notice(I18n.T("You put [] back into [].", tool, this)));
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:162
    public virtual object verify_recipe(DmList recipes = null, object path = null)
    {
        foreach (object category in Dm.each(recipes))
        {
            DmList cat_recipes = recipes[category] as DmList;
            foreach (object i in Dm.each(cat_recipes))
            {
                datum_pipe_info info = i as datum_pipe_info;
                if (Dm.eq(path, info.id))
                    return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/machinery/pipe/pipe_dispenser.dm:171
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 4 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/machinery/pipe/pipe_dispenser.dm:178
public partial class obj_machinery_pipedispenser_disposal : obj_machinery_pipedispenser
{
    //Allow you to drag-drop disposal pipes and transit tubes into it
    // tg: code/game/machinery/pipe/pipe_dispenser.dm:186
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        obj_structure pipe = dropped as obj_structure;
        if (!istype(pipe, typeof(obj_structure_disposalconstruct)) && !istype(pipe, typeof(obj_structure_c_transit_tube)) && !istype(pipe, typeof(obj_structure_c_transit_tube_pod)))
            return null;

        if (get_dist(user, this) > 1 || get_dist(this, pipe) > 1)
            return null;

        if (Dm.truthy(pipe.anchored))
            return null;

        qdel(pipe);
        return null;
    }
}
