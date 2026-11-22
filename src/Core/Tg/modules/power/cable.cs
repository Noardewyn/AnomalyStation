// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/cable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: all of cable.dm but the procs of later milestones and slices, which are generated live-partial stubs
// (docs/ai/specs/live-partial.txt, section P4): a rat chewing a cable (M10+), the ninja's drain (M10+), the shuttle moves (M10);
// a cyborg at the hub since silicons SI2. M3 P4 translated the cable's init, links and icons; M5
// slice 2 the coil as a stack; M5 slice 10 the cable's damage; M7 slice 1 the power networks: the cable's power procs, the
// network merges and cuts, cutting a cable with wirecutters, reading it with a multitool, laying it from the coil
// (place_turf) and the multilayer hub's layer menu and reset; the M7 shocks slice the cable's shock.

//Use this only for things that aren't a subtype of obj/machinery/power
//For things that are, override "should_have_node()" on them
public partial class datum_controller_global_vars
{
    // tg: code/modules/power/cable.dm:3  GLOBAL_LIST_INIT(wire_node_generating_types, typecacheof(list(...)))
    public DmList wire_node_generating_types;

    // tg: code/modules/power/cable.dm:3  (GLOBAL_MANAGED(wire_node_generating_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwire_node_generating_types()
    {
        wire_node_generating_types = typecacheof(Dm.list(
            typeof(obj_structure_grille),
            typeof(obj_structure_table_reinforced)
        )) as DmList;
        Dm.append(ref gvars_datum_init_order, "wire_node_generating_types");
    }
}

