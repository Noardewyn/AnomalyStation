// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/supply.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G4 (D272), D299, gaps P4b (D298): the cargo shuttle; the orders are paid from the accounts, what is sent back is sold, the mail rides on it.

// tg: code/modules/shuttle/mobile_port/variants/supply.dm:1  GLOBAL_LIST_INIT(blacklisted_cargo_types, typecacheof(list(...)))
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:1
    public DmList blacklisted_cargo_types;

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:1  (GLOBAL_MANAGED(blacklisted_cargo_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblacklisted_cargo_types()
    {
        blacklisted_cargo_types = typecacheof(Dm.list(
            typeof(mob_living),
            typeof(obj_docking_port),
            typeof(obj_effect_hierophant),
            typeof(obj_effect_mob_spawn),
            typeof(obj_effect_portal),
            typeof(obj_effect_rune),
            typeof(obj_item_beacon),
            typeof(obj_item_disk_nuclear),
            typeof(obj_item_gps),
            typeof(obj_item_hilbertshotel),
            typeof(obj_item_mail),
            typeof(obj_item_shared_storage),
            typeof(obj_item_swapper),
            typeof(obj_item_warp_cube),
            typeof(obj_machinery_autolathe), // In case you manage to get it to print a beacon while in transit
            typeof(obj_machinery_camera),
            typeof(obj_machinery_disposal),
            typeof(obj_machinery_exodrone_launcher),
            typeof(obj_machinery_fax),
            typeof(obj_machinery_launchpad),
            typeof(obj_machinery_nuclearbomb),
            typeof(obj_machinery_quantumpad),
            typeof(obj_machinery_rnd_production),
            typeof(obj_machinery_syndicatebomb),
            typeof(obj_machinery_teleport_hub),
            typeof(obj_machinery_teleport_station),
            typeof(obj_narsie),
            typeof(obj_projectile_beam_wormhole),
            typeof(obj_structure_blob),
            typeof(obj_structure_checkoutmachine),
            typeof(obj_structure_disposalpipe),
            typeof(obj_structure_disposaloutlet),
            typeof(obj_structure_extraction_point),
            typeof(obj_structure_guardian_beacon),
            typeof(obj_tear_in_reality))) as DmList;
        Dm.append(ref gvars_datum_init_order, "blacklisted_cargo_types");
    }
}

// tg: code/modules/shuttle/mobile_port/variants/supply.dm:45
[DmPath("/obj/docking_port/mobile/supply")]
public partial class obj_docking_port_mobile_supply : obj_docking_port_mobile
{
    /// How many goody orders we can fit in a lockbox before we upgrade to a crate
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:40
    private const int GOODY_FREE_SHIPPING_MAX = 5;
    /// How much to charge oversized goody orders
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:42
    private const int CRATE_TAX = 700;

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Apply speed traits to each shuttle, including ones loaded after roundstart.
        if (HAS_TRAIT(SSstation, STATION_TRAIT_QUICK_SHUTTLE))
        {
            callTime *= 0.5f;
            RegisterSignal(SSstation, SIGNAL_REMOVETRAIT(STATION_TRAIT_QUICK_SHUTTLE), nameof(on_speed_trait_removed));
        }
        if (HAS_TRAIT(SSstation, STATION_TRAIT_SLOW_SHUTTLE))
        {
            callTime *= 1.5f;
            RegisterSignal(SSstation, SIGNAL_REMOVETRAIT(STATION_TRAIT_SLOW_SHUTTLE), nameof(on_speed_trait_removed));
        }
        return dot;
    }

    // Undo only the applied modifier when an admin reverts the trait in the lobby.
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:65
    public virtual void on_speed_trait_removed(datum source = null, object removed_trait = null)
    {
        if (Dm.eq(removed_trait, STATION_TRAIT_QUICK_SHUTTLE))
            callTime /= 0.5f;
        else if (Dm.eq(removed_trait, STATION_TRAIT_SLOW_SHUTTLE))
            callTime /= 1.5f;
        UnregisterSignal(source, SIGNAL_REMOVETRAIT(removed_trait));
    }

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:74
    public override object register(object replace = null, object custom = null)
    {
        object dot = base.register(replace, custom);
        SSshuttle.supply = this;
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:78
    public override object canMove()
    {
        object dot = base.canMove();
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(is_station_level(z)))
            return check_blacklist(shuttle_areas);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:85
    public virtual object check_blacklist(DmList areaInstances = null)
    {
        foreach (area shuttle_area in Dm.each<area>(areaInstances))
        {
            foreach (DmList zlevel_turfs in Dm.each<DmList>(shuttle_area.get_zlevel_turf_lists()))
            {
                foreach (turf shuttle_turf in Dm.each<turf>(zlevel_turfs))
                {
                    foreach (atom passenger in Dm.each<atom>(shuttle_turf.get_all_contents()))
                    {
                        if ((Dm.truthy(is_type_in_typecache(passenger, GLOB.blacklisted_cargo_types)) || HAS_TRAIT(passenger, TRAIT_BANNED_FROM_CARGO_SHUTTLE)) && passenger is not obj_docking_port)
                            return FALSE;
                    }
                }
            }
        }
        return TRUE;
    }

    /// Returns anything on the cargo blacklist found within areas_to_check back to the turf of the home docking port via Centcom branded supply pod.
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:95
    public virtual async DmTask<object> return_blacklisted_things_home(DmList areas_to_check = null, obj_docking_port_stationary home = null)
    {
        DmList stuff_to_send_home = Dm.list();
        foreach (area shuttle_area in Dm.each<area>(areas_to_check))
        {
            foreach (DmList zlevel_turfs in Dm.each<DmList>(shuttle_area.get_zlevel_turf_lists()))
            {
                foreach (turf shuttle_turf in Dm.each<turf>(zlevel_turfs))
                {
                    foreach (atom passenger in Dm.each<atom>(shuttle_turf.get_all_contents()))
                    {
                        if ((Dm.truthy(is_type_in_typecache(passenger, GLOB.blacklisted_cargo_types)) || HAS_TRAIT(passenger, TRAIT_BANNED_FROM_CARGO_SHUTTLE)) && passenger is not obj_docking_port)
                            Dm.append(ref stuff_to_send_home, passenger);
                    }
                }
            }
        }

        if (!Dm.truthy(length(stuff_to_send_home)))
            return FALSE;

        await podspawn(Dm.list(
            Dm.kv("target", get_turf(home)),
            Dm.kv("path", typeof(obj_structure_closet_supplypod_centcompod)),
            Dm.kv("spawn", stuff_to_send_home)));

        return stuff_to_send_home;
    }

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:115
    public override object request(obj_docking_port_stationary destination_port = null, area signal_origin = null, object reason = null, object red_alert = null, object set_coefficient = null)
    {
        if (!Dm.eq(mode, SHUTTLE_IDLE))
            return 2f;
        return base.request(destination_port);
    }

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:120
    public override object check_dock(obj_docking_port_stationary S = null, object silent = null)
    {
        object dot = base.check_dock(S, silent);

        if (!Dm.truthy(dot))
            return dot;

        // If we're not trying to dock at Centcom, we don't care.
        if (!Dm.eq(S.shuttle_id, "cargo_away"))
            return dot;

        // Else we are docking at Centcom, check the blacklist to make sure no contraband was put onto the shuttle mid-transit.
        // If there's anything contrabandy, send these items back to the origin docking port.
        // This is a sort of catch-all Centcom exploit check.
        DmList stuff_sent_home = Waiting.NoSleep(return_blacklisted_things_home(shuttle_areas, previous)) as DmList;
        if (!Dm.truthy(length(stuff_sent_home)))
            return dot;

        foreach (atom thing_sent_home in Dm.each_cast<atom>(stuff_sent_home))
            investigate_log(Dm.text("Blacklisted item found on in-transit Cargo Shuttle: [] ([])", thing_sent_home, thing_sent_home.type), INVESTIGATE_CARGO);

        message_admins("Blacklisted item found on in-transit Cargo Shuttle. See cargo logs for more details.");
        SSshuttle.centcom_message = "Contraband found on Cargo Shuttle. This has been returned via drop pod.";
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:143
    public override async DmTask<object> initiate_docking(obj_docking_port_stationary new_dock = null, object movement_direction = null, object force = null)
    {
        if (Dm.eq(getDockedId(), "cargo_away")) // Buy when we leave home.
        {
            await buy();
            await create_mail();
            if (Dm.truthy(SSshuttle.renew_cargo_air))
                refill_air();
        }
        object dot = await base.initiate_docking(new_dock, movement_direction, force); // Fly/enter transit.
        if (!Dm.eq(dot, DOCKING_SUCCESS))
            return dot;
        if (Dm.eq(getDockedId(), "cargo_away")) // Sell when we get home
        {
            await sell();
            if (Dm.truthy(SSshuttle.renew_cargo_air))
                refill_air();
        }
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:158
    public virtual async DmTask buy()
    {
        _SendSignal_buy();
        DmList miscboxes = Dm.list(); //miscboxes are combo boxes that contain all goody orders grouped
        DmList misc_order_num = Dm.list(); //list of strings of order numbers, so that the manifest can show all orders in a box
        DmList misc_contents = Dm.list(); //list of lists of items that each box will contain
        DmList misc_costs = Dm.list(); //list of overall costs sustained by each buyer.

        DmList empty_turfs = Dm.list();
        foreach (area_shuttle shuttle_area in Dm.each<area_shuttle>(shuttle_areas))
        {
            foreach (turf_open_floor shuttle_turf in Dm.each<turf_open_floor>(shuttle_area.get_turfs_from_all_zlevels()))
            {
                if (Dm.truthy(await shuttle_turf.is_blocked_turf()))
                    continue;
                Dm.append(ref empty_turfs, shuttle_turf);
            }
        }

        //quickly and greedily handle chef's grocery runs first, there are a few reasons why this isn't attached to the rest of cargo...
        //but the biggest reason is that the chef requires produce to cook and do their job, and if they are using this system they
        //already got let down by the botanists. So to open a new chance for cargo to also screw them over any more than is necessary is bad.
        if (Dm.truthy(SSshuttle.chef_groceries.len))
        {
            var grocery_crate = Dm.@new<obj_structure_closet_crate_freezer>(pick_n_take(empty_turfs));
            grocery_crate.name = I18n.T("kitchen produce freezer");
            grocery_crate.desc = I18n.T("Produce order for the Kitchen, deliver to the chef ASAP.");
            investigate_log(Dm.text("Chef's [] sized produce order arrived. Cost was deducted from orderer, not cargo.", SSshuttle.chef_groceries.len), INVESTIGATE_CARGO);
            foreach (datum_orderable_item item in Dm.each_cast<datum_orderable_item>(SSshuttle.chef_groceries))//every order
            {
                for (int amt = 1; amt <= Dm.num(SSshuttle.chef_groceries[item]); amt++)//every order amount
                    Dm.@new(item.purchase_path as Type, grocery_crate);
            }
            SSshuttle.chef_groceries.Cut(); //This lets the console know it can order another round.
        }

        if (!Dm.truthy(SSshuttle.shopping_list.len))
            return;

        float value = 0;
        float purchases = 0;
        float price = 0;
        float pack_cost = 0;
        DmList goodies_by_buyer = Dm.list(); // if someone orders more than GOODY_FREE_SHIPPING_MAX goodies, we upcharge to a normal crate so they can't carry around 20 combat shotties
        DmList clean_up_orders = Dm.list(); // orders to remove since we are done with them

        foreach (datum_supply_order spawning_order in Dm.each<datum_supply_order>(SSshuttle.shopping_list.Copy()))
        {
            if (!Dm.truthy(empty_turfs.len))
                break;

            //adjust galactic material market based on the ordered quantities
            var sheets = spawning_order.pack as datum_supply_pack_custom_minerals;
            if (sheets != null)
            {
                var orders_adjusted = sheets.adjust_market() as DmList;
                if (Dm.truthy(orders_adjusted.len))
                {
                    datum_bank_account paying_for = spawning_order.paying_account ?? SSeconomy.get_dep_account(ACCOUNT_CAR);
                    if (!Dm.truthy(sheets.contains.len)) //no sheets in the market at all
                    {
                        await paying_for.bank_card_talk(I18n.T("Order #[] ([]) was cancelled due to insufficient materials in the market!.", spawning_order.id, spawning_order.pack.name));
                        Dm.remove(ref SSshuttle.shopping_list, spawning_order);
                        Dm.append(ref clean_up_orders, spawning_order);
                        continue;
                    }
                    //some of the orders were adjusted(quantity changed or cancelled) according to the market
                    await paying_for.bank_card_talk(I18n.T("Order #[] ([]) had the following orders adjusted<br>[]<br>.", spawning_order.id, spawning_order.pack.name, orders_adjusted.Join("<br>")));
                }
            }

            price = spawning_order.get_final_cost();

            // department orders EARN money for cargo, not the other way around
            datum_bank_account paying_for_this = null;
            if (!Dm.truthy(spawning_order.department_destination) && Dm.truthy(spawning_order.charge_on_purchase))
            {
                if (spawning_order.paying_account != null) //Someone paid out of pocket
                {
                    paying_for_this = spawning_order.paying_account;
                    // note this is before we increment, so this is the GOODY_FREE_SHIPPING_MAX + 1th goody to ship. also note we only increment off this step if they successfully pay the fee, so there's no way around it
                    if (Dm.truthy(Dm.bitand(spawning_order.pack.order_flags, ORDER_GOODY)))
                    {
                        var current_buyer_orders = goodies_by_buyer[spawning_order.paying_account] as DmList;
                        if (LAZYLEN(current_buyer_orders) == GOODY_FREE_SHIPPING_MAX)
                        {
                            price = round(price + CRATE_TAX);
                            await paying_for_this.bank_card_talk(I18n.T("Goody order size exceeds free shipping limit: Assessing [] [] S&H fee.", CRATE_TAX, MONEY_NAME_SINGULAR));
                        }
                    }
                }
                else
                    paying_for_this = SSeconomy.get_dep_account(ACCOUNT_CAR);

                if (paying_for_this != null)
                {
                    if (!Dm.truthy(paying_for_this.adjust_money(-price, I18n.T("Cargo: []", spawning_order.pack.name))))
                    {
                        if (spawning_order.paying_account != null)
                            await paying_for_this.bank_card_talk(I18n.T("Cargo order #[] rejected due to lack of funds. [] required: []", spawning_order.id, MONEY_NAME_CAPITALIZED, price));
                        if (!Dm.truthy(spawning_order.can_be_cancelled)) //only if it absolutly cannot be canceled by the player do we cancel it for them
                        {
                            Dm.remove(ref SSshuttle.shopping_list, spawning_order);
                            Dm.append(ref clean_up_orders, spawning_order);
                        }
                        continue;
                    }
                }
            }

            pack_cost = spawning_order.pack.get_cost();
            if (spawning_order.paying_account != null)
            {
                paying_for_this = spawning_order.paying_account;
                if (Dm.truthy(Dm.bitand(spawning_order.pack.order_flags, ORDER_GOODY)))
                {
                    var buyer_orders = goodies_by_buyer[spawning_order.paying_account] as DmList;
                    LAZYADD(ref buyer_orders, spawning_order);
                    goodies_by_buyer[spawning_order.paying_account] = buyer_orders;
                }
                string receiver_message = I18n.T("Cargo order #[] has shipped.", spawning_order.id);
                if (Dm.truthy(spawning_order.charge_on_purchase))
                    receiver_message += I18n.T(" [] [] have been charged to your bank account", price, MONEY_NAME);
                await paying_for_this.bank_card_talk(receiver_message);
                SSeconomy.add_audit_entry(paying_for_this, price, spawning_order.pack.name);
                datum_bank_account_department cargo = SSeconomy.get_dep_account(ACCOUNT_CAR);
                cargo.adjust_money(price - pack_cost); //Cargo gets the handling fee
            }
            value += pack_cost;

            if (!Dm.truthy(Dm.bitand(spawning_order.pack.order_flags, ORDER_GOODY))) //we handle goody crates below
            {
                var crate = await spawning_order.generate((atom)pick_n_take(empty_turfs)) as obj;
                if (crate != null)
                    crate.name = Dm.text("[] - #[]", crate.name, spawning_order.id);
            }

            SSblackbox.record_feedback("nested tally", "cargo_imports", 1, Dm.list(Dm.text("[]", spawning_order.pack.get_cost()), Dm.text("[]", spawning_order.pack.name)));
            SSblackbox.record_feedback("nested tally", "cargo_imports_by_rank", 1, Dm.list(Dm.text("[]", spawning_order.pack.name), Dm.text("[]", spawning_order.orderer_rank)));
            object from_whom = paying_for_this?.account_holder ?? I18n.T("nobody (department order)");

            investigate_log(Dm.text("Order #[] ([], placed by []), paid by [] has shipped.", spawning_order.id, spawning_order.pack.name, key_name(spawning_order.orderer_ckey), from_whom), INVESTIGATE_CARGO);
            if (Dm.truthy(Dm.bitand(spawning_order.pack.order_flags, ORDER_DANGEROUS)))
                message_admins(Dm.text("\\A [] ordered by [], paid by [] has shipped.", spawning_order.pack.name, ADMIN_LOOKUPFLW(spawning_order.orderer_ckey), from_whom));
            purchases++;

            // done dealing with order. Time to remove & delete it
            Dm.remove(ref SSshuttle.shopping_list, spawning_order);
            Dm.append(ref clean_up_orders, spawning_order);
        }

        // we handle packing all the goodies last, since the type of crate we use depends on how many goodies they ordered. If it's more than GOODY_FREE_SHIPPING_MAX
        // then we send it in a crate (including the CRATE_TAX cost), otherwise send it in a free shipping case
        foreach (object buyer_key in Dm.each(goodies_by_buyer))
        {
            var buying_account_orders = goodies_by_buyer[buyer_key] as DmList;
            var buying_account = (datum_bank_account)buyer_key;
            object buyer = buying_account.account_holder;

            if (buying_account_orders.len > GOODY_FREE_SHIPPING_MAX) // no free shipping, send a crate
            {
                var our_crate = Dm.@new<obj_structure_closet_crate_secure>(pick_n_take(empty_turfs));
                await our_crate.AddComponent(typeof(datum_component_locked_to_account), buying_account);
                our_crate.name = I18n.T("goody crate - purchased by []", buyer);
                miscboxes[buyer] = our_crate;
            }
            else //free shipping in a case
            {
                var our_case = Dm.@new<obj_item_storage_lockbox_order>(pick_n_take(empty_turfs));
                miscboxes[buyer] = our_case;
                our_case.buyer_account = buying_account;
                our_case.name = I18n.T("goody case - purchased by []", buyer);
            }

            foreach (datum_supply_order our_order in Dm.each_cast<datum_supply_order>(buying_account_orders))
            {
                foreach (object item in Dm.each(our_order.pack.contains))
                {
                    var buyer_contents = misc_contents[buyer] as DmList;
                    LAZYADD(ref buyer_contents, item);
                    misc_contents[buyer] = buyer_contents;
                }
                misc_costs[buyer] = Dm.add(misc_costs[buyer], our_order.pack.cost);
                misc_order_num[buyer] = Dm.text("[]#[] ", misc_order_num[buyer], our_order.id);
            }
        }

        foreach (object miscbox in Dm.each(miscboxes))
        {
            var order = Dm.@new<datum_supply_order>();
            order.id = misc_order_num[miscbox];
            order.generateCombo(miscboxes[miscbox], miscbox, misc_contents[miscbox], misc_costs[miscbox]);
            qdel(order);
        }

        //clean up all dealt with orders
        foreach (datum_supply_order completed_order in Dm.each<datum_supply_order>(clean_up_orders))
            qdel(completed_order);

        datum_bank_account cargo_budget = SSeconomy.get_dep_account(ACCOUNT_CAR);
        investigate_log(Dm.text("[] orders in this shipment, worth [] []. [] [] left.", purchases, value, MONEY_NAME, cargo_budget.account_balance, MONEY_NAME), INVESTIGATE_CARGO);
    }

    private void _SendSignal_buy()
    {
        // SEND_SIGNAL(SSshuttle, COMSIG_SUPPLY_SHUTTLE_BUY) (rules 11.1 (e), a statement)
        if (Dm.truthy(SSshuttle._listen_lookup?[COMSIG_SUPPLY_SHUTTLE_BUY]))
            SSshuttle._SendSignal(COMSIG_SUPPLY_SHUTTLE_BUY, Dm.list(SSshuttle));
    }

    /// Deletes and sells the items on the shuttle
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:307
    public virtual async DmTask sell()
    {
        datum_bank_account cargo_budget = SSeconomy.get_dep_account(ACCOUNT_CAR);
        float presale_points = cargo_budget.account_balance;

        string msg = "";

        var report = Dm.@new<datum_export_report>();

        foreach (area_shuttle shuttle_area in Dm.each_cast<area_shuttle>(shuttle_areas))
        {
            foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(shuttle_area.get_zlevel_turf_lists()))
            {
                foreach (turf shuttle_turf in Dm.each_cast<turf>(zlevel_turfs))
                {
                    foreach (atom_movable exporting_atom in Dm.each<atom_movable>(shuttle_turf.contents.Copy()))
                    {
                        if (iseyemob(exporting_atom))
                            continue;
                        if (isobserver(exporting_atom))
                            continue;
                        if (Dm.truthy(exporting_atom.anchored))
                            continue;
                        await export_item_and_contents(exporting_atom, apply_elastic: TRUE, dry_run: FALSE, external_report: report);
                    }
                }
            }
        }

        foreach (datum_export exported_datum in Dm.each<datum_export>(report.total_amount))
        {
            object export_text = exported_datum.total_printout(report);
            if (!Dm.truthy(export_text))
                continue;

            msg += Dm.text("[]", export_text) + "\n";
            cargo_budget.adjust_money(report.total_value[exported_datum]);
        }

        SSshuttle.centcom_message = msg;
        if (Dm.truthy(report.exported_atoms.len))
            investigate_log(Dm.text("contents sold for [] []. Contents: []. Message: []", cargo_budget.account_balance - presale_points, MONEY_NAME, report.exported_atoms.Join(","), msg), INVESTIGATE_CARGO);
    }

    /**
     * Generates a box of mail depending on our exports and imports.
     * Applied in the cargo shuttle sending/arriving, by building the crate if the round is ready to introduce mail based on the economy subsystem.
     * Then, fills the mail crate with mail, by picking applicable crew who can receive mail at the time to sending.
     */
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:344
    public virtual async DmTask create_mail()
    {
        //Early return if there's no mail waiting to prevent taking up a slot. We also don't send mails on sundays or holidays.
        if (!Dm.truthy(SSeconomy.mail_waiting) || Dm.truthy(SSeconomy.mail_blocked) || Dm.truthy(SSsecurity_level.current_security_level.disables_mail))
            return;

        //spawn crate
        DmList empty_turfs = Dm.list();
        foreach (area_shuttle shuttle_area in Dm.each_cast<area_shuttle>(shuttle_areas))
        {
            foreach (turf_open_floor shuttle_floor in Dm.each<turf_open_floor>(shuttle_area.get_turfs_from_all_zlevels()))
            {
                if (Dm.truthy(await shuttle_floor.is_blocked_turf()))
                    continue;
                Dm.append(ref empty_turfs, shuttle_floor);
            }
        }

        Dm.@new<obj_structure_closet_crate_mail_economy>(pick(empty_turfs));
    }

    /// Takes a supply pack, returns the amount we currently have on order (or OVER_ORDER_LIMIT if we are over the hardcap on orders of this type)
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:360
    public virtual object get_order_count(datum_supply_pack ordering = null)
    {
        float similar_count = 0;
        foreach (datum_supply_order order in Dm.each<datum_supply_order>(Dm.bitor(SSshuttle.shopping_list, SSshuttle.request_list)))
        {
            if (order.pack == ordering)
                similar_count += 1;
        }

        if (similar_count >= CARGO_MAX_ORDER)
            return OVER_ORDER_LIMIT;

        return similar_count;
    }

    /**
     * This proc collects all turfs on a shuttle, then replaces the air with the initial gas mix across the shuttle.
     * Used by the shuttle air upgrade.
     */
    // tg: code/modules/shuttle/mobile_port/variants/supply.dm:376
    public virtual void refill_air()
    {
        foreach (area_shuttle shuttle_area in Dm.each<area_shuttle>(shuttle_areas))
        {
            foreach (turf_open_floor shuttle_floor in Dm.each<turf_open_floor>(shuttle_area.get_turfs_from_all_zlevels()))
            {
                if (Dm.truthy(shuttle_floor.blocks_air))
                    continue; //skip walls
                var GM = (datum_gas_mixture)SSair.parse_gas_string(shuttle_floor.initial_gas_mix, typeof(datum_gas_mixture_turf));
                shuttle_floor.copy_air(GM);
                shuttle_floor.temperature = Dm.num(Dm.initial(shuttle_floor, "temperature"));
                shuttle_floor.update_visuals();
            }
        }
    }
}
