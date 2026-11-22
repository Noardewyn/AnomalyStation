// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/_plumbing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The mapping test's overlap log in Initialize is inactive in production (rules 1.7).

// tg: code/datums/components/plumbing/_plumbing.dm:1
public partial class datum_component_plumbing : datum_component
{
    ///shortcut to our parents' reagent holder. The holder that sends reagents into the pipeline
    // tg: code/datums/components/plumbing/_plumbing.dm:6
    // The generated field would be a Type (tg declares var/datum/reagents/reagents, the name the atoms' typepath var shares): the
    // hand-written member holds the holder, and the constructor with tg's overrides (_plumbing.dm:2) is here.
    public datum_reagents reagents;

    // tg: code/datums/components/plumbing/_plumbing.dm:1
    public datum_component_plumbing()
    {
        dupe_mode = COMPONENT_DUPE_ALLOWED;
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:23
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object ducting_layer = Dm.arg(__extra, 0);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        atom_movable parent_movable = (atom_movable)parent;
        if (!Dm.truthy(parent_movable.reagents))
            return COMPONENT_INCOMPATIBLE;

        if (Dm.truthy(GLOB.plumbing_layer_names[Dm.text("[]", ducting_layer)]))
            this.ducting_layer = Dm.num(ducting_layer);

        ducts = Dm.list();

        reagents = parent_movable.reagents as datum_reagents;

        on_parent_dir_change(parent_movable, NONE, parent_movable.dir);
        if (Dm.truthy(parent_movable.anchored))
        {
            // tg: :40-49  if(PERFORM_ALL_TESTS(maptest_log_mapping)): the mapping test's overlap log, FALSE in production (rules 1.7)
            enable();
        }
        return null;
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:53
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_WRENCH), nameof(check_wrench));
        RegisterSignal(parent, COMSIG_MOVABLE_SET_ANCHORED, nameof(toggle_active));
        RegisterSignal(parent, COMSIG_OBJ_HIDE, nameof(hide));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(create_overlays));
        RegisterSignal(parent, COMSIG_ATOM_POST_DIR_CHANGE, nameof(on_parent_dir_change));
        RegisterSignal(parent, COMSIG_MOVABLE_CHANGE_DUCT_LAYER, nameof(change_ducting_layer));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:62
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_TOOL_ACT(TOOL_WRENCH),
            COMSIG_MOVABLE_SET_ANCHORED,
            COMSIG_OBJ_HIDE,
            COMSIG_ATOM_UPDATE_OVERLAYS,
            COMSIG_ATOM_POST_DIR_CHANGE,
            COMSIG_MOVABLE_CHANGE_DUCT_LAYER,
            COMSIG_ATOM_EXAMINE));
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:73
    public override object Destroy(float? force = null)
    {
        disable();
        ducts.Cut();
        reagents = null;
        return base.Destroy(force);
    }

    ///Returns if the machine is active or not
    // tg: code/datums/components/plumbing/_plumbing.dm:80
    public virtual object active()
    {
        atom_movable parent_movable = (atom_movable)parent;
        return parent_movable.anchored;
    }

    ///Returns the reagent holder meant to receive the reagents. Can be different from the one that sends reagents to the network
    // tg: code/datums/components/plumbing/_plumbing.dm:85
    public virtual object recipient_reagents_holder()
    {
        return reagents;
    }

    ///settle wherever we are, and start behaving like a piece of plumbing
    // tg: code/datums/components/plumbing/_plumbing.dm:89
    public virtual void enable()
    {
        if (Dm.truthy(demand_connects))
            START_PROCESSING(SSplumbing, this);

        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (!Dm.truthy(Dm.bitand(direction, Dm.bitor(demand_connects, supply_connects))))
                continue;

            float opposite_dir = REVERSE_DIR(Dm.num(direction));
            foreach (atom_movable found_atom in Dm.each<atom_movable>(get_step(parent as atom, Dm.num(direction))))
            {
                obj_machinery_duct duct = found_atom as obj_machinery_duct;
                if (duct != null)
                {
                    if (Dm.truthy(duct.neighbours) && Dm.truthy(Dm.bitand(duct.duct_layer, ducting_layer)))
                    {
                        duct.neighbours[parent] = opposite_dir;
                        duct.update_appearance(UPDATE_ICON);
                        duct.net.add_plumber(this, direction);
                    }
                    continue;
                }

                foreach (datum_component_plumbing plumber in Dm.each_cast<datum_component_plumbing>(found_atom.GetComponents(typeof(datum_component_plumbing))))
                {
                    if (Dm.truthy(plumber.active()) && Dm.truthy(Dm.bitand(plumber.ducting_layer, ducting_layer)))
                    {
                        if ((Dm.truthy(Dm.bitand(plumber.demand_connects, opposite_dir)) && Dm.truthy(Dm.bitand(supply_connects, direction))) || (Dm.truthy(Dm.bitand(plumber.supply_connects, opposite_dir)) && Dm.truthy(Dm.bitand(demand_connects, direction)))) //make sure we arent connecting two supplies or demands
                        {
                            datum_ductnet net = Dm.@new<datum_ductnet>();
                            net.add_plumber(this, direction);
                            net.add_plumber(plumber, opposite_dir);
                        }
                    }
                }
            }
        }
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:114
    [SignalHandler]
    public virtual void disable()
    {
        STOP_PROCESSING(SSplumbing, this);

        while (Dm.truthy(ducts.len))
        {
            datum_ductnet net = ducts[ducts[1]] as datum_ductnet;

            //disconnect ourself from any ducts connected to us
            foreach (obj_machinery_duct pipe in Dm.each_cast<obj_machinery_duct>(net.ducts))
            {
                if (Dm.truthy(pipe.neighbours[parent]))
                {
                    Dm.remove(ref pipe.neighbours, parent);
                    pipe.update_appearance(UPDATE_ICON);
                }
            }

            //remove ourself from this network and delete it if emtpy
            if (Dm.truthy(net.remove_plumber(this)))
                qdel(net);
        }
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:132
    [SignalHandler]
    public virtual object check_wrench(obj parent_obj = null, mob user = null, object tool = null, object processing_recipes = null)
    {
        if (!Dm.truthy(active()))
        {
            datum overlap = ducting_layer_check(parent_obj, -ducting_layer) as datum;
            if (overlap != null)
            {
                parent_obj.balloon_alert(user, Dm.text("overlapping [] detected!", istype(overlap, typeof(obj_machinery_duct)) ? "duct" : "machine"));
                return ITEM_INTERACT_FAILURE;
            }
        }
        return null;
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:141
    [SignalHandler]
    public virtual void toggle_active(obj parent_obj = null, object new_state = null)
    {
        // Follow atmos's rule of exposing the connection if you unwrench it and only hiding again if tile is placed back down.
        if (Dm.truthy(tile_covered))
        {
            tile_covered = FALSE;
            parent_obj.update_appearance();
        }

        if (Dm.truthy(new_state))
            enable();
        else
            disable();
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:154
    [SignalHandler]
    public virtual void hide(atom_movable parent_obj = null, object underfloor_accessibility = null)
    {
        atom_movable parent_movable = (atom_movable)parent;
        // If machine is unanchored, keep connector visible.
        // This doesn't necessary map to `active`, so check parent.
        float should_hide = !Dm.truthy(underfloor_accessibility) ? TRUE : FALSE;
        if (Dm.truthy(parent_movable.anchored) || !Dm.truthy(should_hide))
        {
            tile_covered = should_hide;
            parent_obj.update_appearance();
        }
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:165
    [SignalHandler]
    public virtual void create_overlays(atom_movable parent_movable = null, DmList overlays = null)
    {
        if (Dm.truthy(tile_covered))
            return;

        float offset = 0;
        switch (ducting_layer)
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

        float duct_layer = PLUMBING_PIPE_VISIBILE_LAYER + ducting_layer * 0.0003f;

        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            string color;
            float duct_x = offset - parent_movable.pixel_x - parent_movable.pixel_w;
            float duct_y = offset - parent_movable.pixel_y - parent_movable.pixel_z;
            if (Dm.truthy(Dm.bitand(direction, Dm.initial(this, (datum_component_plumbing p) => p.demand_connects))))
                color = demand_color;
            else if (Dm.truthy(Dm.bitand(direction, Dm.initial(this, (datum_component_plumbing p) => p.supply_connects))))
                color = supply_color;
            else
                continue;

            image overlay = image(Dm.resource("icons/obj/pipes_n_cables/hydrochem/connects.dmi"), icon_state: Dm.text("[]-[]", dir2text(direction), ducting_layer), layer: duct_layer);
            overlay.color = color;
            overlay.pixel_w = duct_x;
            overlay.pixel_z = duct_y;
            Dm.append(ref overlays, overlay);
        }
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:203
    [SignalHandler]
    public virtual void on_parent_dir_change(atom_movable parent_obj = null, object old_dir = null, object new_dir = null)
    {
        demand_connects = Dm.initial(this, (datum_component_plumbing p) => p.demand_connects);
        supply_connects = Dm.initial(this, (datum_component_plumbing p) => p.supply_connects);
        if (!Dm.eq(new_dir, SOUTH))
        {
            float angle = 180 - Dm.num(dir2angle(new_dir));
            float new_demand_connects = NONE;
            float new_supply_connects = NONE;
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(direction, demand_connects)))
                    new_demand_connects = Dm.num(Dm.bitor(new_demand_connects, turn(Dm.num(direction), angle)));
                if (Dm.truthy(Dm.bitand(direction, supply_connects)))
                    new_supply_connects = Dm.num(Dm.bitor(new_supply_connects, turn(Dm.num(direction), angle)));
            }
            demand_connects = new_demand_connects;
            supply_connects = new_supply_connects;
        }
        parent_obj.update_appearance(UPDATE_OVERLAYS);

        // DEVIATION(DV56): tg re-links only a component that has ducts; the port also re-links an anchored one turned after its
        // Initialize (old_dir set), since the server's model rules and the owner's map patches set a mapped thing's dir after its
        // ducts have joined it (a mapped plumbed tank would otherwise stay off its line). In tg an anchored, unlinked thing never turns.
        if (Dm.truthy(length(ducts)) || (Dm.truthy(old_dir) && Dm.truthy(active())))
        {
            disable();
            enable();
        }
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:225
    [SignalHandler]
    public virtual void change_ducting_layer(obj source = null, obj changer = null, float? new_layer = null)
    {
        new_layer ??= DUCT_LAYER_DEFAULT;
        ducting_layer = new_layer.Value;
        source.update_appearance(UPDATE_OVERLAYS);

        if (changer != null)
            playsound(changer, Dm.resource("sound/items/tools/ratchet.ogg"), 10, TRUE); //sound

        if (Dm.truthy(length(ducts)))
        {
            disable();
            enable();
        }
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:238
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(demand_connects))
            return PROCESS_KILL;

        datum_reagents receiver = recipient_reagents_holder() as datum_reagents;
        if (QDELETED(receiver))
            return PROCESS_KILL;

        if (!Dm.truthy(receiver.holder_full()))
        {
            foreach (object dir in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(dir, demand_connects)))
                    Waiting.NoSleep(send_request(dir));
            }
        }
        return null;
    }

    // tg: code/datums/components/plumbing/_plumbing.dm:251
    [SignalHandler]
    public virtual void on_examine(atom_movable source = null, mob user = null, DmList examine_list = null)
    {
        if (distinct_reagent_cap != INFINITY)
            Dm.append(ref examine_list, span_notice(I18n.T("This plumbing component will only accept up to [] distinct reagents at once.", distinct_reagent_cap)));
    }

    ///called from in process(). only calls process_request(), but can be overwritten for children with special behaviour
    // tg: code/datums/components/plumbing/_plumbing.dm:258
    public virtual async DmTask send_request(object dir = null)
    {
        float amount_to_give = MACHINE_REAGENT_TRANSFER;
        // infinite cap means we need to special handling, process_request will just grab as much as it wants.
        if (distinct_reagent_cap == INFINITY)
        {
            await process_request(amount_to_give, null, dir); // null for no specific reagent, we're not picky.
            return;
        }

        // we have a cap, so we need to figure out what reagents we want
        DmList all_allowed_reagents = get_all_network_reagents(ducts[Dm.text("[]", dir)] as datum_ductnet) as DmList;
        if (length(all_allowed_reagents) > distinct_reagent_cap)
            all_allowed_reagents.Cut(distinct_reagent_cap + 1);
        else if (!Dm.truthy(length(all_allowed_reagents)))
            return;

        // request an even amount of each allowed reagent
        float amount_per_reagent = round(amount_to_give / length(all_allowed_reagents), CHEMICAL_VOLUME_ROUNDING);
        foreach (object allowed_reagent in Dm.each(all_allowed_reagents))
            await process_request(amount_per_reagent, allowed_reagent, dir);
    }

    /// Returns a list of all distinct reagent types available in the passed duct network.
    /// The passed net can be null, it is handled.
    // tg: code/datums/components/plumbing/_plumbing.dm:279
    public virtual object get_all_network_reagents(datum_ductnet net = null)
    {
        DmList distinct_reagents = Dm.list();
        foreach (datum_reagent existing_regent in Dm.each_cast<datum_reagent>(reagents.reagent_list))
            Dm.combine(ref distinct_reagents, existing_regent.type);
        foreach (datum_component_plumbing supplier in Dm.each_cast<datum_component_plumbing>(net?.suppliers))
        {
            foreach (datum_reagent chemical in Dm.each_cast<datum_reagent>(supplier.reagents.reagent_list))
                Dm.combine(ref distinct_reagents, chemical.type);
        }
        return distinct_reagents;
    }

    ///check who can give us what we want, and how many each of them will give us
    // tg: code/datums/components/plumbing/_plumbing.dm:289
    public virtual async DmTask<object> process_request(float? amount = null, object reagent = null, object dir = null, float? round_robin = null)
    {
        amount ??= MACHINE_REAGENT_TRANSFER;
        round_robin ??= TRUE;
        //find the duct to take from
        string dirtext = num2text(Dm.num(dir));
        datum_ductnet net = ducts[dirtext] as datum_ductnet;
        if (QDELETED(net))
        {
            if (net != null)
                Dm.remove(ref ducts, dirtext);
            return FALSE;
        }

        //find all valid suppliers in the duct
        DmList valid_suppliers = Dm.list();
        foreach (datum_component_plumbing supplier in Dm.each_cast<datum_component_plumbing>(net?.suppliers))
        {
            if (Dm.truthy(supplier.can_give(amount, reagent, net)))
                Dm.append(ref valid_suppliers, supplier);
        }
        float suppliersLeft = length(valid_suppliers);
        if (!Dm.truthy(suppliersLeft))
            return FALSE;

        //take an equal amount from each supplier
        float currentRequest;
        float target_volume = reagents.total_volume + amount.Value;
        foreach (datum_component_plumbing give in Dm.each_cast<datum_component_plumbing>(valid_suppliers))
        {
            currentRequest = (target_volume - reagents.total_volume) / suppliersLeft;
            await give.transfer_to(this, currentRequest, reagent, net, round_robin);
            suppliersLeft--;
        }
        return TRUE;
    }

    ///returns TRUE when they can give the specified amount and reagent. called by process request
    // tg: code/datums/components/plumbing/_plumbing.dm:317
    public virtual object can_give(object amount = null, object reagent = null, datum_ductnet net = null)
    {
        if (Dm.num(amount) <= 0)
            return FALSE;

        if (Dm.truthy(reagent)) //only asked for one type of reagent
            return reagents.has_reagent(reagent);
        else if (Dm.truthy(reagents.total_volume)) //take whatever
            return TRUE;

        return FALSE;
    }

    ///this is where the reagent is actually transferred and is thus the finish point of our process()
    // tg: code/datums/components/plumbing/_plumbing.dm:331
    public virtual async DmTask transfer_to(datum_component_plumbing target = null, object amount = null, object reagent = null, datum_ductnet net = null, float? round_robin = null)
    {
        round_robin ??= TRUE;
        await reagents.trans_to(target.recipient_reagents_holder(), Dm.num(amount), target_id: reagent, methods: Dm.truthy(round_robin) ? LINEAR : NONE);
    }
}
