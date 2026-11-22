// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/ticketmachine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298). The machine's and the tickets' numbers are BYOND maptext, which the world's appearances do not carry
// to the client yet (protocol version 18 sends only the HUD's maptext): the number is read in the examine and the machine says it.
// The var blocks are generated.

//Bureaucracy machine!
//Simply set this up in the hopline and you can serve people based on ticket numbers

// tg: code/modules/paperwork/ticketmachine.dm:4
public partial class obj_machinery_ticket_machine : obj_machinery
{
    // tg: code/modules/paperwork/ticketmachine.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:38
    public override object Destroy(float? force = null)
    {
        foreach (obj_item_ticket_machine_ticket ticket in Dm.each<obj_item_ticket_machine_ticket>(tickets))
            ticket.source = null;
        tickets.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/ticketmachine.dm:44
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_wallframe_ticket_machine>(loc);
        return null;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:49
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The ticket machine shows that ticket #[] is currently being served.", current_number)));
        Dm.append(ref dot, span_notice(I18n.T("You can take a ticket out with <b>Left-Click</b> to be number [] in queue.", ticket_number + 1)));
        return dot;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:54
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool M = tool as obj_item_multitool;
        M.set_buffer(this);
        balloon_alert(user, I18n.T("saved to multitool buffer"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:59
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    //Emag the ticket machine to dispense burning tickets, as well as randomize its number to destroy the HoP's mind.
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("bureaucratic nightmare engaged"));
        ticket_number = rand(0, max_number);
        current_number = ticket_number;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (Dm.truthy(length(tickets)))
        {
            foreach (obj_item_ticket_machine_ticket ticket in Dm.each<obj_item_ticket_machine_ticket>(tickets))
            {
                ticket.audible_message(span_notice(I18n.T("\\the [] disperses!", ticket)), hearing_distance: SAMETILE_MESSAGE_RANGE);
                qdel(ticket);
            }
            tickets.Cut();
        }
        update_appearance();
        return TRUE;
    }

    ///Increments the counter by one, if there is a ticket after the current one we are serving.
    ///If we have a current ticket, remove it from the top of our tickets list and replace it with the next one if applicable
    // tg: code/modules/paperwork/ticketmachine.dm:84
    public virtual async DmTask increment()
    {
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && current_ticket != null)
        {
            current_ticket.audible_message(span_notice(I18n.T("\\the [] disperses!", current_ticket)), hearing_distance: SAMETILE_MESSAGE_RANGE);
            tickets.Cut(1, 2);
            QDEL_NULL(ref current_ticket);
        }
        if (Dm.truthy(LAZYLEN(tickets)))
        {
            current_ticket = tickets[1] as obj_item_ticket_machine_ticket;
            current_number = Dm.num(current_ticket.number); //Destroyed tickets are removed from the queue, so the next ticket's number may have skipped ahead.
            playsound(this, Dm.resource("sound/announcer/announcement/announce_dig.ogg"), 50, FALSE);
            await say(I18n.T("Now serving []!", current_ticket));
            if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                current_ticket.audible_message(span_notice(I18n.T("\\the [] vibrates!", current_ticket)), hearing_distance: SAMETILE_MESSAGE_RANGE);
            update_appearance(); //Update our icon here rather than when they take a ticket to show the current ticket number being served
        }
    }

    // tg: code/modules/paperwork/ticketmachine.dm:157
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        handle_maptext();
        return dot;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:161
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        //Gives you an idea of how many tickets are left
        if (ticket_number >= 0 && ticket_number <= 99)
            icon_state = Dm.text("[]", base_icon_state);
        else if (ticket_number == 100)
            icon_state = Dm.text("[]_empty", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/paperwork/ticketmachine.dm:168
    public virtual void handle_maptext()
    {
        //This is here to handle maptext offsets so that the numbers align.
        if (current_number >= 0 && current_number <= 9)
            maptext_x = 9;
        else if (current_number >= 10 && current_number <= 99)
            maptext_x = 6;
        else if (current_number == 100)
            maptext_x = 4;
        maptext = MAPTEXT(current_number); //Finally, apply the maptext
    }

    // tg: code/modules/paperwork/ticketmachine.dm:178
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_hand_labeler_refill)))
            return NONE;

        if (!(ticket_number >= max_number))
        {
            to_chat(user, span_notice(I18n.T("[] refuses []! There [] still [] ticket\\s left!", this, tool, max_number - ticket_number == 1 ? I18n.T("is") : I18n.T("are"), max_number - ticket_number)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You start to refill []'s ticket holder (doing this will reset its ticket count!).", this)));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You insert [] into [] as it whirs nondescriptly.", tool, this)));
        qdel(tool);
        ticket_number = 0;
        current_number = 0;
        if (Dm.truthy(length(tickets)))
        {
            foreach (obj_item_ticket_machine_ticket ticket in Dm.each<obj_item_ticket_machine_ticket>(tickets))
            {
                ticket.audible_message(span_notice(I18n.T("\\the [] disperses!", ticket)), hearing_distance: SAMETILE_MESSAGE_RANGE);
                qdel(ticket);
            }
            tickets.Cut();
        }
        max_number = Dm.num(Dm.initial(this, nameof(max_number)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:206
    public virtual void reset_cooldown()
    {
        ready = TRUE;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:209
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(ready))
        {
            to_chat(user, span_warning(I18n.T("You press the button, but nothing happens...")));
            return dot;
        }
        if (ticket_number >= max_number)
        {
            to_chat(user, span_warning(I18n.T("Ticket supply depleted, please refill this unit with a hand labeller refill cartridge!")));
            return dot;
        }
        object user_ref = REF(user);
        if (Dm.@in(user_ref, ticket_holders) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("You already have a ticket!")));
            return dot;
        }
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 100, FALSE);
        ticket_number++;
        to_chat(user, span_notice(I18n.T("You take a ticket from [], looks like you're number [] in queue...", this, ticket_number)));
        obj_item_ticket_machine_ticket theirticket = Dm.@new<obj_item_ticket_machine_ticket>(get_turf(this), ticket_number);
        theirticket.source = this;
        theirticket.owner_ref = user_ref;
        await user.put_in_hands(theirticket);
        Dm.append(ticket_holders, user_ref);
        Dm.append(tickets, theirticket);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED))) //Emag the machine to destroy the HOP's life.
        {
            ready = FALSE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_cooldown)), cooldown, file: "code/modules/paperwork/ticketmachine.dm", line: 231); //Small cooldown to prevent piles of flaming tickets
            await theirticket.fire_act();
            await user.dropItemToGround(theirticket);
            if (user is mob_living burning_user)
                await burning_user.adjust_fire_stacks(1);
            (user as mob_living)?.ignite_mob();
        }
        update_appearance();
        return dot;
    }
}

