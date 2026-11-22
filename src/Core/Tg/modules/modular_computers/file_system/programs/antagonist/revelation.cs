// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/antagonist/revelation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): Revelation, the syndicate's virus that wipes and damages the computer it runs on (a cyborg's
// built-in tablet kills its cyborg), armed, obfuscated or set off through its NtosRevelation window. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/antagonist/revelation.dm:1
public partial class datum_computer_file_program_revelation : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/antagonist/revelation.dm:13
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        if (Dm.truthy(armed))
            await activate();
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/revelation.dm:18
    public virtual async DmTask activate()
    {
        if (computer != null)
        {
            if (computer is obj_item_modular_computer_pda_silicon modularInterface) //If this is a borg's integrated tablet
            {
                to_chat(modularInterface.silicon_owner, span_userdanger(I18n.T("SYSTEM PURGE DETECTED/")));
                _addtimer(Dm.@new<datum_callback>(modularInterface.silicon_owner, nameof(mob_living_silicon_robot.death)), 2 * SECONDS, TIMER_UNIQUE, file: "code/modules/modular_computers/file_system/programs/antagonist/revelation.dm", line: 23);
                return;
            }

            computer.visible_message(span_notice(I18n.T("\\The []'s screen brightly flashes and loud electrical buzzing is heard.", computer)));
            computer.enabled = FALSE;
            computer.update_appearance();

            obj_item_modular_computer host = computer;
            QDEL_LIST(computer.stored_files);
            // QDEL_LIST deleted this program too (it is one of the computer's files), and computer_file/Destroy nulled its computer:
            // tg's next line runtimes there, so the purge ends with the files wiped and the screen off, as tg's runtime leaves it
            if (computer == null)
                return;

            await host.take_damage(25, BRUTE, 0, 0);

            if (computer.internal_cell != null && prob(25))
            {
                object cell = computer.internal_cell;
                QDEL_NULL(ref cell);
                computer.internal_cell = cell;
                computer.visible_message(span_notice(I18n.T("\\The []'s battery explodes in rain of sparks.", computer)));
                do_sparks(3, FALSE, computer); // tg passes src, the program (a datum): its computer
            }
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/revelation.dm:39
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "PRG_arm":
                armed = !Dm.truthy(armed) ? TRUE : FALSE;
                return TRUE;
            case "PRG_activate":
                await activate();
                return TRUE;
            case "PRG_obfuscate":
            {
                object newname = @params["new_name"];
                if (!Dm.truthy(newname))
                    return dot;
                filedesc = newname as string;
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/revelation.dm:56
    public override datum_computer_file clone(object rename = null)
    {
        datum_computer_file_program_revelation temp = base.clone(rename) as datum_computer_file_program_revelation;
        temp.armed = armed;
        return temp;
    }

    // tg: code/modules/modular_computers/file_system/programs/antagonist/revelation.dm:61
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["armed"] = armed;

        return data;
    }
}
