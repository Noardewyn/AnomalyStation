// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/power.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The M7 shocks slice translated get_powernet_info_from_source (:442) and electrocute_mob (:478). M3 P3 translated the machinery procs that M3's machines reach (powered, use_energy, the static power,
// power_change and the update_appearance hook); M7's lights slice charge_cell; M7 slice 1 the power machines
// (/obj/machinery/power), the powernet helpers and the global procs that build and merge the networks (propagate_network,
// merge_powernets, get_cable_node); M7 slice 3 (the APC) the APC's grid and cell: available_energy (:135), use_energy's grid
// and cell part (:173-187), directly_use_energy (:196) and use_power_from_net (:220).

//////////////////////////////
// POWER MACHINERY BASE CLASS
//////////////////////////////

/////////////////////////////
// Definitions
/////////////////////////////

// tg: code/modules/power/power.dm:9
[DmPath("/obj/machinery/power")]
public partial class obj_machinery_power : obj_machinery
{
    // tg: code/modules/power/power.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (isturf(loc))
        {
            turf turf_loc = loc as turf;
            turf_loc.add_blueprints_preround(this);
        }
        return dot;
    }

    // tg: code/modules/power/power.dm:31
    public override object Destroy(float? force = null)
    {
        disconnect_from_network();
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.update_cable_icons_on_turf), get_turf(this)), 0.3f * SECONDS, file: "code/modules/power/power.dm", line: 33);
        return base.Destroy(force);
    }

    ///////////////////////////////
    // General procedures
    //////////////////////////////

    // common helper procs for all power machines
    // All power generation handled in add_avail()
    // Machines should use add_load(), surplus(), avail()
    // Non-machines should use add_delayedload(), delayed_surplus(), newavail()

    //override this if the machine needs special functionality for making wire nodes appear, ie emitters, generators, etc.
    // tg: code/modules/power/power.dm:46
    public virtual object should_have_node()
    {
        return FALSE;
    }

    // tg: code/modules/power/power.dm:49
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(can_change_cable_layer))
        {
            if (!QDELETED(powernet))
                Dm.append(ref dot, span_notice(I18n.T("It's operating on the [].", LOWER_TEXT(GLOB.cable_layer_to_name[Dm.text("[]", cable_layer)]))));
            else
                Dm.append(ref dot, span_warning(I18n.T("It's disconnected from the [].", LOWER_TEXT(GLOB.cable_layer_to_name[Dm.text("[]", cable_layer)]))));
            Dm.append(ref dot, span_notice(I18n.T("Its power line can be changed with a [].", EXAMINE_HINT(I18n.T("multitool")))));
        }
        return dot;
    }

    // tg: code/modules/power/power.dm:58
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(can_change_cable_layer))
            return await cable_layer_act(user, tool);
        return null;
    }

    // tg: code/modules/power/power.dm:62
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await multitool_act(user, tool);
    }

    /// Called on multitool_act when we can change cable layers, override to add more conditions
    // tg: code/modules/power/power.dm:66
    public virtual async DmTask<object> cable_layer_act(mob_living user = null, obj_item tool = null)
    {
        object choice = await tgui_input_list(user, I18n.T("Select Power Line For Operation"), I18n.T("Select Cable Layer"), GLOB.cable_name_to_layer);
        if (choice == null || QDELETED(this) || QDELETED(user) || QDELETED(tool) || !Dm.truthy(await user.Adjacent(this)) || !Dm.truthy(user.is_holding(tool)))
            return ITEM_INTERACT_BLOCKING;

        cable_layer = Dm.num(GLOB.cable_name_to_layer[choice]);
        balloon_alert(user, I18n.T("now operating on the []", choice));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/power.dm:75
    public virtual object add_avail(object amount = null)
    {
        if (Dm.truthy(powernet))
        {
            ((datum_powernet)powernet).newavail += Dm.num(amount);
            return TRUE;
        }
        else
            return FALSE;
    }

    // tg: code/modules/power/power.dm:82
    public virtual void add_load(object amount = null)
    {
        if (Dm.truthy(powernet))
            Dm.append(ref ((datum_powernet)powernet).load, amount);
    }

    // tg: code/modules/power/power.dm:86
    public virtual object surplus()
    {
        if (Dm.truthy(powernet))
            return clamp(((datum_powernet)powernet).avail - Dm.num(((datum_powernet)powernet).load), 0, ((datum_powernet)powernet).avail);
        else
            return 0;
    }

    // tg: code/modules/power/power.dm:92
    public virtual object avail(object amount = null)
    {
        if (Dm.truthy(powernet))
            return (Dm.truthy(amount) ? (object)Dm.num(Dm.ge(((datum_powernet)powernet).avail, amount)) : ((datum_powernet)powernet).avail);
        else
            return 0;
    }

    // tg: code/modules/power/power.dm:98
    public virtual void add_delayedload(object amount = null)
    {
        if (Dm.truthy(powernet))
            ((datum_powernet)powernet).delayedload += Dm.num(amount);
    }

    // tg: code/modules/power/power.dm:102
    public virtual object delayed_surplus()
    {
        if (Dm.truthy(powernet))
            return clamp(((datum_powernet)powernet).newavail - ((datum_powernet)powernet).delayedload, 0, ((datum_powernet)powernet).newavail);
        else
            return 0;
    }

    // tg: code/modules/power/power.dm:108
    public virtual object newavail()
    {
        if (Dm.truthy(powernet))
            return ((datum_powernet)powernet).newavail;
        else
            return 0;
    }

    // tg: code/modules/power/power.dm:114
    public virtual async DmTask disconnect_terminal() // machines without a terminal will just return, no harm no fowl.
    {
        return;
    }
}