// tg: code/modules/paperwork/ticketmachine.dm:99
public partial class obj_machinery_button_ticket_machine : obj_machinery_button
{
    // tg: code/modules/paperwork/ticketmachine.dm:106
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (device_instance != null) // tg's device (buttons.cs: the GAP(types) of the button's device)
        {
            obj_item_assembly_control_ticket_machine ours = device_instance as obj_item_assembly_control_ticket_machine;
            ours.id = id;
        }
        return dot;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:112
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (Dm.eq(tool.tool_behaviour, TOOL_MULTITOOL))
        {
            obj_item_multitool M = tool as obj_item_multitool;
            if (M.buffer != null && !istype(M.buffer, typeof(obj_machinery_ticket_machine)))
                return dot;
            obj_item_assembly_control_ticket_machine controller = device_instance as obj_item_assembly_control_ticket_machine;
            controller.ticket_machine_ref = WEAKREF(M.buffer) as datum_weakref;
            id = null;
            controller.id = null;
            to_chat(user, span_warning(I18n.T("You've linked [] to [].", this, M.buffer)));
        }
        return dot;
    }
}

// tg: code/modules/paperwork/ticketmachine.dm:124
public partial class obj_item_assembly_control_ticket_machine : obj_item_assembly_control
{
    // tg: code/modules/paperwork/ticketmachine.dm:130
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:134
    public override void LateInitialize(params object[] __extra)
    {
        find_machine();
    }

    /// Locate the ticket machine to which we're linked by our ID
    // tg: code/modules/paperwork/ticketmachine.dm:138
    public virtual object find_machine()
    {
        foreach (obj_machinery_ticket_machine ticketsplease in Dm.each<obj_machinery_ticket_machine>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ticket_machine))))
        {
            if (Dm.eq(ticketsplease.id, id))
                ticket_machine_ref = WEAKREF(ticketsplease) as datum_weakref;
        }
        if (ticket_machine_ref != null)
            return TRUE;
        else
            return FALSE;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:147
    public override async DmTask<object> activate(mob activator = null)
    {
        if (Dm.truthy(cooldown))
            return null;
        if (ticket_machine_ref == null)
            return null;
        obj_machinery_ticket_machine machine = ticket_machine_ref.resolve() as obj_machinery_ticket_machine;
        if (machine == null)
            return null;
        cooldown = TRUE;
        await machine.increment();
        if (machine.current_ticket == null)
            to_chat(activator, span_notice(I18n.T("The button light indicates that there are no more tickets to be processed.")));
        // VARSET_CALLBACK(src, cooldown, FALSE): cooldown__varset (the GAP(lead) of doorcontrol.cs)
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__varset), FALSE), 1 * SECONDS, file: "code/modules/paperwork/ticketmachine.dm", line: 159);
        return null;
    }
}

// tg: code/modules/paperwork/ticketmachine.dm:241
public partial class obj_item_ticket_machine_ticket : obj_item
{
    // tg: code/modules/paperwork/ticketmachine.dm:256
    [DmParams("mapload", "num")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object num = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        number = num;
        if (num != null)
        {
            name += I18n.T(" #[]", num);
            saved_maptext = MAPTEXT(num);
            maptext = saved_maptext as string;
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition)));
        return dot;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:265
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (number != null)
        {
            Dm.append(ref dot, span_notice(I18n.T("The ticket reads shimmering text that tells you that you are number [] in queue.", number)));
            if (source != null)
                Dm.append(ref dot, span_notice(I18n.T("Below that, you can see that you are [] spot\\s away from being served.", Dm.num(number) - source.current_number)));
        }
        return dot;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:272
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        maptext = saved_maptext as string; //For some reason, storage code removes all maptext off objs, this stops its number from being wiped off when taken out of storage.
        return dot;
    }

    // tg: code/modules/paperwork/ticketmachine.dm:276
    public override object Destroy(float? force = null)
    {
        if (source != null)
        {
            source.ticket_holders.Remove(owner_ref);
            source.tickets.Remove(this);
            if (source.current_ticket == this)
                source.current_ticket = null;
            source = null;
        }
        return base.Destroy(force);
    }
}
