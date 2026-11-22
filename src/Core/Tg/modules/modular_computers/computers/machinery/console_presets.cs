// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/machinery/console_presets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (the engineering package E2, D287): the preset consoles that store their programs (engineering, research, command,
// identification, civilian, curator), as the job PDAs store theirs: the apps of later packages are stored as generated-only
// programs, whose windows show the client's placeholder (not-translated.md 74). The cargo interfacing consoles (cargochat, :93-170)
// with gaps P4d (D298): the department order app and the cargo bus channel.

// tg: code/modules/modular_computers/computers/machinery/console_presets.dm:1
public partial class obj_machinery_modular_computer_preset : obj_machinery_modular_computer
{
    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:5
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (cpu == null)
            return dot;

        foreach (Type programs in Dm.each_cast<Type>(starting_programs))
        {
            datum_computer_file program_type = Dm.@new(programs) as datum_computer_file;
            cpu.store_file(program_type);
        }
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/machinery/console_presets.dm:36
public partial class obj_machinery_modular_computer_preset_research_away : obj_machinery_modular_computer_preset_research
{
    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cpu.device_theme = PDA_THEME_RETRO;
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/machinery/console_presets.dm:67
public partial class obj_machinery_modular_computer_preset_id_centcom : obj_machinery_modular_computer_preset_id
{
    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_computer_file_program_card_mod card_mod_centcom = cpu.find_file_by_name("plexagonidwriter") as datum_computer_file_program_card_mod;
        card_mod_centcom.is_centcom = TRUE;
        return dot;
    }
}

// ===== CARGO CHAT CONSOLES =====
// tg: code/modules/modular_computers/computers/machinery/console_presets.dm:94  (gaps P4d)
public partial class obj_machinery_modular_computer_preset_cargochat : obj_machinery_modular_computer_preset
{
    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        add_starting_software();
        object dot = base.Initialize(mapload, __extra);
        setup_starting_software();
        // REGISTER_REQUIRED_MAP_ITEM(1, 1): the unit tests' map item check (UNIT_TESTS)
        if (department_type != null)
        {
            name = Dm.text("[] []", LOWER_TEXT(Dm.initial(department_type, (datum_job_department p) => p.department_name)), name);
            cpu.name = name;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:111
    public virtual void add_starting_software()
    {
        Dm.append(ref starting_programs, typeof(datum_computer_file_program_department_order));
    }

    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:114
    public virtual void setup_starting_software()
    {
        if (department_type == null)
            return;

        var chatprogram = cpu.find_file_by_name("ntnrc_client") as datum_computer_file_program_chatclient;
        chatprogram.username = Dm.text("[]_department", LOWER_TEXT(Dm.initial(department_type, (datum_job_department p) => p.department_name)));
        Dm.append(ref cpu.idle_threads, chatprogram);

        var orderprogram = cpu.find_file_by_name("dept_order") as datum_computer_file_program_department_order;
        orderprogram.set_linked_department(department_type);
        cpu.active_program = orderprogram;
        update_appearance(UPDATE_ICON);
    }
}

// tg: code/modules/modular_computers/computers/machinery/console_presets.dm:142  (gaps P4d)
public partial class obj_machinery_modular_computer_preset_cargochat_cargo : obj_machinery_modular_computer_preset_cargochat
{
    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:147
    public override void add_starting_software()
    {
        Dm.append(ref starting_programs, typeof(datum_computer_file_program_bounty_board));
        Dm.append(ref starting_programs, typeof(datum_computer_file_program_budgetorders));
        Dm.append(ref starting_programs, typeof(datum_computer_file_program_shipping));
        Dm.append(ref starting_programs, typeof(datum_computer_file_program_restock_tracker));
    }

    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:153
    public override void setup_starting_software()
    {
        var chatprogram = cpu.find_file_by_name("ntnrc_client") as datum_computer_file_program_chatclient;
        cpu.active_program = chatprogram;
        update_appearance(UPDATE_ICON);
        // Rest of the chat program setup is done in LateInit
    }

    // tg: code/modules/modular_computers/computers/machinery/console_presets.dm:159
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        var chatprogram = cpu.find_file_by_name("ntnrc_client") as datum_computer_file_program_chatclient;
        chatprogram.username = "cargo_requests_operator";

        datum_ntnet_conversation cargochat = chatprogram.create_new_channel("#cargobus", strong: TRUE);
        foreach (obj_machinery_modular_computer_preset_cargochat cargochat_console in Dm.each_cast<obj_machinery_modular_computer_preset_cargochat>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_modular_computer_preset_cargochat))))
        {
            if (cargochat_console == this)
                continue;
            var other_chatprograms = cargochat_console.cpu.find_file_by_name("ntnrc_client") as datum_computer_file_program_chatclient;
            other_chatprograms.active_channel = chatprogram.active_channel;
            cargochat.add_client(other_chatprograms, silent: TRUE);
        }
    }
}