// tg: code/modules/power/power.dm:119
public partial class obj_machinery : obj
{
    // returns true if the area has power on given channel (or doesn't require power).
    // defaults to power_channel
    // tg: code/modules/power/power.dm:119
    public virtual object powered__proc(object chan = null, float? ignore_use_power = null)
    {
        chan ??= power_channel;
        ignore_use_power ??= FALSE;
        if (!Dm.truthy(use_power) && !Dm.truthy(ignore_use_power))
            return TRUE;

        area A = get_area(this) as area; // make sure it's in an area
        if (A == null)
            return FALSE; // if not, then not powered

        return A.powered(chan); // return power status of the area
    }

    /**
     * Returns the available energy from the apc's cell and grid that can be used.
     * Args:
     * - consider_cell: Whether to count the energy from the APC's cell or not.
     * Returns: The available energy the machine can access from the APC.
     */
    // tg: code/modules/power/power.dm:135
    public virtual object available_energy(float? consider_cell = null)
    {
        consider_cell ??= TRUE;
        area home = get_area(this) as area;

        if (home == null)
            return FALSE;
        if (!Dm.truthy(home.requires_power))
            return INFINITY;

        obj_machinery_power_apc local_apc = home.apc;
        if (local_apc == null)
            return FALSE;

        return Dm.truthy(consider_cell) ? local_apc.available_energy() : local_apc.surplus();
    }

    /**
     * Draws energy from the APC. Will use excess energy from the APC's connected grid,
     * then use energy from the APC's cell if there wasn't enough energy from the grid, unless ignore_apc is true.
     * Args:
     * - amount: The amount of energy to use.
     * - channel: The power channel to use.
     * - ignore_apc: If true, do not consider the APC's cell when demanding energy.
     * - force: If true and if there isn't enough energy, consume the remaining energy. Returns 0 if false and there isn't enough energy.
     * Returns: The amount of energy used.
     */
    // tg: code/modules/power/power.dm:159
    public virtual async DmTask<object> use_energy(object amount = null, object channel = null, float? ignore_apc = null, float? force = null)
    {
        channel ??= power_channel;
        ignore_apc ??= FALSE;
        force ??= TRUE;
        if (Dm.le(amount, 0)) //just in case
            return FALSE;
        area home = get_area(this) as area;

        if (home == null)
            return FALSE; //apparently space isn't an area
        if (!Dm.truthy(home.requires_power))
            return amount; //Shuttles get free power, don't ask why

        obj_machinery_power_apc local_apc = home.apc;
        if (local_apc == null || !Dm.truthy(local_apc.operating))
            return FALSE;

        // Surplus from the grid.
        object surplus = local_apc.surplus();
        object grid_used = min(surplus, amount);
        object apc_used = 0;
        if (Dm.gt(amount, grid_used) && !Dm.truthy(ignore_apc) && !QDELETED(local_apc.cell)) // Use from the APC's cell if there isn't enough energy from the grid.
            apc_used = await local_apc.cell.use(Dm.sub(amount, grid_used), force);

        if (!Dm.truthy(force) && Dm.gt(amount, Dm.add(grid_used, apc_used))) // If we aren't forcing it and there isn't enough energy to supply demand, return nothing.
            return FALSE;

        // Use the grid's and APC's energy.
        amount = Dm.add(grid_used, apc_used);
        local_apc.add_load(Dm.mul(grid_used, JOULES));
        home.use_energy(Dm.mul(amount, JOULES), channel);
        return amount;
    }

