// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/transfer_valve.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the tank transfer valve, the blast cannon's bomb; the ordnance lab's bombs come with it).
// Leaves (not-translated.md section 137): the cuffable_item element (cuffing an item to a wrist), the wheelchair bomb (wheelchairs
// are vehicles), a heretic's cosmic field that makes the valve a dud (antagonists) and the admin messages. The screentips
// (add_context, add_item_context) are live-partial rows (D264). The var block is generated (D111).

// tg: code/game/objects/items/devices/transfer_valve.dm:1
public partial class obj_item_transfer_valve : obj_item
{
    // tg: code/game/objects/items/devices/transfer_valve.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/devices/transfer_valve.dm:27", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)");
        RegisterSignal(this, COMSIG_ITEM_FRIED, nameof(on_fried));
        register_context();
        register_item_context();
        return dot;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:32
    public override object Destroy(float? force = null)
    {
        attached_device = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:36
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (tank_one != null)
            Dm.append(ref dot, span_notice(I18n.T("A [] is attached to the primary port.", tank_one)));
        if (tank_two != null)
            Dm.append(ref dot, span_notice(I18n.T("A [] is attached to the secondary port.", tank_two)));
        if (tank_one == null || tank_two == null)
            Dm.append(ref dot, span_notice(I18n.T("You could attach a gas tank onto the open port.")));
        if (attached_device != null)
            Dm.append(ref dot, span_notice(I18n.T("A [] is attached to the fuse input.", attached_device)));
        if (Dm.truthy(wired))
        {
            Dm.append(ref dot, span_notice(I18n.T("It looks like some nutjob added makeshift backpack straps with []...", EXAMINE_HINT("wires"))));
            Dm.append(ref dot, span_notice(I18n.T("You could cut off the straps with [].", EXAMINE_HINT(TOOL_WIRECUTTER))));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:70
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (tank_one != null)
        {
            await split_gases();
            valve_open = FALSE;
            await tank_one.forceMove(drop_location() as atom);
        }
        else if (tank_two != null)
        {
            await split_gases();
            valve_open = FALSE;
            await tank_two.forceMove(drop_location() as atom);
        }

        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:82
    public override object IsAssemblyHolder()
    {
        return TRUE;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:85
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == tank_one)
        {
            tank_one = null;
            update_appearance();
        }
        else if (gone == tank_two)
        {
            tank_two = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:94
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (istype(interacting_with, typeof(obj_vehicle_ridden_wheelchair)))
        {
            // tg: :96-107  the wheelchair bomb (chair.attach_bomb(src) after a half-second do_after); wheelchairs are vehicles.
            Dm.Deferred("code/game/objects/items/devices/transfer_valve.dm:96", "interact_with_atom: attaching the TTV to a wheelchair (wheelchairs are vehicles, M10+)");
            return NONE;
        }
        else if (interacting_with is obj_item_tank new_tank)
        {
            if (Dm.truthy(await try_attach_tank(new_tank, user)))
                return ITEM_INTERACT_SUCCESS;
            else
                return ITEM_INTERACT_FAILURE;
        }
        else if (interacting_with is obj_item_assembly assembly) // isassembly (__DEFINES/is_helpers.dm), inline (rules 11.1 (a))
        {
            // tg passes no user here (a runtime in try_attach_assembly's transferItemToLoc); the port passes the clicker.
            if (Dm.truthy(await try_attach_assembly(assembly, user)))
                return ITEM_INTERACT_SUCCESS;
            else
                return ITEM_INTERACT_FAILURE;
        }
        return NONE;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:121
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_tank new_tank)
        {
            await try_attach_tank(new_tank, user);
            return ITEM_INTERACT_SUCCESS;
        }

        //TODO: Have this take an assemblyholder <-- commit five years ago
        if (tool is obj_item_assembly assembly) // isassembly, inline (rules 11.1 (a))
        {
            await try_attach_assembly(assembly, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stack_cable_coil coil && !Dm.truthy(wired))
        {
            if (Dm.num(coil.get_amount()) < 15)
            {
                to_chat(user, span_warning(I18n.T("You need fifteen lengths of coil for this!")));
                return ITEM_INTERACT_BLOCKING;
            }
            await coil.use(15);
            to_chat(user, span_notice(I18n.T("You add some cables, not being really sure why. Looks like <i>backpack</i> straps.")));
            wired = TRUE;
            slot_flags = Dm.num(Dm.bitor(slot_flags, ITEM_SLOT_BACK));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:145
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(wired))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You remove the cables.")));
        wired = FALSE;
        slot_flags = Dm.num(Dm.bitand(slot_flags, ~ITEM_SLOT_BACK));
        await forceMove(drop_location() as atom);
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 15);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:158
    public virtual async DmTask<object> try_attach_tank(obj_item_tank new_tank = null, mob user = null)
    {
        if (tank_one == null)
        {
            if (!Dm.truthy(await user.transferItemToLoc(new_tank, this)))
                return FALSE;
            tank_one = new_tank;
            to_chat(user, span_notice(I18n.T("You attach the [] to the transfer valve's primary port.", new_tank)));
        }
        else if (tank_two == null)
        {
            if (!Dm.truthy(await user.transferItemToLoc(new_tank, this)))
                return FALSE;
            tank_two = new_tank;
            to_chat(user, span_notice(I18n.T("You attach the [] to the transfer valve's secondary port.", new_tank)));
        }
        else
        {
            to_chat(user, span_warning(I18n.T("There are already two tanks attached, remove one first!")));
            return FALSE;
        }

        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:176
    public virtual async DmTask<object> try_attach_assembly(obj_item_assembly A = null, mob user = null)
    {
        if (Dm.truthy(A.secured))
        {
            to_chat(user, span_notice(I18n.T("The [] is secured; unsecure it first.", A)));
            return FALSE;
        }
        if (attached_device != null)
        {
            to_chat(user, span_warning(I18n.T("There is already a device attached to the valve, remove it first!")));
            return FALSE;
        }
        if (!Dm.truthy(await user.transferItemToLoc(A, this)))
            return FALSE;
        attached_device = A;
        to_chat(user, span_notice(I18n.T("You attach the [] to the valve controls and secure it.", A)));
        A.holder = this;
        await A.on_attach();
        await A.toggle_secure(); //this calls update_icon(), which calls update_icon() on the holder (i.e. the bomb).
        log_bomber(user, Dm.text("attached a [] to a ttv -", A.name), this, null, FALSE);
        attacher = user;
        return TRUE;
    }

    //These keep attached devices synced up, for example a TTV with a mouse trap being found in a bag so it's triggered, or moving the TTV with an infrared beam sensor to update the beam's direction.
    // tg: code/game/objects/items/devices/transfer_valve.dm:195
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (attached_device != null)
            attached_device.holder_movement();
        return dot;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:200
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (attached_device != null)
            await attached_device.dropped();
        return dot;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:205
    public override async DmTask<object> on_found(mob finder = null)
    {
        if (attached_device != null)
            await attached_device.on_found(finder);
        return null;
    }

    //Triggers mousetraps
    // tg: code/game/objects/items/devices/transfer_valve.dm:210
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (attached_device != null)
            await attached_device.attack_hand();
        return dot;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:217
    public virtual async DmTask process_activation(obj_item D = null)
    {
        if (Dm.truthy(toggle))
        {
            toggle = FALSE;
            await toggle_valve();
            _addtimer(Dm.@new<datum_callback>(this, nameof(toggle_off)), 5, file: "code/game/objects/items/devices/transfer_valve.dm", line: 221); //To stop a signal being spammed from a proxy sensor constantly going off or whatever
        }
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:223
    public virtual void toggle_off()
    {
        toggle = TRUE;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:226
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, (tank_one == null && tank_two == null && attached_device == null) ? "_1" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:230
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (tank_one != null)
            Dm.append(ref dot, Dm.text("[]", tank_one.icon_state));

        if (tank_two == null)
        {
            underlays = null;
        }
        else
        {
            mutable_appearance J = mutable_appearance(icon, icon_state: Dm.text("[]", tank_two.icon_state)) as mutable_appearance;
            matrix T = matrix();
            T.Translate(-13, 0);
            J.transform = T;
            underlays = Dm.list(J);
        }

        if (Dm.truthy(wired))
        {
            cable_overlay = mutable_appearance(icon, icon_state: "valve_cables", layer: Dm.num(layer) + 0.05f, appearance_flags: KEEP_TOGETHER) as mutable_appearance;
            add_overlay(cable_overlay);
        }
        else if (cable_overlay != null)
        {
            cut_overlay(cable_overlay);
            cable_overlay = null;
        }

        worn_icon_state = Dm.text("[][][]", Dm.initial(this, (obj_item_transfer_valve p) => p.worn_icon_state), tank_two != null ? "l" : "", tank_one != null ? "r" : "");
        if (ishuman(loc)) //worn
        {
            mob_living_carbon_human human = (mob_living_carbon_human)loc;
            await human.update_worn_back();
        }

        if (attached_device == null)
            return dot;

        Dm.append(ref dot, "device");
        if (attached_device is not obj_item_assembly_infra sensor)
            return dot;
        if (Dm.truthy(sensor.on) && Dm.truthy(sensor.visible))
            Dm.append(ref dot, "proxy_beam");
        return dot;
    }

    /// Merge both gases into a single tank. Combine the volume by default. If target tank isn't specified default to tank_two
    // tg: code/game/objects/items/devices/transfer_valve.dm:269
    public virtual async DmTask<object> merge_gases(obj_item_tank target = null, float? change_volume = null)
    {
        change_volume ??= TRUE;
        if (target == null)
            target = tank_two;

        if (target == null || (target != tank_one && target != tank_two))
            return FALSE;

        // tg: :276-279  for(var/obj/effect/forcefield/cosmic_field/potential_field as anything in GLOB.active_cosmic_fields): a heretic's
        // cosmic field within 2 tiles keeps the valve shut (a revenant flash). GLOB.active_cosmic_fields is not declared; no cosmic
        // field exists without the heretics (antagonists, M10), so the loop finds none.
        Dm.Deferred("code/game/objects/items/devices/transfer_valve.dm:276", "merge_gases: a heretic's cosmic field near the valve makes it a dud (antagonists, M10+)");

        // Throw both tanks into processing queue
        datum_gas_mixture target_mix = await target.return_air() as datum_gas_mixture;
        datum_gas_mixture other_mix;
        other_mix = await (target == tank_one ? tank_two : tank_one).return_air() as datum_gas_mixture;

        if (Dm.truthy(change_volume))
            target_mix.volume = Dm.num(target_mix.volume) + Dm.num(other_mix.volume);

        target_mix.merge(other_mix.remove_ratio(1) as datum_gas_mixture);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:292
    public virtual async DmTask split_gases()
    {
        if (!Dm.truthy(valve_open) || tank_one == null || tank_two == null)
            return;
        datum_gas_mixture mix_one = await tank_one.return_air() as datum_gas_mixture;
        datum_gas_mixture mix_two = await tank_two.return_air() as datum_gas_mixture;

        float volume_ratio = Dm.num(mix_one.volume) / Dm.num(mix_two.volume);
        datum_gas_mixture temp;
        temp = mix_two.remove_ratio(volume_ratio) as datum_gas_mixture;
        mix_one.merge(temp);
        mix_two.volume = Dm.num(mix_two.volume) - Dm.num(mix_one.volume);
    }

    /*
        Exadv1: I know this isn't how it's going to work, but this was just to check
        it explodes properly when it gets a signal (and it does).
    */
    // tg: code/game/objects/items/devices/transfer_valve.dm:308
    public virtual async DmTask toggle_valve(obj_item_tank target = null, float? change_volume = null)
    {
        change_volume ??= TRUE;
        playsound(this, Dm.resource("sound/effects/valve_opening.ogg"), 50);
        if (!Dm.truthy(valve_open) && tank_one != null && tank_two != null)
        {
            turf bombturf = get_turf(this);

            object attachment = null;
            object attachment_signal_log = null;
            if (attached_device != null)
            {
                if (attached_device is obj_item_assembly_signaler attached_signaller) // issignaler, inline (rules 11.1 (a))
                {
                    attachment = Dm.text("<A href='byond://?_src_=holder;[];secrets=list_signalers'>[]</A>", HrefToken(), attached_signaller);
                    attachment_signal_log = Dm.truthy(attached_signaller.last_receive_signal_log) ? Dm.text("The following log entry is the last one associated with the attached signaller<br>[]", attached_signaller.last_receive_signal_log) : "There is no signal log entry.";
                }
                else
                {
                    attachment = attached_device;
                }
            }

            string admin_attachment_message = null;
            string attachment_message = null;
            if (attachment != null)
            {
                admin_attachment_message = Dm.text("The bomb had [], which was attached by []", attachment, attacher != null ? ADMIN_LOOKUPFLW(attacher) : "Unknown");
                attachment_message = Dm.text(" with [] attached by []", attachment, attacher != null ? key_name_admin(attacher) : "Unknown");
            }

            mob bomber = get_mob_by_key(fingerprintslast);
            string admin_bomber_message = null;
            string bomber_message = null;
            if (bomber != null)
            {
                admin_bomber_message = Dm.text("The bomb's most recent set of fingerprints indicate it was last touched by []", ADMIN_LOOKUPFLW(bomber));
                bomber_message = Dm.text(" - Last touched by: []", key_name_admin(bomber));
                bomber.log_message("opened bomb valve", LOG_GAME, log_globally: FALSE);
            }

            if (attachment is obj_item_assembly_voice spoken_trigger)
            {
                attachment_message += Dm.text(" with the following activation message: \"[]\"", spoken_trigger.recorded);
                admin_attachment_message += Dm.text(" with the following activation message: \"[]\"", spoken_trigger.recorded);
            }

            string admin_bomb_message = Dm.text("Bomb valve opened in []<br>[]<br>[]<br>[]", ADMIN_VERBOSEJMP(bombturf), admin_attachment_message, admin_bomber_message, attachment_signal_log);
            Dm.append(ref GLOB.bombers, admin_bomb_message);
            message_admins(admin_bomb_message);
            log_game(Dm.text("Bomb valve opened in [][][]", AREACOORD(bombturf), attachment_message, bomber_message));

            valve_open = Dm.num(await merge_gases(target, change_volume));

            if (!Dm.truthy(valve_open))
                stack_trace("TTV gas merging failed.", "code/game/objects/items/devices/transfer_valve.dm", 350);

            for (int i = 1; i <= 6; i++)
                _addtimer(Dm.@new<datum_callback>(this, nameof(update_appearance)), 20 + (i - 1) * 10, file: "code/game/objects/items/devices/transfer_valve.dm", line: 353);
        }
        else if (Dm.truthy(valve_open) && tank_one != null && tank_two != null)
        {
            await split_gases();
            valve_open = FALSE;
            update_appearance();
        }
    }

    /*
        This doesn't do anything but the timer etc. expects it to be here
        eventually maybe have it update icon to show state (timer, prox etc.) like old bombs
    */
    // tg: code/game/objects/items/devices/transfer_valve.dm:363
    public virtual object c_state()
    {
        return null;
    }

    ///Signal when deep fried, so it can have an explosive reaction!
    // tg: code/game/objects/items/devices/transfer_valve.dm:367
    [SignalHandler]
    public virtual void on_fried(datum source = null, object fry_time = null)
    {
        log_bomber(null, "TTV valve opened via deepfrying", this, Dm.text("last fingerprints = []", fingerprintslast));
        Waiting.Detach(toggle_valve()); // the valve's tanks are read through return_air (async in this port)
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:372
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:375
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "TransferValve", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:381
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["tank_one"] = tank_one != null ? tank_one.name : null;
        data["tank_two"] = tank_two != null ? tank_two.name : null;
        data["attached_device"] = attached_device != null ? attached_device.name : null;
        data["valve"] = valve_open;
        return data;
    }

    // tg: code/game/objects/items/devices/transfer_valve.dm:389
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "tankone":
                if (tank_one != null)
                {
                    await split_gases();
                    valve_open = FALSE;
                    await tank_one.forceMove(drop_location() as atom);
                    dot = TRUE;
                }
                break;
            case "tanktwo":
                if (tank_two != null)
                {
                    await split_gases();
                    valve_open = FALSE;
                    await tank_two.forceMove(drop_location() as atom);
                    dot = TRUE;
                }
                break;
            case "toggle":
                await toggle_valve();
                dot = TRUE;
                break;
            case "device":
                if (attached_device != null)
                {
                    await attached_device.attack_self(usr);
                    dot = TRUE;
                }
                break;
            case "remove_device":
                if (attached_device != null)
                {
                    await attached_device.on_detach();
                    attached_device = null;
                    dot = TRUE;
                }
                break;
        }

        update_appearance();
        return dot;
    }

    /**
     * Returns if this is ready to be detonated. Checks if both tanks are in place.
     */
    // tg: code/game/objects/items/devices/transfer_valve.dm:425
    public virtual object ready()
    {
        return tank_one != null && tank_two != null ? tank_two : null;
    }
}

// tg: code/game/objects/items/devices/transfer_valve.dm:428
public partial class obj_item_transfer_valve_fake : obj_item_transfer_valve
{
    // tg: code/game/objects/items/devices/transfer_valve.dm:428
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        tank_one = Dm.@new<obj_item_tank_internals_plasma>(this);
        tank_two = Dm.@new<obj_item_tank_internals_oxygen>(this);

        update_appearance();
        return dot;
    }
}
