// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/program_circuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the base of the modular programs' circuit components. Its subtypes (each program's
// component, in the program's own file) and the computer's shell (computer_circuit.dm) are CI3's: until then they stay inert.
// The var block is generated (D111).

/**
 * Circuit components of modular programs are special.
 * They're added to the unremovable components of the shell when the prog is installed and deleted if uninstalled.
 * This means they don't work like normal unremovable comps that live and die along with their shell.
 */
// tg: code/modules/wiremod/core/program_circuit.dm:6
public partial class obj_item_circuit_component_mod_program : obj_item_circuit_component
{
    // tg: code/modules/wiremod/core/program_circuit.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (associated_program != null)
        {
            display_name = Dm.str(Dm.initial(associated_program, (datum_computer_file_program p) => p.filedesc));
            desc = Dm.str(Dm.initial(associated_program, (datum_computer_file_program p) => p.extended_desc));
        }
        return base.Initialize(mapload, __extra); // Set the name correctly
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:29
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        obj_item_modular_computer computer = null;
        if (istype(shell, typeof(obj_item_modular_computer)))
            computer = shell as obj_item_modular_computer;
        else if (istype(shell, typeof(obj_machinery_modular_computer)))
        {
            obj_machinery_modular_computer console = shell as obj_machinery_modular_computer;
            computer = console.cpu;
        }

        ///Find the associated program in the computer's stored_files (install it otherwise) and store a reference to it.
        datum_computer_file_program found_program = Dm.locate_type(associated_program as Type, computer.stored_files) as datum_computer_file_program;
        ///The integrated circuit was loaded/duplicated
        if (found_program == null)
        {
            associated_program = await Dm.new_async(associated_program as Type);
            computer.store_file(associated_program as datum_computer_file);
        }
        else
            associated_program = found_program;

        RegisterSignal(associated_program as datum, COMSIG_COMPUTER_PROGRAM_START, nameof(on_start));
        RegisterSignal(associated_program as datum, COMSIG_COMPUTER_PROGRAM_KILL, nameof(on_kill));
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:50
    [DmParams()]
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(associated_program as datum, Dm.list(COMSIG_COMPUTER_PROGRAM_START, COMSIG_COMPUTER_PROGRAM_KILL));
        associated_program = Dm.initial(type, (obj_item_circuit_component_mod_program c) => c.associated_program);
        await base.unregister_shell();
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:55
    [ShouldCallParent]
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        start = add_input_port("Start", PORT_TYPE_SIGNAL, trigger: nameof(start_prog)) as datum_port_input;
        kill = add_input_port("Kill", PORT_TYPE_SIGNAL, trigger: nameof(kill_prog)) as datum_port_input;
        running = add_output_port("Running", PORT_TYPE_BOOLEAN) as datum_port_output;
    }

    ///For most programs, triggers only work if they're open (either active or idle).
    // tg: code/modules/wiremod/core/program_circuit.dm:63
    public override object should_receive_input(datum_port_input port = null)
    {
        object dot = base.should_receive_input(port);
        if (!Dm.truthy(dot))
            return FALSE;
        datum_computer_file_program program = associated_program as datum_computer_file_program;
        if (program == null)
            return FALSE;
        if (!Dm.truthy(program.computer.enabled))
            return FALSE;
        if (Dm.truthy(Dm.bitand(program.program_flags, PROGRAM_CIRCUITS_RUN_WHEN_CLOSED)) || COMPONENT_TRIGGERED_BY(start, port))
            return TRUE;
        obj_item_modular_computer computer = program.computer;
        if (computer.active_program == program || Dm.@in(program, computer.idle_threads))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:78
    public virtual async DmTask start_prog(datum_port_input port = null)
    {
        datum_computer_file_program program = associated_program as datum_computer_file_program;
        await program.computer.open_program(program: program);
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:81
    [SignalHandler]
    public virtual void on_start(mob_living user = null)
    {
        running.set_output(TRUE);
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:85
    public virtual async DmTask kill_prog(datum_port_input port = null)
    {
        (associated_program as datum_computer_file_program).kill_program();
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:88
    [SignalHandler]
    public virtual void on_kill(mob_living user = null)
    {
        running.set_output(FALSE);
    }

    // tg: code/modules/wiremod/core/program_circuit.dm:92
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        if (!Dm.truthy(Dm.bitand((associated_program as datum_computer_file_program)?.program_flags ?? Dm.initial(associated_program, (datum_computer_file_program p) => p.program_flags), PROGRAM_CIRCUITS_RUN_WHEN_CLOSED)))
            Dm.append(ref dot, create_ui_notice("Requires open program for inputs", "purple"));
        return dot;
    }
}