// tg: code/modules/power/cable.dm:17
public partial class obj_structure_cable : obj_structure
{
    // tg: code/modules/power/cable.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        Dm.append(ref GLOB.cable_list, this); //add it to the global cable list
        connect_cable();
        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE));
        RegisterSignal(this, COMSIG_RAT_INTERACT, nameof(on_rat_eat));
        if (isturf(loc))
        {
            turf turf_loc = loc as turf;
            turf_loc.add_blueprints_preround(this);
        }

        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/power/cable.dm:63
    public override void LateInitialize(params object[] __extra)
    {
        update_appearance(UPDATE_ICON);
        is_fully_initialized = TRUE;
    }

    // tg: code/modules/power/cable.dm:67
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (isobserver(user))
            Dm.append(ref dot, get_power_info());
        return dot;
    }

    // tg: code/modules/power/cable.dm:72
    [SignalHandler]
    public virtual object on_rat_eat(datum source = null, mob_living_basic_regal_rat king = null)
    {
        if (Dm.truthy(avail()))
        {
            Waiting.NoSleep(king.apply_damage(10));
            playsound(king, "sound/effects/sparks/sparks2.ogg", 100, TRUE);
        }
        Waiting.NoSleep(deconstruct());

        return COMPONENT_RAT_INTERACTED;
    }

    // tg: code/modules/power/cable.dm:9  #define UNDER_SMES -1 (a file-local define, rules 11.1)
    private const float UNDER_SMES = -1;
    // tg: code/modules/power/cable.dm:10  #define UNDER_TERMINAL 1
    private const float UNDER_TERMINAL = 1;

    ///Set the linked indicator bitflags
    // tg: code/modules/power/cable.dm:83
    public virtual void connect_cable(float? clear_before_updating = null)
    {
        clear_before_updating ??= FALSE;
        float under_thing = NONE;
        if (Dm.truthy(clear_before_updating))
            linked_dirs = NONE;

        obj_machinery_power search_parent = Dm.locate<obj_machinery_power_terminal>(loc);
        if (search_parent != null)
            under_thing = UNDER_TERMINAL;
        else
        {
            search_parent = Dm.locate<obj_machinery_power_smes>(loc);
            if (search_parent != null)
                under_thing = UNDER_SMES;
        }

        foreach (object check_dir in Dm.each<object>(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(check_dir, banned_links)))
                continue;
            turf step_turf = get_step(this, Dm.num(check_dir));
            //don't link from smes to its terminal
            if (Dm.truthy(under_thing))
            {
                if (under_thing == UNDER_SMES)
                {
                    obj_machinery_power_terminal term = Dm.locate<obj_machinery_power_terminal>(step_turf);
                    //Why null or equal to the search parent?
                    //during map init it's possible for a placed smes terminal to not have initialized to the smes yet
                    //but the cable underneath it is ready to link.
                    //I don't believe null is even a valid state for a smes terminal while the game is actually running
                    //So in the rare case that this happens, we also shouldn't connect
                    //This might break.
                    if (term != null && (term.master == null || term.master == search_parent))
                        continue;
                }
                else if (under_thing == UNDER_TERMINAL)
                {
                    obj_machinery_power_smes S = Dm.locate<obj_machinery_power_smes>(step_turf);
                    if (S != null && (S.terminal == null || S.terminal == search_parent))
                        continue;
                }
            }

            float inverse = REVERSE_DIR(Dm.num(check_dir));
            foreach (obj_structure_cable other_cable in Dm.each<obj_structure_cable>(step_turf))
            {
                if (!Dm.truthy(Dm.bitand(other_cable.cable_layer, cable_layer)) || Dm.truthy(Dm.bitand(other_cable.banned_links, inverse)))
                    continue;
                linked_dirs = Dm.num(Dm.bitor(linked_dirs, check_dir));
                other_cable.linked_dirs = Dm.bitor(other_cable.linked_dirs, inverse);

                // We will update on LateInitialize otherwise.
                if (Dm.truthy(other_cable.is_fully_initialized))
                    other_cable.update_appearance(UPDATE_ICON);
            }
        }

        if (Dm.truthy(is_fully_initialized))
            update_appearance(UPDATE_ICON);
    }

    ///Clear the linked indicator bitflags
    // tg: code/modules/power/cable.dm:134
    public virtual void disconnect_cable()
    {
        foreach (object check_dir in Dm.each<object>(GLOB.cardinals))
        {
            if (!Dm.truthy(Dm.bitand(linked_dirs, check_dir)))
                continue;
            float inverse = REVERSE_DIR(Dm.num(check_dir));
            turf check_turf = get_step(loc, Dm.num(check_dir));
            foreach (obj_structure_cable other_cable in Dm.each<obj_structure_cable>(check_turf))
            {
                if (!Dm.truthy(Dm.bitand(other_cable.cable_layer, cable_layer)))
                    continue;
                other_cable.linked_dirs = Dm.bitand(other_cable.linked_dirs, Dm.bitnot(inverse));
                if (Dm.truthy(other_cable.is_fully_initialized))
                    other_cable.update_appearance();
            }
        }
    }

    // tg: code/modules/power/cable.dm:147
    public override object Destroy(float? force = null) // called when a cable is deleted
    {
        disconnect_cable();

        if (Dm.truthy(powernet))
            Waiting.NoSleep(cut_cable_from_powernet()); // update the powernets
        Dm.remove(ref GLOB.cable_list, this); //remove it from global cable list

        return base.Destroy(force); // then go ahead and delete the cable
    }

    // tg: code/modules/power/cable.dm:156
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        var cable = Dm.@new<obj_item_stack_cable_coil>(drop_location(), 1);
        cable.set_cable_color(cable_color);
    }

    // tg: code/modules/power/cable.dm:160
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (!Dm.truthy(powernet) || !Dm.eq(damage_flag, BOMB))
        {
            await base.atom_destruction(damage_flag);
            return;
        }

        powernet.propagate_light_flicker(this);
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/power/cable.dm:167
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        if (Dm.eq(damage_flag, BOMB) && Dm.truthy(HAS_TRAIT(this, TRAIT_UNDERFLOOR)))
            damage_amount = Dm.num(damage_amount) * 0.25f;
        return await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
    }

    // tg: code/modules/power/cable.dm:172
    public virtual object get_dir_string(object links = null, object node = null)
    {
        if (!Dm.truthy(links))
            return Dm.text("l[]-noconnection", cable_layer);

        DmList dir_icon_list = Dm.list();
        foreach (object check_dir in Dm.each<object>(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(links, check_dir)))
                Dm.append(dir_icon_list, Dm.text("[]", check_dir));
        }

        string dir_string = dir_icon_list.Join("-");
        if (length(dir_icon_list) == 1 || !Dm.truthy(node))
            return Dm.text("l[]-[]", cable_layer, dir_string);
        return Dm.text("l[]-[]-node", cable_layer, dir_string);
    }

    // tg: code/modules/power/cable.dm:186
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        bool node = Dm.truthy(banned_links);
        if (!node)
        {
            foreach (obj connector in Dm.each<obj>(loc))
            {
                if (Dm.truthy(GLOB.wire_node_generating_types[connector.type]))
                {
                    node = true;
                    break;
                }
                if (connector is not obj_machinery_power power_node)
                    continue;
                if (Dm.truthy(power_node.should_have_node()))
                {
                    node = true;
                    break;
                }
            }
        }
        icon_state = get_dir_string(linked_dirs, node ? TRUE : FALSE) as string;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/power/cable.dm:202
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(banned_links))
            return dot;
        foreach (object check_dir in Dm.each<object>(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(banned_links, check_dir)))
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("l[]-clasp-[]", cable_layer, check_dir), layer, this, appearance_flags: Dm.bitor(KEEP_APART, RESET_COLOR)));
        }
        return dot;
    }

    // tg: code/modules/power/cable.dm:210
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        turf our_turf = get_turf(this);
        if (our_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
            return NONE;

        if (Dm.truthy(await shock__proc(user, 50)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(span_notice(I18n.T("[] cuts the cable.", user)), span_notice(I18n.T("You cut the cable.")));
        investigate_log(Dm.text("was cut by [] in []", key_name(usr), AREACOORD(this)), INVESTIGATE_WIRES);
        await deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/cable.dm:223
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        turf our_turf = get_turf(this);
        if (our_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
            return NONE;

        if (Dm.truthy(await shock__proc(user, 50)))
            return ITEM_INTERACT_BLOCKING;

        DmList choices = Dm.list();
        foreach (object check_dir in Dm.each(GLOB.cardinals))
        {
            if (!Dm.truthy(Dm.bitand(check_dir, Dm.bitor(linked_dirs, banned_links))))
                continue;

            if (Dm.truthy(Dm.bitand(check_dir, banned_links)))
                choices[capitalize(dir2text(check_dir))] = icon(icon, get_dir_string(Dm.bitor(linked_dirs, check_dir)));
            else
                choices[capitalize(dir2text(check_dir))] = icon(icon, get_dir_string(Dm.bitand(linked_dirs, Dm.bitnot(check_dir))));
        }

        object choice = await show_radial_menu(user, this, choices, require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await can_interact(user)) || !Dm.truthy(choice))
            return ITEM_INTERACT_BLOCKING;

        obj_item cutters = user.get_active_held_item() as obj_item;
        // DM parse: (!cutters.tool_behaviour) == TOOL_WIRECUTTER, which is never true (! binds tighter than ==)
        if (Dm.eq(Dm.not(cutters.tool_behaviour), TOOL_WIRECUTTER))
            return ITEM_INTERACT_BLOCKING;

        // Insuls taken off?
        if (Dm.truthy(await shock__proc(user, 50)))
            return ITEM_INTERACT_BLOCKING;

        object picked_dir = text2dir(choice);
        if (Dm.truthy(Dm.bitand(picked_dir, banned_links)))
            Dm.mask(ref banned_links, Dm.bitnot(picked_dir));
        else
        {
            Dm.combine(ref banned_links, picked_dir);
            obj_structure_cable other_cable = Dm.locate<obj_structure_cable>(get_step(our_turf, Dm.num(picked_dir)));
            float inverse = REVERSE_DIR(Dm.num(picked_dir));
            if (other_cable != null && Dm.truthy(Dm.bitand(other_cable.linked_dirs, inverse)))
            {
                Dm.mask(ref other_cable.linked_dirs, Dm.bitnot(inverse));
                other_cable.update_appearance(UPDATE_ICON);
            }
        }
        connect_cable(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/cable.dm:266
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        turf our_turf = get_turf(this);
        if (our_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
            return NONE;
        add_fingerprint(user);
        to_chat(user, get_power_info());
        await shock__proc(user, 5, null, 0.2f);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/cable.dm:275
    public virtual object get_power_info()
    {
        if (Dm.num(powernet?.avail) > 0)
            return span_danger(I18n.T("Total power: []\nLoad: []\nExcess power: []", display_power(powernet.avail), display_power(powernet.load), display_power(surplus())));
        else
            return span_danger(I18n.T("The cable is not powered."));
    }

    // shock the user with probability prb
    // tg: code/modules/power/cable.dm:282  (the M7 shocks slice)
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        shock_source = powernet;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }

    ////////////////////////////////////////////
    // Power related
    ///////////////////////////////////////////

    // All power generation handled in add_avail()
    // Machines should use add_load(), surplus(), avail()
    // Non-machines should use add_delayedload(), delayed_surplus(), newavail()

    // tg: code/modules/power/cable.dm:299
    public virtual void add_avail(object amount = null)
    {
        if (Dm.truthy(powernet))
            powernet.newavail += Dm.num(amount);
    }

    // tg: code/modules/power/cable.dm:303
    public virtual void add_load(object amount = null)
    {
        if (Dm.truthy(powernet))
            Dm.append(ref powernet.load, amount);
    }

    // tg: code/modules/power/cable.dm:307
    public virtual object surplus()
    {
        if (Dm.truthy(powernet))
            return clamp(powernet.avail - Dm.num(powernet.load), 0, powernet.avail);
        else
            return 0;
    }

    // tg: code/modules/power/cable.dm:313
    public virtual object avail(object amount = null)
    {
        if (Dm.truthy(powernet))
            return (Dm.truthy(amount) ? (object)Dm.num(Dm.ge(powernet.avail, amount)) : powernet.avail);
        else
            return 0;
    }

    // tg: code/modules/power/cable.dm:319
    public virtual void add_delayedload(object amount = null)
    {
        if (Dm.truthy(powernet))
            powernet.delayedload += Dm.num(amount);
    }

    // tg: code/modules/power/cable.dm:323
    public virtual object delayed_surplus()
    {
        if (Dm.truthy(powernet))
            return clamp(powernet.newavail - powernet.delayedload, 0, powernet.newavail);
        else
            return 0;
    }

    // tg: code/modules/power/cable.dm:329
    public virtual object newavail()
    {
        if (Dm.truthy(powernet))
            return powernet.newavail;
        else
            return 0;
    }

    /////////////////////////////////////////////////
    // Cable laying helpers
    ////////////////////////////////////////////////

    // merge with the powernets of power objects in the given direction
    // tg: code/modules/power/cable.dm:340
    public virtual void mergeConnectedNetworks(object direction = null)
    {
        float inverse_dir = (!Dm.truthy(direction)) ? 0 : REVERSE_DIR(Dm.num(direction)); //flip the direction, to match with the source position on its turf

        turf TB = get_step(this, Dm.num(direction));

        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(TB))
        {
            if (!Dm.truthy(C))
                continue;

            if (this == C)
                continue;

            if (!Dm.truthy(Dm.bitand(cable_layer, C.cable_layer)))
                continue;

            if (Dm.truthy(Dm.bitand(C.linked_dirs, inverse_dir))) //we've got a matching cable in the neighbor turf
            {
                if (!Dm.truthy(C.powernet)) //if the matching cable somehow got no powernet, make him one (should not happen for cables)
                {
                    datum_powernet newPN = Dm.@new<datum_powernet>();
                    newPN.add_cable(C);
                }

                if (Dm.truthy(powernet)) //if we already have a powernet, then merge the two powernets
                    merge_powernets(powernet, C.powernet);
                else
                    C.powernet.add_cable(this); //else, we simply connect to the matching cable powernet
            }
        }
    }

    // merge with the powernets of power objects in the source turf
    // tg: code/modules/power/cable.dm:367
    public virtual void mergeConnectedNetworksOnTurf()
    {
        DmList to_connect = Dm.list();
        if (!Dm.truthy(powernet)) //if we somehow have no powernet, make one (should not happen for cables)
        {
            datum_powernet newPN = Dm.@new<datum_powernet>();
            newPN.add_cable(this);
        }

        //first let's add turf cables to our powernet
        //then we'll connect machines on turf where a cable is present
        foreach (atom_movable AM in Dm.each<atom_movable>(loc))
        {
            if (AM is obj_machinery_power_apc)
            {
                obj_machinery_power_apc N = AM as obj_machinery_power_apc;
                if (!Dm.truthy(N.terminal))
                    continue; // APC are connected through their terminal

                if (Dm.eq(N.terminal.powernet, powernet)) //already connected
                    continue;

                Dm.append(ref to_connect, N.terminal); //we'll connect the machines after all cables are merged
            }
            else if (AM is obj_machinery_power) //other power machines
            {
                obj_machinery_power M = AM as obj_machinery_power;

                if (Dm.eq(M.powernet, powernet))
                    continue;

                Dm.append(ref to_connect, M); //we'll connect the machines after all cables are merged
            }
        }

        //now that cables are done, let's connect found machines
        foreach (obj_machinery_power PM in Dm.each<obj_machinery_power>(to_connect))
        {
            if (!Dm.truthy(PM.connect_to_network()))
                PM.disconnect_from_network(); //if we somehow can't connect the machine to the new powernet, remove it from the old nonetheless
        }
    }

    //////////////////////////////////////////////
    // Powernets handling helpers
    //////////////////////////////////////////////

    // tg: code/modules/power/cable.dm:403
    public virtual object get_cable_connections(object powernetless_only = null)
    {
        DmList dot = null;
        dot = Dm.list();
        turf T = get_turf(this);
        foreach (object check_dir in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(linked_dirs, check_dir)))
            {
                T = get_step(this, Dm.num(check_dir));
                foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(T))
                {
                    if (Dm.truthy(Dm.bitand(cable_layer, C.cable_layer)))
                        Dm.append(ref dot, C);
                }
            }
        }
        return dot;
    }

    // tg: code/modules/power/cable.dm:413
    public virtual object get_all_cable_connections(object powernetless_only = null)
    {
        DmList dot = null;
        dot = Dm.list();
        turf T = null;
        foreach (object check_dir in Dm.each(GLOB.cardinals))
        {
            T = get_step(this, Dm.num(check_dir));
            foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(Dm.sub(T.contents, this)))
                Dm.append(ref dot, C);
        }
        return dot;
    }

    // tg: code/modules/power/cable.dm:421
    public virtual object get_machine_connections(object powernetless_only = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (obj_machinery_power P in Dm.each<obj_machinery_power>(get_turf(this)))
        {
            if (!Dm.truthy(powernetless_only) || !Dm.truthy(P.powernet))
            {
                if (Dm.truthy(P.anchored))
                    Dm.append(ref dot, P);
            }
        }
        return dot;
    }

    // tg: code/modules/power/cable.dm:428
    public virtual void auto_propagate_cut_cable(obj O = null)
    {
        if (Dm.truthy(O) && !QDELETED(O))
        {
            datum_powernet newPN = Dm.@new<datum_powernet>();// creates a new powernet...
            propagate_network(O as obj_structure_cable, newPN);//... and propagates it to the other side of the cable
        }
    }

    //Makes a new network for the cable and propgates it. If we already have one, just die
    // tg: code/modules/power/cable.dm:434
    public virtual void propagate_if_no_network()
    {
        if (Dm.truthy(powernet))
            return;
        datum_powernet newPN = Dm.@new<datum_powernet>();
        propagate_network(this, newPN);
    }

    // cut the cable's powernet at this cable and updates the powergrid
    // tg: code/modules/power/cable.dm:441
    public virtual async DmTask cut_cable_from_powernet(float? remove = null)
    {
        remove ??= TRUE;
        if (!Dm.truthy(powernet))
            return;

        turf T1 = loc as turf;
        if (!Dm.truthy(T1))
            return;

        //clear the powernet of any machines on tile first
        foreach (obj_machinery_power P in Dm.each<obj_machinery_power>(T1))
            P.disconnect_from_network();

        DmList P_list = Dm.list();
        foreach (object dir_check in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(linked_dirs, dir_check)))
            {
                T1 = get_step(loc, Dm.num(dir_check));
                Dm.append(ref P_list, Dm.locate<obj_structure_cable>(T1));
            }
        }

        // remove the cut cable from its turf and powernet, so that it doesn't get count in propagate_network worklist
        if (Dm.truthy(remove))
            await moveToNullspace();
        powernet.remove_cable(this); //remove the cut cable from its powernet

        float first = TRUE;
        foreach (obj O in Dm.each<obj>(P_list))
        {
            if (Dm.truthy(first))
            {
                first = FALSE;
                continue;
            }
            _addtimer(Dm.@new<datum_callback>(O, nameof(auto_propagate_cut_cable), O), 0, file: "code/modules/power/cable.dm", line: 469); //so we don't rebuild the network X times when singulo/explosion destroys a line of X cables
        }
    }
}

