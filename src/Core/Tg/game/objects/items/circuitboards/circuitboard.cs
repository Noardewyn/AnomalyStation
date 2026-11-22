// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/circuitboards/circuitboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (machine frames, D148): the circuit board and the machine board: the name extension and greyscale, the default
// parts a board gives its machine (apply_default_parts, flatten_component_list with the stock part datums) and the hooks a
// frame calls. Generated live-partial stubs: grind_results (the grinder, M8). The machine
// boards of tg's machine_circuitboards.dm are var-only and turn live with these; the boards with procs of their own stay
// inert (D132: their machine gets no default parts, _machinery.cs). The var blocks are generated.

//File with the circuitboard and circuitboard/machine class definitions and procs


// Circuitboard
// tg: code/game/objects/items/circuitboards/circuitboard.dm:6
public partial class obj_item_circuitboard : obj_item
{
    // tg: code/game/objects/items/circuitboards/circuitboard.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(name_extension))
            name = Dm.text("[] []", Dm.initial(this, (obj_item_circuitboard c) => c.name), name_extension);
        if (Dm.eq(icon_state, "circuit_map")) // some circuitboards have cool custom sprites
            Waiting.NoSleep(set_greyscale(new_config: typeof(datum_greyscale_config_circuit)));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/circuitboards/circuitboard.dm:32  (gaps P5: what the grinder makes of the board)
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_silicon), 20));
    }

    // tg: code/game/objects/items/circuitboards/circuitboard.dm:35
    public virtual async DmTask<object> apply_default_parts(obj_machinery machine = null)
    {
        if (Dm.truthy(LAZYLEN(machine.component_parts)))
        {
            // This really shouldn't happen. If it somehow does, print out a stack trace and gracefully handle it.
            stack_trace(Dm.text("apply_defauly_parts called on machine that already had component_parts: []", machine), "code/game/objects/items/circuitboards/circuitboard.dm", 38);

            // Remove references of components so it doesn't trigger Exited logic and remove existing parts.
            foreach (object part in Dm.each(machine.component_parts.Copy()))
            {
                Dm.remove(ref machine.component_parts, part);
                qdel(part as datum);
            }
        }

        // List of components always contains the circuit board used to build it.
        machine.component_parts = Dm.list(this);
        await forceMove(machine);

        // GAP(types): the machine's board instance is circuit_value (its circuit var is generated as a Type)
        if (machine.circuit_value != this)
        {
            // This really shouldn't happen. If it somehow does, print out a stack trace and gracefully handle it.
            stack_trace(Dm.text("apply_default_parts called from a circuit board that does not belong to machine: []", machine), "code/game/objects/items/circuitboards/circuitboard.dm", 50);

            QDEL_NULL(ref machine.circuit_value);
            machine.circuit_value = this;
        }

        return null;
    }

    /**
     * Used to allow the circuitboard to configure a machine in some way, shape or form.
     *
     * Arguments:
     * * machine - The machine to attempt to configure.
     */
    // tg: code/game/objects/items/circuitboards/circuitboard.dm:64
    public virtual void configure_machine(obj_machinery machine = null)
    {
        return;
    }

    /**
     * This proc is called during /obj/structure/frame/machine/finalize_construction in case there's anything else that needs to be met before completion.
     * Arguments:
     * * install_frame - The frame the circuit has been installed into for reference.
     */
    // tg: code/game/objects/items/circuitboards/circuitboard.dm:72
    public virtual object completion_requirements(obj_structure_frame install_frame = null, mob_living user = null)
    {
        return TRUE;
    }
}

