// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The Spectre-Meter (gaps S4c, D327): a maintenance disk's app that scans the spook around it (spirits, ghosts, hauntium), with its
// overlay's needle, the geiger's clicks in its automatic mode and its NtosSpectreMeter window (lines 1-138), and its circuit component
// (:140-173) that scans from a circuit plugged into the computer and reports each scan. The var blocks are generated (D111).

// tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:8
public partial class datum_computer_file_program_maintenance_spectre_meter : datum_computer_file_program_maintenance
{
    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:1-6  the file's defines
    private const float SPOOK_VALUE_SAME_TURF_MULT = 1.5f;
    private const float SPOOK_VALUE_LIVING_MULT = 6;
    private const float SPOOK_VALUE_DEF_MOB = 10;
    private const float SPOOK_VALUE_ICON_STATE_MAX = 120;
    private const float SPOOK_VALUE_SEGMENT = 15;
    internal const float SPOOK_COOLDOWN = 2 * SECONDS;

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:28
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        if (Dm.truthy(dot))
            soundloop = Dm.@new<datum_looping_sound_spectre_meter>();
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:33
    public override object kill_program(mob user = null)
    {
        QDEL_NULL(ref soundloop);
        auto_mode = FALSE;
        last_spook_value = 0;
        program_open_overlay = "spectre_meter_0";
        power_cell_use = PROGRAM_BASIC_CELL_USE;
        STOP_PROCESSING(SSprocessing, this);
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:42
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["spook_value"] = last_spook_value;
        data["auto_mode"] = auto_mode;
        data["on_cooldown"] = !(manual_scan_cd <= world.time) ? TRUE : FALSE; // !COOLDOWN_FINISHED(src, manual_scan_cd)
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:49
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "manual_scan":
                Waiting.Detach(scan_surroundings());
                return TRUE;
            case "toggle_mode":
                auto_mode = !Dm.truthy(auto_mode) ? TRUE : FALSE;
                if (Dm.truthy(auto_mode))
                {
                    ///We want SSprocess. It fires twice as fast than the standard SSobjs used by [computer_file/program/process_tick]
                    START_PROCESSING(SSprocessing, this);
                    soundloop.start(computer);
                }
                else
                {
                    STOP_PROCESSING(SSprocessing, this);
                    soundloop.stop(TRUE);
                }
                power_cell_use = Dm.truthy(auto_mode) ? PROGRAM_BASIC_CELL_USE * 3 : PROGRAM_BASIC_CELL_USE;
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:67
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(auto_mode))
            Waiting.Detach(scan_surroundings(FALSE));
        return null;
    }

    ///Return the "spook level" of the area the computer is in.
    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:72
    public virtual async DmTask scan_surroundings(float? manual = null)
    {
        manual ??= TRUE;
        if (Dm.truthy(manual) && !(manual_scan_cd <= world.time)) // COOLDOWN_FINISHED(src, manual_scan_cd)
            return;

        float spook_value = 0;
        turf turf = get_turf(computer) as turf;

        foreach (atom atom in Dm.each<atom>(range(5, turf)))
        {
            float spook_amount = 0;
            if (Dm.truthy(ismob(atom)))
            {
                ///ghastly mobs count toward spookiness more than observers.
                float spook_value_mult = 0;
                if (atom is mob_living living)
                {
                    if (Dm.truthy(Dm.bitand(living.mob_biotypes, MOB_SPIRIT)))
                        spook_value_mult = SPOOK_VALUE_LIVING_MULT;
                }
                else if (Dm.truthy(isobserver(atom)))
                    spook_value_mult = 1;
                spook_amount += SPOOK_VALUE_DEF_MOB * spook_value_mult;
            }
            DmList materials = await atom.has_material_type(typeof(datum_material_hauntium)) as DmList;
            if (materials != null)
                spook_amount += Dm.num(materials[typeof(datum_material_hauntium)]) / SHEET_MATERIAL_AMOUNT;
            spook_amount += Dm.num((atom.reagents as datum_reagents)?.get_reagent_amount(typeof(datum_reagent_hauntium))) / 20;
            if (spook_amount == 0)
                continue;
            if (atom.loc == turf)
                spook_amount *= SPOOK_VALUE_SAME_TURF_MULT;
            spook_value += spook_amount / Math.Max(Dm.num(get_dist(turf, atom)), 1);
            CHECK_TICK();
        }

        last_spook_value = Dm.round(spook_value);
        soundloop.last_spook_value = last_spook_value;
        object old_open_overlay = program_open_overlay;
        program_open_overlay = Dm.text("spectre_meter_[]", Math.Min(FLOOR(last_spook_value, SPOOK_VALUE_SEGMENT), SPOOK_VALUE_ICON_STATE_MAX));
        if (!Dm.eq(program_open_overlay, old_open_overlay))
            computer.update_appearance(UPDATE_OVERLAYS);

        if (Dm.truthy(manual))
        {
            manual_scan_cd = world.time + SPOOK_COOLDOWN; // COOLDOWN_START(src, manual_scan_cd, SPOOK_COOLDOWN)
            playsound(computer, Dm.resource("sound/effects/ping_hit.ogg"), vol: 40, vary: TRUE);
        }

        if (Dm.truthy(computer._listen_lookup?[COMSIG_MODULAR_COMPUTER_SPECTRE_SCAN]))
            computer._SendSignal(COMSIG_MODULAR_COMPUTER_SPECTRE_SCAN, Dm.list(computer, last_spook_value));
    }
}

// tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:114
public partial class datum_looping_sound_spectre_meter : datum_looping_sound
{
    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:114
    public datum_looping_sound_spectre_meter()
    {
        mid_length = 2;
        volume = 12;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:115  mid_sounds = /datum/looping_sound/geiger::mid_sounds
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // the geiger's initial list (components/geiger_sound.dm:67), which the types tool does not resolve through ::
        mid_sounds = Dm.list(
            Dm.list(Dm.kv(Dm.resource("sound/items/geiger/low1.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/low2.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/low3.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/low4.ogg"), 1)),
            Dm.list(Dm.kv(Dm.resource("sound/items/geiger/med1.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/med2.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/med3.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/med4.ogg"), 1)),
            Dm.list(Dm.kv(Dm.resource("sound/items/geiger/high1.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/high2.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/high3.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/high4.ogg"), 1)),
            Dm.list(Dm.kv(Dm.resource("sound/items/geiger/ext1.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/ext2.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/ext3.ogg"), 1), Dm.kv(Dm.resource("sound/items/geiger/ext4.ogg"), 1)));
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:120
    public override object get_sound(object _mid_sounds = null)
    {
        float index = 1;
        if (last_spook_value <= 14)
            return null;
        else if (last_spook_value <= 40)
            index = 1;
        else if (last_spook_value <= 65)
            index = 2;
        else if (last_spook_value <= 90)
            index = 3;
        else
            index = 4;
        return base.get_sound((mid_sounds as DmList)[index]);
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:135
    public override void stop(float? null_parent = null)
    {
        null_parent ??= FALSE;
        last_spook_value = 0;
        base.stop(null_parent);
    }
}

// tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:140
public partial class obj_item_circuit_component_mod_program_spectre_meter : obj_item_circuit_component_mod_program
{
    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:149
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        scan_results = add_output_port("Scan Results", PORT_TYPE_NUMBER) as datum_port_output;
        scanned = add_output_port("Scaned", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:154
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        RegisterSignal((associated_program as datum_computer_file_program).computer, COMSIG_MODULAR_COMPUTER_SPECTRE_SCAN, nameof(on_scan));
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:158
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal((associated_program as datum_computer_file_program).computer, COMSIG_MODULAR_COMPUTER_SPECTRE_SCAN);
        await base.unregister_shell(shell);
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:162
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        Dm.append(ref dot, create_ui_notice(Dm.text("Scan Coooldown: []", datum_computer_file_program_maintenance_spectre_meter.SPOOK_COOLDOWN), "orange", "stopwatch"));
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:166
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum_computer_file_program_maintenance_spectre_meter meter = associated_program as datum_computer_file_program_maintenance_spectre_meter;
        Waiting.Detach(meter.scan_surroundings());
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/maintenance/spectre_meter.dm:170
    [SignalHandler]
    public virtual void on_scan(datum source = null, object spook_value = null)
    {
        scan_results.set_output(spook_value);
        scanned.set_output(COMPONENT_SIGNAL);
    }
}
