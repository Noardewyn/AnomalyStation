// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/disks/virus_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316: the uplink's Detomatix and F.R.A.M.E. disks; the clown's and the mime's disks with them).
// The messenger sends a virus from its "PDA_sendMessage" with the virus toggle on (messenger_program.dm:279).

/**
 * Virus disk
 * Can't hold apps, instead does unique actions.
 */
// tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:5
public partial class obj_item_disk_computer_virus : obj_item_disk_computer
{
    // tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:12
    public virtual async DmTask<object> send_virus(obj_item_modular_computer_pda source = null, obj_item_modular_computer_pda target = null, mob_living user = null, object message = null)
    {
        if (charges <= 0)
        {
            to_chat(user, span_notice(I18n.T("ERROR: Out of charges.")));
            return FALSE;
        }
        if (target == null)
        {
            to_chat(user, span_notice(I18n.T("ERROR: Could not find device.")));
            return FALSE;
        }
        return TRUE;
    }
}

/**
 * Clown virus
 * Makes people's PDA honk
 * Can also be used on open panel airlocks to make them honk on opening.
 */
// tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:26
public partial class obj_item_disk_computer_virus_clown : obj_item_disk_computer_virus
{
    // tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:29
    public override async DmTask<object> send_virus(obj_item_modular_computer_pda source = null, obj_item_modular_computer_pda target = null, mob_living user = null, object message = null)
    {
        object dot = await base.send_virus(source, target, user, message);
        if (!Dm.truthy(dot))
            return FALSE;

        user.show_message(span_notice(I18n.T("Success!")));
        charges--;
        target.honkvirus_amount = rand(15, 25);
        return TRUE;
    }
}

/**
 * Mime virus
 * Makes PDA's silent, removing their ringtone.
 */
// tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:43
public partial class obj_item_disk_computer_virus_mime : obj_item_disk_computer_virus
{
    // tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:46
    public override async DmTask<object> send_virus(obj_item_modular_computer_pda source = null, obj_item_modular_computer_pda target = null, mob_living user = null, object message = null)
    {
        object dot = await base.send_virus(source, target, user, message);
        if (!Dm.truthy(dot))
            return FALSE;

        datum_computer_file_program_messenger app = Dm.locate<datum_computer_file_program_messenger>(target.stored_files);
        if (app == null)
            return FALSE;
        user.show_message(span_notice(I18n.T("Success!")));
        charges--;
        app.alert_silenced = TRUE;
        app.ringtone = "";
        return dot;
    }
}

/**
 * Detomatix virus
 * Sends a false message, and blows the PDA up if the target responds to it (or opens their messenger before a timer)
 */
// tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:63
public partial class obj_item_disk_computer_virus_detomatix : obj_item_disk_computer_virus
{
    // tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:67
    public override async DmTask<object> send_virus(obj_item_modular_computer_pda source = null, obj_item_modular_computer_pda target = null, mob_living user = null, object message = null)
    {
        object dot = await base.send_virus(source, target, user, message);
        if (!Dm.truthy(dot))
            return FALSE;

        float difficulty = Dm.num(target.get_detomatix_difficulty());
        if (Dm.truthy(Dm.bitand(Dm.num(target.SEND_SIGNAL(COMSIG_TABLET_CHECK_DETONATE, Dm.list(target))), COMPONENT_TABLET_NO_DETONATE)) || prob(difficulty * 15))
        {
            user.show_message(span_danger(I18n.T("ERROR: Target could not be bombed.")), MSG_VISUAL);
            charges--;
            return null;
        }

        obj_item_modular_computer_pda original_host = source;
        object fakename = await sanitize_name(await tgui_input_text(user, "Enter a name for the rigged message.", "Forge Message", max_length: MAX_NAME_LEN), allow_numbers: TRUE);
        if (!Dm.truthy(fakename) || source != original_host || !Dm.truthy(await user.can_perform_action(source)))
            return null;
        object fakejob = await sanitize_name(await tgui_input_text(user, "Enter a job for the rigged message.", "Forge Message", max_length: MAX_NAME_LEN), allow_numbers: TRUE);
        if (!Dm.truthy(fakejob) || source != original_host || !Dm.truthy(await user.can_perform_action(source)))
            return null;
        bool attach_fake_photo = Dm.eq(await tgui_alert(user, "Attach a fake photo?", "Forge Message", Dm.list("Yes", "No")), "Yes");

        datum_computer_file_program_messenger app = Dm.locate<datum_computer_file_program_messenger>(source.stored_files);
        datum_computer_file_program_messenger target_app = Dm.locate<datum_computer_file_program_messenger>(target.stored_files);
        if (app == null || charges <= 0 || !Dm.truthy(await app.send_rigged_message(user, message, Dm.list(target_app), fakename, fakejob, attach_fake_photo ? TRUE : FALSE)))
            return FALSE;
        charges--;
        user.show_message(span_notice(I18n.T("Success!")));
        object reference = REF(this);
        target.add_traits(Dm.list(TRAIT_PDA_CAN_EXPLODE, TRAIT_PDA_MESSAGE_MENU_RIGGED), reference);
        _addtimer(TRAIT_CALLBACK_REMOVE(target, TRAIT_PDA_MESSAGE_MENU_RIGGED, reference), 10 * SECONDS, file: "code/modules/modular_computers/computers/item/disks/virus_disk.dm", line: 95);
        _addtimer(TRAIT_CALLBACK_REMOVE(target, TRAIT_PDA_CAN_EXPLODE, reference), 1 * MINUTES, file: "code/modules/modular_computers/computers/item/disks/virus_disk.dm", line: 96);
        return TRUE;
    }
}

/**
 * Frame cartridge
 * Creates and opens a false uplink on someone's PDA
 * Can be loaded with TC to show up on the false uplink.
 */
// tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:104
public partial class obj_item_disk_computer_virus_frame : obj_item_disk_computer_virus
{
    // tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:110
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_telecrystal telecrystal_stack)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (charges == 0)
        {
            to_chat(user, span_notice(I18n.T("[] is out of charges, it's refusing to accept [].", this, tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        telecrystals += Dm.num(telecrystal_stack.amount);
        to_chat(user, span_notice(I18n.T("You slot [] into []. The next time it's used, it will also give telecrystals.", telecrystal_stack, this)));
        await telecrystal_stack.use(telecrystal_stack.amount);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/disks/virus_disk.dm:124
    public override async DmTask<object> send_virus(obj_item_modular_computer_pda source = null, obj_item_modular_computer_pda target = null, mob_living user = null, object message = null)
    {
        object dot = await base.send_virus(source, target, user, message);
        if (!Dm.truthy(dot))
            return FALSE;

        charges--;
        string unlock_code = Dm.text("[] []", rand(100, 999), pick(GLOB.phonetic_alphabet));
        to_chat(user, span_notice(I18n.T("Success! The unlock code to the target is: []", unlock_code)));
        datum_component_uplink hidden_uplink = target.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        if (hidden_uplink == null)
        {
            datum_mind target_mind = null;
            DmList backup_players = Dm.list();
            foreach (datum_mind player in Dm.each<datum_mind>(get_crewmember_minds()))
            {
                if (Dm.eq(player.assigned_role?.title, target.saved_job))
                    backup_players.Add(player);
                if (Dm.eq(player.name, target.saved_identification))
                {
                    target_mind = player;
                    break;
                }
            }
            if (target_mind == null)
            {
                if (backup_players.len == 0)
                    target_mind = user.mind;
                else
                    target_mind = pick(backup_players) as datum_mind;
            }
            hidden_uplink = await target.AddComponent(typeof(datum_component_uplink), target_mind, Dm.named("enabled", TRUE), Dm.named("starting_tc", telecrystals)) as datum_component_uplink;
            hidden_uplink.unlock_code = unlock_code;
            hidden_uplink.uplink_handler.owner = target_mind;
        }
        else
            hidden_uplink.uplink_handler.add_telecrystals(telecrystals);
        telecrystals = 0;
        hidden_uplink.locked = FALSE;
        hidden_uplink.active = TRUE;
        return dot;
    }
}