    /**
     * An alternative to 'use_power', this proc directly costs the APC in direct charge, as opposed to prioritising the grid.
     * Args:
     * - amount: How much energy the APC's cell is to be costed.
     * - force: If true, consumes the remaining energy of the cell if there isn't enough energy to supply the demand.
     * Returns: The amount of energy that got used by the cell.
     */
    // tg: code/modules/power/power.dm:196
    public virtual async DmTask<object> directly_use_energy(object amount = null, float? force = null)
    {
        force ??= FALSE;
        area my_area = get_area(this) as area;
        if (my_area == null)
        {
            stack_trace("machinery is somehow not in an area, nullspace?", "code/modules/power/power.dm", 199);
            return FALSE;
        }
        if (!Dm.truthy(my_area.requires_power))
            return amount;

        obj_machinery_power_apc my_apc = my_area.apc;
        if (my_apc == null || !Dm.truthy(my_apc.operating) || QDELETED(my_apc.cell))
            return FALSE;
        return await my_apc.cell.use(amount, force);
    }

    /**
     * Attempts to draw power directly from the APC's Powernet rather than the APC's battery. For high-draw machines, like the cell charger
     *
     * Checks the surplus power on the APC's powernet, and compares to the requested amount. If the requested amount is available, this proc
     * will add the amount to the APC's usage and return that amount. Otherwise, this proc will return FALSE.
     * If the take_any var arg is set to true, this proc will use and return any surplus that is under the requested amount, assuming that
     * the surplus is above zero.
     * Args:
     * - amount, the amount of power requested from the powernet. In joules.
     * - take_any, a bool of whether any amount of power is acceptable, instead of all or nothing. Defaults to FALSE
     */
    // tg: code/modules/power/power.dm:220
    public virtual object use_power_from_net(object amount = null, float? take_any = null)
    {
        take_any ??= FALSE;
        if (Dm.le(amount, 0)) //just in case
            return FALSE;
        area home = get_area(this) as area;

        if (!Dm.truthy(home))
            return FALSE; //apparently space isn't an area
        if (!Dm.truthy(home.requires_power))
            return amount; //Shuttles get free power, don't ask why

        obj_machinery_power_apc local_apc = home.apc;
        if (local_apc == null || !Dm.truthy(local_apc.operating))
            return FALSE;

        object surplus = local_apc.surplus();
        if (Dm.le(surplus, 0)) //I don't know if powernet surplus can ever end up negative, but I'm just gonna failsafe it
            return FALSE;
        if (Dm.lt(surplus, amount))
        {
            if (!Dm.truthy(take_any))
                return FALSE;
            amount = surplus;
        }
        local_apc.add_load(amount);
        return amount;
    }

    /**
     * Draws power from the apc's powernet and cell to charge a power cell.
     * Args:
     * - amount: The amount of energy given to the cell.
     * - cell: The cell to charge.
     * - grid_only: If true, only draw from the grid and ignore the APC's cell.
     * - channel: The power channel to use.
     * Returns: The amount of energy the cell received.
     */
    // tg: code/modules/power/power.dm:253  (renamed charge_cell__proc: /obj/machinery/recharge_station has a var charge_cell, rules B.2)
    public virtual async DmTask<object> charge_cell__proc(object amount = null, obj_item_stock_parts_power_store cell = null, float? grid_only = null, float? channel = null)
    {
        grid_only ??= FALSE;
        channel ??= AREA_USAGE_EQUIP;
        object demand = await use_energy(min(Dm.num(amount), Dm.num(cell.used_charge())), channel: channel, ignore_apc: grid_only);
        object power_given = await cell.give(demand);
        return power_given;
    }


    // tg: code/modules/power/power.dm:259
    public virtual void addStaticPower(object value = null, object powerchannel = null)
    {
        area A = get_area(this) as area;
        A?.addStaticPower(value, powerchannel);
    }

    // tg: code/modules/power/power.dm:263
    public virtual void removeStaticPower(object value = null, object powerchannel = null)
    {
        addStaticPower(Dm.neg(value), powerchannel);
    }