// Circuitboard/machine
/*Common Parts: Parts List: Ignitor, Timer, Infra-red laser, Infra-red sensor, t_scanner, Capacitor, Valve, sensor unit,
micro-manipulator, console screen, beaker, Microlaser, matter bin, power cells.
*/
// tg: code/game/objects/items/circuitboards/circuitboard.dm:80
public partial class obj_item_circuitboard_machine : obj_item_circuitboard
{
    /**
     * Converts req_components map into a linear list with its datum components resolved
     *
     * Arguments
     * * obj/machinery/machine - if not null adds the parts to the machine directly & will not return anything
    */
    // tg: code/game/objects/items/circuitboards/circuitboard.dm:161
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(LAZYLEN(req_components)))
        {
            Dm.append(ref dot, span_info(I18n.T("It requires no components.")));
            return dot;
        }

        DmList nice_list = Dm.list();
        foreach (object component_path in Dm.each(req_components))
        {
            if (!ispath(component_path))
                continue;

            object component_name = null;
            object component_amount = req_components[component_path];

            //e.g. "glass sheet" vs. "glass"
            if (ispath(component_path, typeof(obj_item_stack)))
            {
                Type stack_path = (Type)component_path;
                component_name = Dm.initial(stack_path, (obj_item_stack p) => p.singular_name);
            }

            //stock parts in datum or obj form
            else if (ispath(component_path, typeof(obj_item_stock_parts)) || ispath(component_path, typeof(datum_stock_part)))
            {
                Type stock_part;
                if (ispath(component_path, typeof(obj_item_stock_parts)))
                    stock_part = (Type)component_path;
                else
                {
                    Type datum_part = (Type)component_path;
                    stock_part = Dm.initial(datum_part, (datum_stock_part p) => p.physical_object_type);
                }

                if (!Dm.truthy(specific_parts))
                    component_name = Dm.initial(stock_part, (obj_item_stock_parts p) => p.base_name);
                if (!Dm.truthy(component_name))
                    component_name = Dm.initial(stock_part, (obj_item_stock_parts p) => p.name);
            }

            //beakers, any non conventional part
            else if (ispath(component_path, typeof(atom)))
            {
                Type stock_part = (Type)component_path;
                component_name = Dm.initial(stock_part, (atom p) => p.name);
            }

            //append decoded name to final result
            if (component_name == null)
                stack_trace(Dm.text("[] was an invalid component", component_path), "code/game/objects/items/circuitboards/circuitboard.dm", 202);
            Dm.append(ref nice_list, I18n.T("[] []\\s", component_amount, component_name));
        }

        Dm.append(ref dot, span_info(I18n.T("It requires [].", english_list(nice_list))));
        return dot;
    }

    // tg: code/game/objects/items/circuitboards/circuitboard.dm:102
    [ShouldNotOverride]
    public virtual object flatten_component_list(obj_machinery machine = null)
    {
        object dot = NONE;
        if (QDELETED(machine))
            dot = Dm.list();

        foreach (object comp_path__iter in Dm.each(req_components))
        {
            object comp_path = comp_path__iter;
            float comp_amt = Dm.num(req_components[comp_path]);
            if (!Dm.truthy(comp_amt))
                continue;

            if (Dm.truthy(def_components) && Dm.truthy(def_components[comp_path]))
                comp_path = def_components[comp_path];

            if (ispath(comp_path, typeof(obj_item_stack)))
                continue;
            else if (ispath(comp_path, typeof(datum_stock_part)))
            {
                object stock_part_datum = GLOB.stock_part_datums[comp_path];
                if (stock_part_datum == null)
                    throw Dm.CRASH(Dm.text("[] didn't have a matching stock part datum", comp_path), "code/game/objects/items/circuitboards/circuitboard.dm", 120);
                for (float _ = 1; _ <= comp_amt; _++)
                {
                    if (!Dm.truthy(dot))
                        Dm.append(ref machine.component_parts, stock_part_datum);
                    else
                        Dm.append(ref dot, stock_part_datum);
                }
            }
            else
            {
                for (float _ = 1; _ <= comp_amt; _++)
                {
                    if (!Dm.truthy(dot))
                        Dm.append(ref machine.component_parts, Dm.@new((Type)comp_path, machine));
                    else
                        Dm.append(ref dot, comp_path);
                }
            }
        }
        return dot;
    }

    /**
     * Applies the default component parts for this machine
     *
     * Arguments
     * * obj/machinery/machine - the machine to apply the default parts to
    */
    // tg: code/game/objects/items/circuitboards/circuitboard.dm:141
    public override async DmTask<object> apply_default_parts(obj_machinery machine = null)
    {
        if (!Dm.truthy(req_components) && !Dm.truthy(Dm.length(replacement_parts)))
            return null;

        object dot = await base.apply_default_parts(machine);

        if (Dm.truthy(replacement_parts))
        {
            foreach (object part__iter in Dm.each(replacement_parts))
            {
                object part = part__iter;
                if (ispath(part, typeof(obj_item)))
                    part = Dm.@new((Type)part, machine);
                else if (ismovable(part))
                {
                    atom_movable thing = (atom_movable)part;
                    await thing.forceMove(machine);
                }
                Dm.append(ref machine.component_parts, part);
            }
            replacement_parts = null;
        }
        else
            flatten_component_list(machine);

        await machine.RefreshParts();
        return dot;
    }
}
