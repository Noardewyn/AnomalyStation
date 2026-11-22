// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the DNA scanner (its parts, door, bolts, breakout, the console link) and the DNA data disk with
// its CentCom debug kind. The var blocks are generated (D111).

// tg: code/game/machinery/dna_scanner.dm:1
public partial class obj_machinery_dna_scannernew : obj_machinery
{
    // tg: code/game/machinery/dna_scanner.dm:21
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float new_scan_level = 0;
        damage_coeff = 0;
        precision_coeff = 0;
        foreach (datum_stock_part_scanning_module scanning_module in Dm.each<datum_stock_part_scanning_module>(component_parts))
            new_scan_level += Dm.num(scanning_module.tier);
        scan_level = new_scan_level;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            precision_coeff = Dm.num(matter_bin.tier);
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            damage_coeff = Dm.num(micro_laser.tier);
        return dot;
    }

    // tg: code/game/machinery/dna_scanner.dm:33
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (in_range(user, this) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Radiation pulse accuracy increased by factor <b>[]</b>.<br>Radiation pulse damage decreased by factor <b>[]</b>.", Dm.pow(precision_coeff, 2), Dm.pow(damage_coeff, 2))));
        return dot;
    }

    // tg: code/game/machinery/dna_scanner.dm:38
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        //no power or maintenance
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            icon_state = Dm.text("[][]_unpowered", base_icon_state, Dm.truthy(state_open) ? "_open" : null);
            return await base.update_icon_state(__extra);
        }

        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[][]_maintenance", base_icon_state, Dm.truthy(state_open) ? "_open" : null);
            return await base.update_icon_state(__extra);
        }

        //running and someone in there
        if (occupant != null)
        {
            icon_state = Dm.text("[]_occupied", base_icon_state);
            return await base.update_icon_state(__extra);
        }

        //running
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(state_open) ? "_open" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/dna_scanner.dm:58
    public virtual async DmTask toggle_open(mob user = null)
    {
        if (Dm.truthy(panel_open))
        {
            to_chat(user, span_notice(I18n.T("Close the maintenance panel first.")));
            return;
        }

        if (Dm.truthy(state_open))
        {
            await close_machine();
            return;
        }

        else if (Dm.truthy(locked))
        {
            to_chat(user, span_notice(I18n.T("The bolts are locked down, securing the door shut.")));
            return;
        }

        await open_machine();
    }

    // tg: code/game/machinery/dna_scanner.dm:72
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (HAS_TRAIT(user, TRAIT_PRIMITIVE) || user.ai_controller != null)
        {
            if (Dm.truthy(locked))
                return null; //Your primitive brain cant escape a dna scanner noob
        }
        else if (!Dm.truthy(locked)) //Not locked and not primitive? escape immediately
            await open_machine();

        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(span_notice(I18n.T("You see [] kicking against the door of []!", user, this)),
            span_notice(I18n.T("You lean on the back of [] and start pushing the door open... (this will take about [].)", this, DisplayTimeText(breakout_time))),
            span_hear(I18n.T("You hear a metallic creaking from [].", this)));
        if (Dm.truthy(await do_after(user, breakout_time, target: this)))
        {
            if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || user.loc != this || Dm.truthy(state_open) || !Dm.truthy(locked) || HAS_TRAIT(user, TRAIT_PRIMITIVE) || user.ai_controller != null)
                return null;
            locked = FALSE;
            user.visible_message(span_warning(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            await open_machine();
        }
        return null;
    }

    // tg: code/game/machinery/dna_scanner.dm:91
    public virtual object locate_computer(Type type_ = null)
    {
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            object C = Dm.locate_type(type_, get_step(this, Dm.num(direction)));
            if (C != null)
                return C;
        }
        return null;
    }

    // tg: code/game/machinery/dna_scanner.dm:98
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        density_to_set ??= TRUE;
        if (!Dm.truthy(state_open))
            return FALSE;

        await base.close_machine(target);

        // DNA manipulators cannot operate on severed heads or brains
        if (iscarbon(occupant))
        {
            if (linked_console != null)
                await linked_console.on_scanner_close();
        }

        return TRUE;
    }

    // tg: code/game/machinery/dna_scanner.dm:111
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        if (Dm.truthy(state_open))
            return FALSE;
        if (Dm.truthy(locked)) //haha bro u cant open it its locked xD
            return FALSE;

        await base.open_machine(drop, density_to_set);

        if (linked_console != null)
            linked_console.on_scanner_open();

        return TRUE;
    }

    // tg: code/game/machinery/dna_scanner.dm:124
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user) || Dm.truthy(locked))
        {
            if (Dm.num(message_cooldown) <= world.time)
            {
                message_cooldown = world.time + 50;
                to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
            }
            return null;
        }
        await open_machine();
        return null;
    }

    // tg: code/game/machinery/dna_scanner.dm:132
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return occupant != null ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/dna_scanner.dm:135
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: FALSE, open_density: FALSE, closed_density: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/game/machinery/dna_scanner.dm:138
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        await toggle_open(user);
        return null;
    }

    // tg: code/game/machinery/dna_scanner.dm:141
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        if (!iscarbon(dropped))
            return null;
        await close_machine(dropped as atom_movable);
        return null;
    }

    //This is only called by the scanner. if you ever want to use this outside of that context you'll need to refactor things a bit
    // tg: code/game/machinery/dna_scanner.dm:147
    public virtual void set_linked_console(obj_machinery_computer_dna_console new_console = null)
    {
        if (linked_console != null)
            UnregisterSignal(linked_console, COMSIG_QDELETING);
        linked_console = new_console;
        if (linked_console != null)
            RegisterSignal(linked_console, COMSIG_QDELETING, nameof(react_to_console_del));
    }

    // tg: code/game/machinery/dna_scanner.dm:154
    [SignalHandler]
    public virtual void react_to_console_del(datum source = null)
    {
        set_linked_console(null);
    }
}

//Just for transferring between genetics machines.
// tg: code/game/machinery/dna_scanner.dm:159
public partial class obj_item_disk_data : obj_item_disk
{
    // tg: code/game/machinery/dna_scanner.dm:168
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("datadisk[]", rand(0, 7));
        set_sticker_icon_state(pick("o_dna1", "o_dna2"));
        if (Dm.truthy(length(genetic_makeup_buffer)))
        {
            object blood_type = genetic_makeup_buffer["blood_type"];
            if (Dm.truthy(blood_type))
                blood_type = get_blood_type(blood_type) ?? random_human_blood_type();
        }
        return dot;
    }
}

// tg: code/game/machinery/dna_scanner.dm:178
public partial class obj_item_disk_data_debug : obj_item_disk_data
{
    // tg: code/game/machinery/dna_scanner.dm:183
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Grabs all instances of mutations and adds them to the disk
        foreach (object mut in Dm.each(subtypesof(typeof(datum_mutation))))
        {
            datum_mutation @ref = GET_INITIALIZED_MUTATION(mut) as datum_mutation;
            Dm.append(ref mutations, @ref);
        }
        return dot;
    }
}