///multilayer cable to connect different layers

// tg: code/modules/power/cable.dm:751
public partial class obj_structure_cable_multilayer : obj_structure_cable
{
    // tg: code/modules/power/cable.dm:760  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/power/cable.dm:764
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        underlays.Cut();
        mutable_appearance cable_node_3 = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/layer_cable.dmi"), "l4-1-2-4-8-node") as mutable_appearance;
        cable_node_3.color = CABLE_COLOR_BLUE;
        if (cable_node_3 != null)
            cable_node_3.alpha = Dm.truthy(Dm.bitand(cable_layer, CABLE_LAYER_3)) ? 255 : 0;
        Dm.append(underlays, cable_node_3);
        mutable_appearance cable_node_2 = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/layer_cable.dmi"), "l2-1-2-4-8-node") as mutable_appearance;
        cable_node_2.color = CABLE_COLOR_YELLOW;
        if (cable_node_2 != null)
            cable_node_2.alpha = Dm.truthy(Dm.bitand(cable_layer, CABLE_LAYER_2)) ? 255 : 0;
        Dm.append(underlays, cable_node_2);
        mutable_appearance cable_node_1 = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/layer_cable.dmi"), "l1-1-2-4-8-node") as mutable_appearance;
        cable_node_1.color = CABLE_COLOR_RED;
        if (cable_node_1 != null)
            cable_node_1.alpha = Dm.truthy(Dm.bitand(cable_layer, CABLE_LAYER_1)) ? 255 : 0;
        Dm.append(underlays, cable_node_1);
        mutable_appearance machinery_node = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/layer_cable.dmi"), "l2-noconnection") as mutable_appearance;
        machinery_node.color = "black";
        Dm.append(underlays, machinery_node);
        return dot;
    }

    // tg: code/modules/power/cable.dm:783
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        turf T = get_turf(this);
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(Dm.sub(T.contents, this)))
        {
            if (Dm.truthy(Dm.bitand(C.cable_layer, cable_layer)))
                Waiting.NoSleep(C.deconstruct()); // remove adversary cable
        }
        if (!Dm.truthy(mapload))
            auto_propagate_cut_cable(this);

        update_appearance();
        return dot;
    }

    // tg: code/modules/power/cable.dm:794
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        Dm.append(ref dot, await base.examine(user, __extra));
        Dm.append(ref dot, span_notice(I18n.T("L1:[].", Dm.truthy(Dm.bitand(cable_layer, CABLE_LAYER_1)) ? I18n.T("Connect") : I18n.T("Disconnect"))));
        Dm.append(ref dot, span_notice(I18n.T("L2:[].", Dm.truthy(Dm.bitand(cable_layer, CABLE_LAYER_2)) ? I18n.T("Connect") : I18n.T("Disconnect"))));
        Dm.append(ref dot, span_notice(I18n.T("L3:[].", Dm.truthy(Dm.bitand(cable_layer, CABLE_LAYER_3)) ? I18n.T("Connect") : I18n.T("Disconnect"))));
        return dot;
    }

    // tg: code/modules/power/cable.dm:805
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user))
            return null;
        if (!Dm.truthy(GLOB.hub_radial_layer_list))
            GLOB.hub_radial_layer_list = Dm.list(
                Dm.kv("Layer 1", Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-red")),
                Dm.kv("Layer 2", Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-yellow")),
                Dm.kv("Layer 3", Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-blue"))
                );

        object layer_result = await show_radial_menu(user, this, GLOB.hub_radial_layer_list, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await check_menu(user as mob_living)))
            return null;
        object CL = null;
        object sw__1 = layer_result;
        if (Dm.eq(sw__1, "Layer 1"))
        {
            CL = CABLE_LAYER_1;
            to_chat(user, span_warning(I18n.T("You toggle L1 connection.")));
        }
        else if (Dm.eq(sw__1, "Layer 2"))
        {
            CL = CABLE_LAYER_2;
            to_chat(user, span_warning(I18n.T("You toggle L2 connection.")));
        }
        else if (Dm.eq(sw__1, "Layer 3"))
        {
            CL = CABLE_LAYER_3;
            to_chat(user, span_warning(I18n.T("You toggle L3 connection.")));
        }

        await cut_cable_from_powernet(FALSE);

        disconnect_cable();

        Dm.bitxor_assign(ref cable_layer, CL);

        connect_cable(TRUE);

        await Reload();
        return null;
    }

    // tg: code/modules/power/cable.dm:840
    public virtual async DmTask<object> check_menu(mob_living user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return FALSE;
        }
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    ///Reset powernet in this hub.
    // tg: code/modules/power/cable.dm:851
    public virtual async DmTask Reload()
    {
        turf T = get_turf(this);
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(Dm.sub(T.contents, this)))
        {
            if (Dm.truthy(Dm.bitand(C.cable_layer, cable_layer)))
                await C.deconstruct(); // remove adversary cable
        }
        auto_propagate_cut_cable(this); // update the powernets
    }

    // tg: code/modules/power/cable.dm:858
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("You push the reset button.")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(Reload)), 10, TIMER_UNIQUE, file: "code/modules/power/cable.dm", line: 860); //spam protect
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/power/cable.dm:802
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        await attack_hand(user);
        return null;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/power/cable.dm:800  GLOBAL_LIST(hub_radial_layer_list)
    public DmList hub_radial_layer_list;

    // tg: code/modules/power/cable.dm:800  (GLOBAL_UNMANAGED(hub_radial_layer_list), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalhub_radial_layer_list()
    {
        return;
    }
}