    /**
     * Called whenever the power settings of the containing area change
     *
     * by default, check equipment channel & set flag, can override if needed
     *
     * Returns TRUE if the NOPOWER flag was toggled
     */
    // tg: code/modules/power/power.dm:273
    [SignalHandler, ShouldCallParent]
    public virtual object power_change()
    {
        object dot = null;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            update_appearance();
            return dot;
        }
        float initial_stat = machine_stat;
        if (Dm.truthy(powered__proc(power_channel)))
        {
            Waiting.NoSleep(set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(NOPOWER))));
            if (Dm.truthy(Dm.bitand(initial_stat, NOPOWER)))
            {
                if (Dm.truthy(this._listen_lookup?[COMSIG_MACHINERY_POWER_RESTORED]))
                    this._SendSignal(COMSIG_MACHINERY_POWER_RESTORED, Dm.list(this));
                dot = TRUE;
            }
        }
        else
        {
            Waiting.NoSleep(set_machine_stat(Dm.bitor(machine_stat, NOPOWER)));
            if (!Dm.truthy(Dm.bitand(initial_stat, NOPOWER)))
            {
                if (Dm.truthy(this._listen_lookup?[COMSIG_MACHINERY_POWER_LOST]))
                    this._SendSignal(COMSIG_MACHINERY_POWER_LOST, Dm.list(this));
                dot = TRUE;
            }
        }

        if (!Dm.eq(appearance_power_state, Dm.bitand(machine_stat, NOPOWER)))
            update_appearance();
        return dot;
    }

    // Saves like 300ms of init by not duping calls in the above proc
    // tg: code/modules/power/power.dm:296
    public override object update_appearance(float? updates = null)
    {
        object dot = null;
        dot = base.update_appearance(updates);
        appearance_power_state = Dm.num(Dm.bitand(machine_stat, NOPOWER));
        return dot;
    }
}

// tg: code/modules/power/power.dm:301  (interleaved types, rules 1.9)
public partial class obj_machinery_power : obj_machinery
{
    // connect the machine to a powernet if a node cable or a terminal is present on the turf
    // tg: code/modules/power/power.dm:301
    public virtual object connect_to_network()
    {
        turf T = this.loc as turf;
        if (!Dm.truthy(T) || !(T is turf))
            return FALSE;

        obj_structure_cable C = T.get_cable_node(cable_layer) as obj_structure_cable; //check if we have a node cable on the machine turf, the first found is picked
        if (!Dm.truthy(C) || !Dm.truthy(C.powernet))
        {
            obj_machinery_power_terminal term = Dm.locate<obj_machinery_power_terminal>(T);
            if (!Dm.truthy(term) || !Dm.truthy(term.powernet))
                return FALSE;
            else
            {
                ((datum_powernet)term.powernet).add_machine(this);
                return TRUE;
            }
        }

        C.powernet.add_machine(this);
        return TRUE;
    }

    // remove and disconnect the machine from its current powernet
    // tg: code/modules/power/power.dm:319
    public virtual object disconnect_from_network()
    {
        if (!Dm.truthy(powernet))
            return FALSE;
        ((datum_powernet)powernet).remove_machine(this);
        return TRUE;
    }

