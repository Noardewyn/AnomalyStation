// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/ducts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (E15 added the duct's connection to a plumbing machine and the duct stack) but the duct stack's add_context
// and add_item_context (live-partial, screentips).

// tg: code/modules/plumbing/ducts.dm:1
public partial class obj_machinery_duct : obj_machinery
{
    // tg: code/modules/plumbing/ducts.dm:17
    [DmParams("mapload", "color_of_duct", "layer_of_duct")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object color_of_duct = Dm.arg(__extra, 0);
        object layer_of_duct = Dm.arg(__extra, 1);
        object dot = null;
        if (Dm.truthy(GLOB.plumbing_layer_names[Dm.text("[]", layer_of_duct)]))
            duct_layer = Dm.num(layer_of_duct);

        if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(maptest_log_mapping) is FALSE in production (rules 1.7)
            Dm.NotTranslated("code/modules/plumbing/ducts.dm:21", "Initialize: the mapping test's overlap log (ducting_layer_check, log_mapping; plumbing, M8)");

        dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(GLOB.pipe_color_name[color_of_duct]))
            duct_color = color_of_duct as string;
        Waiting.NoSleep(add_atom_colour(duct_color, FIXED_COLOUR_PRIORITY));

        object offset = null;
        switch (duct_layer)
        {
            case FIRST_DUCT_LAYER:
                offset = -10;
                break;
            case SECOND_DUCT_LAYER:
                offset = -5;
                break;
            case THIRD_DUCT_LAYER:
                offset = 0;
                break;
            case FOURTH_DUCT_LAYER:
                offset = 5;
                break;
            case FIFTH_DUCT_LAYER:
                offset = 10;
                break;
        }
        pixel_x = Dm.num(offset);
        pixel_y = Dm.num(offset);
        layer = Dm.num(Dm.initial(this, (atom p) => p.layer)) + duct_layer * 0.0003f;

        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE));

        register_context();
        return dot;
    }

    // tg: code/modules/plumbing/ducts.dm:58
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        //can be initialized already during map init by its neighbours
        if (net == null)
            net = Dm.@new<datum_ductnet>(this);

        LAZYINITLIST(ref neighbours);
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            //we have already connected. happens in circular connections
            float connected = FALSE;
            foreach (atom_movable neighbour in Dm.each_cast<atom_movable>(neighbours))
            {
                if (Dm.truthy(Dm.bitand(direction, neighbours[neighbour])))
                {
                    connected = TRUE;
                    break;
                }
            }
            if (Dm.truthy(connected))
                continue;

            foreach (atom_movable plumbable in Dm.each<atom_movable>(get_step(this, Dm.num(direction))))
            {
                float opposite_dir = REVERSE_DIR(Dm.num(direction));

                if (plumbable is obj_machinery_duct)
                {
                    obj_machinery_duct other = (obj_machinery_duct)plumbable;

                    //must be same duct color
                    if ((duct_color != other.duct_color) && !(duct_color == ATMOS_COLOR_OMNI || other.duct_color == ATMOS_COLOR_OMNI))
                        continue;
                    //must be same duct layer
                    if (!Dm.truthy(Dm.bitand(duct_layer, other.duct_layer)))
                        continue;

                    //connect ductnets
                    if (other.net == null) //will be null only for map loaded ducts
                    {
                        Dm.append(ref net.ducts, other);
                        other.net = net;
                    }
                    else if (net != other.net) //merge the nets
                    {
                        datum_ductnet othernet = other.net;
                        //Take all its suppliers & demanders
                        Dm.combine(ref net.suppliers, othernet.suppliers);
                        Dm.combine(ref net.demanders, othernet.demanders);
                        foreach (datum_component_plumbing component in Dm.each_cast<datum_component_plumbing>(Dm.add(othernet.suppliers, othernet.demanders)))
                        {
                            // tg types this loop var /obj/machinery/duct, but component.ducts is keyed by direction text: the
                            // var is only used as a key, so it is an untyped object here (an each_cast would raise where DM does not).
                            foreach (object duct in Dm.each(component.ducts))
                            {
                                if (Dm.eq(component.ducts[duct], othernet))
                                    component.ducts[duct] = net;
                            }
                        }
                        othernet.suppliers.Cut();
                        othernet.demanders.Cut();

                        //Take all its ducts
                        Dm.combine(ref net.ducts, othernet.ducts);
                        foreach (obj_machinery_duct duct in Dm.each_cast<obj_machinery_duct>(othernet.ducts))
                            duct.net = net;

                        //destory it
                        qdel(othernet);
                    }

                    //connecting us to duct
                    neighbours[other] = direction;
                    //connecting duct to us
                    LAZYADDASSOC(ref other.neighbours, this, opposite_dir);
                    other.update_appearance(UPDATE_ICON);

                    continue;
                }

                foreach (datum_component_plumbing plumbing in Dm.each_cast<datum_component_plumbing>(plumbable.GetComponents(typeof(datum_component_plumbing))))
                {
                    //not anchored
                    if (!Dm.truthy(plumbing.active()))
                        continue;

                    //not on the same layer
                    if (!Dm.truthy(Dm.bitand(duct_layer, plumbing.ducting_layer)))
                        continue;

                    //does the duct backend connect to either supplier or demander
                    if (!Dm.truthy(Dm.bitand(opposite_dir, Dm.bitor(plumbing.supply_connects, plumbing.demand_connects))))
                        continue;

                    //connect duct to plumber
                    if (!Dm.truthy(net.add_plumber(plumbing, opposite_dir)))
                        continue;

                    //assign neighbour
                    neighbours[plumbing.parent] = direction;
                }
            }
        }

        update_appearance(UPDATE_ICON);
    }

    ///we disconnect ourself from our neighbours. we also destroy our ductnet and tell our neighbours to make a new one
    // tg: code/modules/plumbing/ducts.dm:144
    [DmParams()]
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        obj_item_stack_ducts duct_stack = Dm.@new<obj_item_stack_ducts>(drop_location());
        duct_stack.duct_color = duct_color;
        duct_stack.duct_layer = duct_layer;
        await duct_stack.add_atom_colour(duct_color, FIXED_COLOUR_PRIORITY);
        return null;
    }

    ///Removes duct from ductnet
    // tg: code/modules/plumbing/ducts.dm:151
    public virtual void disconnect()
    {
        //remove ourself from the duct
        Dm.remove(ref net.ducts, this);
        if (!Dm.truthy(net.ducts.len))
            qdel(net); //destroy the pipeline. Suppliers aren't important if there aren't any ducts left
        net = null;
    }

    // tg: code/modules/plumbing/ducts.dm:158
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        //object was early deleted
        if (net == null)
            return base.Destroy(force);

        // tg writes list(src = TRUE): a bare identifier key is its text (rules 6.1), so the key is "src", not the duct.
        DmList visited = Dm.list(Dm.kv("src", TRUE));
        while (Dm.truthy(neighbours.len))
        {
            atom_movable neighbour = popleft(neighbours) as atom_movable;

            //disconnect ourself from our neighbours
            obj_machinery_duct pipe = neighbour as obj_machinery_duct;
            if (pipe != null)
            {
                Dm.remove(ref pipe.neighbours, this);
                pipe.update_appearance(UPDATE_ICON);
            }

            //find every node that can be reached from our neighbour making sure to not revisit it again in circles
            if (Dm.truthy(visited[neighbour]))
                continue;
            datum_ductnet newnet = null;
            DmList queue = Dm.list(neighbour);
            while (Dm.truthy(queue.len))
            {
                atom_movable node = popleft(queue) as atom_movable;
                if (Dm.truthy(visited[node]))
                    continue;

                //visit all neighbours of this pipe as well
                pipe = node as obj_machinery_duct;
                if (pipe != null)
                {
                    //assign to new pipenet
                    pipe.disconnect();
                    if (newnet == null)
                        newnet = Dm.@new<datum_ductnet>();
                    Dm.append(ref newnet.ducts, pipe);
                    pipe.net = newnet;

                    //go through its neighbours as well
                    foreach (atom_movable subnode in Dm.each<atom_movable>(pipe.neighbours))
                        Dm.append(ref queue, subnode);

                    visited[node] = TRUE;
                    continue;
                }

                //assign machines to new network
                foreach (datum_component_plumbing plumbing in Dm.each_cast<datum_component_plumbing>(node.GetComponents(typeof(datum_component_plumbing))))
                {
                    //disconnect old net
                    foreach (object dirtext in Dm.each(plumbing.ducts))
                    {
                        if (Dm.eq(plumbing.ducts[dirtext], net))
                            net.remove_plumber(plumbing);
                    }
                    //assign new net
                    if (newnet != null)
                    {
                        foreach (obj_machinery_duct pipe__it in Dm.each_cast<obj_machinery_duct>(newnet.ducts))
                        {
                            pipe = pipe__it;
                            object dir = pipe.neighbours[node];
                            if (Dm.truthy(dir))
                                newnet.add_plumber(plumbing, REVERSE_DIR(Dm.num(dir)));
                        }
                    }
                }
            }
        }

        disconnect();

        return base.Destroy(force);
    }

    // tg: code/modules/plumbing/ducts.dm:217
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = null;
        dot = NONE;
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Destroy duct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/plumbing/ducts.dm:223
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Its current color and layer are [] and []. Use in-hand to change.", GLOB.pipe_color_name[duct_color], GLOB.plumbing_layer_names[Dm.text("[]", duct_layer)])));
        Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("wrenched")))));
        return dot;
    }

    // tg: code/modules/plumbing/ducts.dm:228
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string temp_icon = Dm.initial(this, (atom p) => p.icon_state) as string;

        //compute connections
        float connects = NONE;
        foreach (object neighbour in Dm.each(neighbours))
            Dm.combine(ref connects, Dm.num(neighbours[neighbour]));

        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            switch (Dm.num(Dm.bitand(direction, connects)))
            {
                case NORTH:
                    temp_icon += "_n";
                    break;
                case SOUTH:
                    temp_icon += "_s";
                    break;
                case EAST:
                    temp_icon += "_e";
                    break;
                case WEST:
                    temp_icon += "_w";
                    break;
            }
        }
        icon_state = temp_icon;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/plumbing/ducts.dm:249  (I can also be the RPD)
    [DmParams("user", "wrench")]
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item wrench = null)
    {
        wrench.play_tool_sound(this);

        user.visible_message(
            I18n.T("[] ununfastens \\the [].", user, this),
            span_notice(I18n.T("You unfasten \\the [].", this)),
            span_hear(I18n.T("You hear ratcheting.")));

        await deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/plumbing/ducts.dm:260
public partial class obj_item_stack_ducts : obj_item_stack
{
    // tg: code/modules/plumbing/ducts.dm:278
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        //when wrench is over us
        register_context();

        //when we are over pipe
        register_item_context();
        return dot;
    }

    // tg: code/modules/plumbing/ducts.dm:287  add_context: live-partial (screentips).

    // tg: code/modules/plumbing/ducts.dm:293  add_item_context: live-partial (screentips).

    // tg: code/modules/plumbing/ducts.dm:299
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Its current color and layer are [] and []. Use in-hand to change.", GLOB.pipe_color_name[duct_color], GLOB.plumbing_layer_names[Dm.text("[]", duct_layer)])));
        Dm.append(ref dot, span_notice(I18n.T("Place on ground & [] to create duct.", EXAMINE_HINT(I18n.T("wrench")))));
        return dot;
    }

    // tg: code/modules/plumbing/ducts.dm:304
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object new_layer = await tgui_input_list(user, "Select a layer", "Layer", GLOB.plumbing_layers, GLOB.plumbing_layer_names[Dm.text("[]", duct_layer)]);
        if (!Dm.truthy(user.is_holding(this)))
            return null;
        if (Dm.truthy(new_layer))
            duct_layer = Dm.num(GLOB.plumbing_layers[new_layer]);
        object new_color = await tgui_input_list(user, "Select a color", "Color", GLOB.pipe_paint_colors, GLOB.pipe_color_name[duct_color]);
        if (!Dm.truthy(user.is_holding(this)))
            return null;
        if (Dm.truthy(new_color))
        {
            duct_color = GLOB.pipe_paint_colors[new_color] as string;
            await add_atom_colour(duct_color, FIXED_COLOUR_PRIORITY);
        }
        return null;
    }

    // tg: code/modules/plumbing/ducts.dm:317
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return await check_attach_turf(loc as turf);
    }

    // tg: code/modules/plumbing/ducts.dm:320
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // Turn into a duct stack and then merge to the in-hand stack.
        if (istype(interacting_with, typeof(obj_machinery_duct)))
        {
            if (Dm.eq(amount, max_amount))
            {
                balloon_alert(user, "stack full!");
                return ITEM_INTERACT_FAILURE;
            }
            qdel(interacting_with);
            await add(1);
            return ITEM_INTERACT_SUCCESS;
        }

        return await check_attach_turf(interacting_with as turf, user);
    }

    // tg: code/modules/plumbing/ducts.dm:332
    public virtual async DmTask<object> check_attach_turf(turf open_turf = null, mob user = null)
    {
        object dot = NONE;
        if (isopenturf(open_turf))
        {
            datum overlap = ducting_layer_check(open_turf, duct_layer) as datum;
            if (overlap != null)
            {
                if (user != null)
                    open_turf.balloon_alert(user, Dm.text("overlapping [] detected!", istype(overlap, typeof(obj_machinery_duct)) ? "duct" : "machine"));
                return ITEM_INTERACT_FAILURE;
            }

            Dm.@new<obj_machinery_duct>(open_turf, duct_color, duct_layer);
            playsound(open_turf, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            await use(1);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }
}