///////////////////////////////////////////////
// The cable coil object, used for laying cable
///////////////////////////////////////////////

////////////////////////////////
// Definitions
////////////////////////////////

// tg: code/modules/power/cable.dm:481
public partial class obj_item_stack_cable_coil : obj_item_stack
{
    // tg: code/modules/power/cable.dm:479  #define CABLE_RESTRAINTS_COST 15 (a file-local define, rules 11.4)
    private const float CABLE_RESTRAINTS_COST = 15;

    // tg: code/modules/power/cable.dm:515
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        float? merge = Dm.arg<float?>(__extra, 1);
        DmList mat_override = Dm.arg<DmList>(__extra, 2);
        float? mat_amt = Dm.arg<float?>(__extra, 3);
        merge ??= TRUE;
        mat_amt ??= 1;
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, merge, mat_override, mat_amt));
        pixel_x = base_pixel_x + rand(-2, 2);
        pixel_y = base_pixel_y + rand(-2, 2);

        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));

        update_appearance();
        return dot;
    }

    // tg: code/modules/power/cable.dm:524
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_copper), 2));
    }

    // tg: code/modules/power/cable.dm:527
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, I18n.T("<b>Use it in hand</b> to change the layer you are placing on, amongst other things."));
        return dot;
    }

    // tg: code/modules/power/cable.dm:531
    public override object update_name(float? updates = null)
    {
        if (Dm.truthy(novariants))
            return null;
        object dot = base.update_name(updates);
        name = I18n.T("cable []", (Dm.num(amount) < 3) ? I18n.T("piece") : I18n.T("coil"));
        return dot;
    }

    // tg: code/modules/power/cable.dm:537
    public override object update_desc(float? updates = null)
    {
        if (Dm.truthy(novariants))
            return null;
        object dot = base.update_desc(updates);
        desc = I18n.T("A [] of insulated power cable.", (Dm.num(amount) < 3) ? I18n.T("piece") : I18n.T("coil"));
        return dot;
    }

    // tg: code/modules/power/cable.dm:543
    public virtual void set_cable_color(object new_color = null)
    {
        color = GLOB.cable_colors[new_color];
        cable_color = (string)new_color;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/power/cable.dm:548
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(novariants))
            return null;
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.num(amount) < 3 ? amount : "");
        inhand_icon_state = Dm.text("coil_[]", cable_color);
        return dot;
    }

    // tg: code/modules/power/cable.dm:555
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(locate_type(typeof(obj_structure_chair_stool), get_turf(user))))
            user.visible_message(span_suicide(I18n.T("[] is making a noose with []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        else
            user.visible_message(span_suicide(I18n.T("[] is strangling []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return OXYLOSS;
    }

    // tg: code/modules/power/cable.dm:562
    public virtual async DmTask<object> check_menu(mob_living user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return FALSE;
        }
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/power/cable.dm:572
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user))
            return null;

        image restraints_icon = Dm.image(icon: Dm.resource("icons/obj/weapons/restraints.dmi"), icon_state: "cuff");
        restraints_icon.maptext = MAPTEXT(Dm.text("<span []>[]</span>", Dm.num(amount) >= CABLE_RESTRAINTS_COST ? "" : "style='color: red'", CABLE_RESTRAINTS_COST));
        restraints_icon.color = color;

        DmList radial_menu = Dm.list(
            Dm.kv("Layer 1", Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-red")),
            Dm.kv("Layer 2", Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-yellow")),
            Dm.kv("Layer 3", Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-blue")),
            Dm.kv("Multilayer cable hub", Dm.image(icon: Dm.resource("icons/obj/pipes_n_cables/structures.dmi"), icon_state: "cable_bridge")),
            Dm.kv("Multi Z layer cable hub", Dm.image(icon: Dm.resource("icons/obj/pipes_n_cables/structures.dmi"), icon_state: "cablerelay-broken-cable")),
            Dm.kv("Cable restraints", restraints_icon)
        );

        object layer_result = await show_radial_menu(user, this, radial_menu, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await check_menu(user as mob_living)))
            return null;
        object sw__1 = layer_result;
        if (Dm.eq(sw__1, "Layer 1"))
        {
            icon = Dm.initial(this, (atom a) => a.icon);
            novariants = FALSE;
            set_cable_color(CABLE_COLOR_RED);
            target_type = typeof(obj_structure_cable_layer1);
            target_layer = CABLE_LAYER_1;
        }
        else if (Dm.eq(sw__1, "Layer 2"))
        {
            icon = Dm.initial(this, (atom a) => a.icon);
            novariants = FALSE;
            set_cable_color(CABLE_COLOR_YELLOW);
            target_type = typeof(obj_structure_cable);
            target_layer = CABLE_LAYER_2;
        }
        else if (Dm.eq(sw__1, "Layer 3"))
        {
            icon = Dm.initial(this, (atom a) => a.icon);
            novariants = FALSE;
            set_cable_color(CABLE_COLOR_BLUE);
            target_type = typeof(obj_structure_cable_layer3);
            target_layer = CABLE_LAYER_3;
        }
        else if (Dm.eq(sw__1, "Multilayer cable hub"))
        {
            name = I18n.T("multilayer cable hub");
            desc = I18n.T("A multilayer cable hub.");
            icon = Dm.resource("icons/obj/pipes_n_cables/structures.dmi");
            icon_state = "cable_bridge";
            novariants = TRUE;
            set_cable_color(CABLE_COLOR_WHITE);
            target_type = typeof(obj_structure_cable_multilayer);
            target_layer = CABLE_LAYER_2;
        }
        else if (Dm.eq(sw__1, "Multi Z layer cable hub"))
        {
            name = I18n.T("multi z layer cable hub");
            desc = I18n.T("A multi-z layer cable hub.");
            icon = Dm.resource("icons/obj/pipes_n_cables/structures.dmi");
            icon_state = "cablerelay-broken-cable";
            novariants = TRUE;
            set_cable_color(CABLE_COLOR_WHITE);
            target_type = typeof(obj_structure_cable_multilayer_multiz);
            target_layer = CABLE_LAYER_2;
        }
        else if (Dm.eq(sw__1, "Cable restraints"))
        {
            if (Dm.num(amount) >= CABLE_RESTRAINTS_COST)
            {
                if (Dm.truthy(await use(CABLE_RESTRAINTS_COST)))
                {
                    obj_item_restraints_handcuffs_cable restraints = Dm.@new<obj_item_restraints_handcuffs_cable>(null, cable_color);
                    await user.put_in_hands(restraints);
                }
            }
        }
        update_appearance();
        return null;
    }


    ///////////////////////////////////
    // General procedures
    ///////////////////////////////////
    //you can use wires to heal robotics

    // tg: code/modules/power/cable.dm:642
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!ishuman(interacting_with))
            return NONE;

        if (Dm.truthy(user.combat_mode))
            return NONE;

        return await try_heal_loop(interacting_with, user);
    }

    // tg: code/modules/power/cable.dm:651
    public virtual async DmTask<object> try_heal_loop(atom interacting_with = null, mob_living user = null, float? repeating = null)
    {
        repeating ??= FALSE;
        mob_living_carbon_human attacked_humanoid = interacting_with as mob_living_carbon_human;

        // if(astype(attacked_humanoid.w_uniform, /obj/item/clothing/under)?.repair_sensors(user)) return ITEM_INTERACT_SUCCESS (:654-655):
        // the suit sensors' repair is not translated (suit sensors, M9), so it repairs nothing and the robotic limb check runs.
        Dm.Deferred("code/modules/power/cable.dm:654", "try_heal_loop: repairing a jumpsuit's broken suit sensors (suit sensors, M10+)");

        obj_item_bodypart affecting = attacked_humanoid.get_bodypart((string)check_zone(user.zone_selected)) as obj_item_bodypart;
        if (affecting == null || !Dm.truthy(IS_ROBOTIC_LIMB(affecting)))
            return NONE;

        if (!Dm.truthy(affecting.burn_dam))
        {
            balloon_alert(user, I18n.T("limb not damaged"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] starts to fix some of the wires in [] [].", user, attacked_humanoid == user ? user.p_their() : I18n.T("[]'s", attacked_humanoid), affecting.name)),
            span_notice(I18n.T("You start fixing some of the wires in [] [].", attacked_humanoid == user ? I18n.T("your") : I18n.T("[]'s", attacked_humanoid), affecting.name)));

        float use_delay = Dm.truthy(repeating) ? 1 * SECONDS : 0;
        if (user == attacked_humanoid)
            use_delay = 5 * SECONDS;

        if (!Dm.truthy(await do_after(user, use_delay, attacked_humanoid)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await attacked_humanoid.item_heal(user, brute_heal: 0, burn_heal: 15, heal_message_brute: "dents", heal_message_burn: "burnt wires", required_bodytype: BODYTYPE_ROBOTIC)))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(await use(1)) && Dm.num(amount) > 0)
            Waiting.Detach(try_heal_loop(interacting_with, user, TRUE));

        return ITEM_INTERACT_SUCCESS;
    }

    ///////////////////////////////////////////////
    // Cable laying procedures
    //////////////////////////////////////////////

    // called when cable_coil is clicked on a turf
    // tg: code/modules/power/cable.dm:688
    public virtual async DmTask<object> place_turf(turf target_turf = null, mob user = null, object dirnew = null)
    {
        if (!isturf(user.loc))
            return null;

        if (!isturf(target_turf) || target_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE || !Dm.truthy(target_turf.can_have_cabling()))
        {
            to_chat(user, span_warning(I18n.T("You can only lay cables on catwalks and plating!")));
            return null;
        }

        if (Dm.num(get_amount()) < 1) // Out of cable
        {
            to_chat(user, span_warning(I18n.T("There is no cable left!")));
            return null;
        }

        if (get_dist(target_turf, user) > 1) // Too far
        {
            to_chat(user, span_warning(I18n.T("You can't lay cable at a place that far away!")));
            return null;
        }

        foreach (obj_structure_cable old_cable in Dm.each<obj_structure_cable>(target_turf))
        {
            if (Dm.truthy(Dm.bitand(old_cable.cable_layer, target_layer)))
            {
                to_chat(user, span_warning(I18n.T("There's already a cable at that position!")));
                return null;
            }
        }

        obj_structure_cable new_cable = await Dm.new_async(target_type, target_turf) as obj_structure_cable;

        //create a new powernet with the cable, if needed it will be merged later
        datum_powernet new_powernet = Dm.@new<datum_powernet>();
        new_powernet.add_cable(new_cable);

        foreach (object dir_check in Dm.each(GLOB.cardinals))
            new_cable.mergeConnectedNetworks(dir_check); //merge the powernet with adjacents powernets
        new_cable.mergeConnectedNetworksOnTurf(); //merge the powernet with on turf powernets

        await use(1);

        if (Dm.truthy(new_cable.powernet.avail) && Dm.truthy(await new_cable.shock__proc(user as mob_living, 50)) && prob(50))
            await new_cable.deconstruct();

        return new_cable;
    }
}

// tg: code/modules/power/cable.dm:732
public partial class obj_item_stack_cable_coil_cut : obj_item_stack_cable_coil
{
    // tg: code/modules/power/cable.dm:738
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        float? merge = Dm.arg<float?>(__extra, 1);
        DmList mat_override = Dm.arg<DmList>(__extra, 2);
        float? mat_amt = Dm.arg<float?>(__extra, 3);
        merge ??= TRUE;
        mat_amt ??= 1;
        if (!Dm.truthy(amount))
            amount = rand(1, 2);
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, merge, mat_override, mat_amt));
        pixel_x = base_pixel_x + rand(-2, 2);
        pixel_y = base_pixel_y + rand(-2, 2);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/power/cable.dm:286 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_structure_cable : obj_structure
{
    // tg: code/modules/power/cable.dm:286
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct();
        return null;
    }
}