    // attach a wire to a power machine - leads from the turf you are standing on
    //almost never called, overwritten by all power machines but terminal and generator
    // tg: code/modules/power/power.dm:327
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_stack_cable_coil))
            return NONE;
        obj_item_stack_cable_coil coil = tool as obj_item_stack_cable_coil;
        turf userturf = user.loc as turf;
        if (userturf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE || !isfloorturf(userturf))
            return ITEM_INTERACT_BLOCKING;
        if (get_dist(this, user) > 1)
            return ITEM_INTERACT_BLOCKING;
        await coil.place_turf(userturf, user);
        return ITEM_INTERACT_SUCCESS;
    }

    ///////////////////////////////////////////
    // Powernet handling helpers
    //////////////////////////////////////////

    //returns all the cables WITHOUT a powernet in neighbors turfs,
    //pointing towards the turf the machine is located at
    // tg: code/modules/power/power.dm:345
    public virtual object get_connections()
    {
        DmList dot = null;
        dot = Dm.list();
        turf T = null;

        foreach (object card in Dm.each(GLOB.cardinals))
        {
            T = get_step(loc, Dm.num(card));

            foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(T))
            {
                if (Dm.truthy(C.powernet))
                    continue;
                Dm.append(ref dot, C);
            }
        }
        return dot;
    }

    //returns all the cables in neighbors turfs,
    //pointing towards the turf the machine is located at
    // tg: code/modules/power/power.dm:360
    public virtual object get_marked_connections()
    {
        DmList dot = null;
        dot = Dm.list();
        turf T = null;

        foreach (object card in Dm.each(GLOB.cardinals))
        {
            T = get_step(loc, Dm.num(card));

            foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(T))
                Dm.append(ref dot, C);
        }
        return dot;
    }

    //returns all the NODES (O-X) cables WITHOUT a powernet in the turf the machine is located at
    // tg: code/modules/power/power.dm:372
    public virtual object get_indirect_connections()
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(loc))
        {
            if (Dm.truthy(C.powernet))
                continue;
            Dm.append(ref dot, C);
        }
        return dot;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/power/power.dm:380
    public static void update_cable_icons_on_turf(turf T = null)
    {
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(T.contents))
            C.update_appearance();
    }

    ///////////////////////////////////////////
    // GLOBAL PROCS for powernets handling
    //////////////////////////////////////////

    ///remove the old powernet and replace it with a new one throughout the network.
    // tg: code/modules/power/power.dm:389
    public static void propagate_network(obj_structure_cable C = null, datum_powernet PN = null, float? skip_assigned_powernets = null)
    {
        skip_assigned_powernets ??= FALSE;
        DmList found_machines = Dm.list();
        DmList cables = Dm.list();
        float index = 1;
        obj_structure_cable working_cable = null;

        cables[C] = TRUE; //associated list for performance reasons

        while (index <= length(cables))
        {
            working_cable = cables[index] as obj_structure_cable;
            index++;

            DmList connections = working_cable.get_cable_connections(skip_assigned_powernets) as DmList;

            foreach (obj_structure_cable cable_entry in Dm.each<obj_structure_cable>(connections))
            {
                if (!Dm.truthy(cables[cable_entry])) //Since it's an associated list, we can just do an access and check it's null before adding; prevents duplicate entries
                    cables[cable_entry] = TRUE;
            }
        }

        foreach (obj_structure_cable cable_entry in Dm.each<obj_structure_cable>(cables))
        {
            PN.add_cable(cable_entry);
            Dm.append(ref found_machines, cable_entry.get_machine_connections(skip_assigned_powernets));
        }

        //now that the powernet is set, connect found machines to it
        foreach (obj_machinery_power PM in Dm.each<obj_machinery_power>(found_machines))
        {
            if (!Dm.truthy(PM.connect_to_network())) //couldn't find a node on its turf...
                PM.disconnect_from_network(); //... so disconnect if already on a powernet
        }
    }


    //Merge two powernets, the bigger (in cable length term) absorbing the other
    // tg: code/modules/power/power.dm:418
    public static object merge_powernets(datum_powernet net1 = null, datum_powernet net2 = null)
    {
        if (!Dm.truthy(net1) || !Dm.truthy(net2)) //if one of the powernet doesn't exist, return
            return null;

        if (net1 == net2) //don't merge same powernets
            return null;

        //We assume net1 is larger. If net2 is in fact larger we are just going to make them switch places to reduce on code.
        if (net1.cables.len < net2.cables.len) //net2 is larger than net1. Let's switch them around
        {
            object temp = net1;
            net1 = net2;
            net2 = temp as datum_powernet;
        }

        //merge net2 into net1
        foreach (obj_structure_cable Cable in Dm.each<obj_structure_cable>(net2.cables)) //merge cables
            net1.add_cable(Cable);

        foreach (obj_machinery_power Node in Dm.each<obj_machinery_power>(net2.nodes)) //merge power machines
        {
            if (!Dm.truthy(Node.connect_to_network()))
                Node.disconnect_from_network(); //if somehow we can't connect the machine to the new powernet, disconnect it from the old nonetheless
        }

        return net1;
    }

    /// Extracts the powernet and cell of the provided power source
    // tg: code/modules/power/power.dm:442
    public static object get_powernet_info_from_source(object power_source = null)
    {
        area source_area = null;
        if (isarea(power_source))
        {
            source_area = power_source as area;
            power_source = source_area.apc;
        }
        else if (power_source is obj_structure_cable)
        {
            obj_structure_cable Cable = power_source as obj_structure_cable;
            power_source = Cable.powernet;
        }

        datum_powernet PN = null;
        obj_item_stock_parts_power_store cell = null;

        if (power_source is datum_powernet)
            PN = power_source as datum_powernet;
        else if (power_source is obj_item_stock_parts_power_store)
            cell = power_source as obj_item_stock_parts_power_store;
        else if (power_source is obj_machinery_power_apc)
        {
            obj_machinery_power_apc apc = power_source as obj_machinery_power_apc;
            cell = apc.cell;
            if (Dm.truthy(apc.terminal))
                PN = apc.terminal.powernet as datum_powernet;
        }
        else
            return FALSE;

        if (!Dm.truthy(cell) && !Dm.truthy(PN))
            return null;

        return Dm.list(Dm.kv("powernet", PN), Dm.kv("cell", cell));
    }

    //Determines how strong could be shock, deals damage to mob, uses power.
    //M is a mob who touched wire/whatever
    //power_source is a source of electricity, can be power cell, area, apc, cable, powernet or null
    //source is an object caused electrocuting (airlock, grille, etc)
    //siemens_coeff - layman's terms, conductivity
    //dist_check - set to only shock mobs within 1 of source (vendors, airlocks, etc.)
    //No animations will be performed by this proc.
    // tg: code/modules/power/power.dm:478
    public static async DmTask<object> electrocute_mob(mob_living_carbon victim = null, object power_source = null, obj source = null, float? siemens_coeff = null, float? dist_check = null)
    {
        siemens_coeff ??= 1;
        dist_check ??= FALSE;
        if (!(victim is mob_living_carbon) || ismecha(victim.loc))
            return FALSE; //feckin mechs are dumb

        if (Dm.truthy(dist_check))
        {
            if (!in_range(source, victim))
                return FALSE;
        }

        if (Dm.eq(((obj_item)victim.get_item_by_slot(ITEM_SLOT_GLOVES))?.siemens_coefficient, 0))
        {
            if (Dm.truthy(victim._listen_lookup?[COMSIG_LIVING_SHOCK_PREVENTED]))
                victim._SendSignal(COMSIG_LIVING_SHOCK_PREVENTED, Dm.list(victim, power_source, source, siemens_coeff, dist_check));
            return FALSE; //to avoid spamming with insulated gloves on
        }

        DmList powernet_info = get_powernet_info_from_source(power_source) as DmList;
        if (!Dm.truthy(powernet_info))
            return FALSE;

        datum_powernet PN = powernet_info["powernet"] as datum_powernet;
        obj_item_stock_parts_power_store cell = powernet_info["cell"] as obj_item_stock_parts_power_store;

        object PN_damage = 0;
        object cell_damage = 0;
        if (Dm.truthy(PN))
            PN_damage = PN.get_electrocute_damage();
        if (Dm.truthy(cell))
            cell_damage = cell.get_electrocute_damage();
        object shock_damage = 0;
        if (Dm.ge(PN_damage, cell_damage))
        {
            power_source = PN;
            shock_damage = PN_damage;
        }
        else
        {
            power_source = cell;
            shock_damage = cell_damage;
        }
        object drained_hp = await victim.electrocute_act(shock_damage, source, siemens_coeff); //zzzzzzap!
        log_combat(source, victim, "electrocuted");

        object drained_energy = Dm.mul(drained_hp, 20);

        if (isarea(power_source))
        {
            area source_area = power_source as area;
            if (source_area.apc?.terminal != null)
                await source_area.apc.terminal.use_energy(drained_energy);
        }
        else if (power_source is datum_powernet)
            PN.delayedload += Dm.num(min(drained_energy, max(PN.newavail - PN.delayedload, 0)));
        else if (power_source is obj_item_stock_parts_power_store)
            await cell.use(drained_energy);
        return drained_energy;
    }
}

////////////////////////////////////////////////
// Misc.
///////////////////////////////////////////////

// tg: code/modules/power/power.dm:529  (interleaved types, rules 1.9)
public partial class turf : atom
{
    // return a cable able connect to machinery on layer if there's one on the turf, null if there isn't one
    // tg: code/modules/power/power.dm:529
    public virtual object get_cable_node(float? cable_layer = null)
    {
        cable_layer ??= CABLE_LAYER_ALL;
        if (!Dm.truthy(can_have_cabling()))
            return null;
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(this))
        {
            if (Dm.truthy(Dm.bitand(C.cable_layer, cable_layer)))
            {
                C.update_appearance(); // I hate this. it's here because update_icon_state SCANS nearby turfs for objects to connect to. Wastes cpu time
                return C;
            }
        }
        return null;
    }
}
