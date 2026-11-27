// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/animate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/wiremod/components/admin/animate.dm:4
public partial class obj_item_circuit_component_begin_animation : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/animate.dm:1
    private const string COMP_ANIMATE_ATOM = "Object";
    // tg: code/modules/wiremod/components/admin/animate.dm:2
    private const string COMP_ANIMATE_FILTER = "Filter";

    // tg: code/modules/wiremod/components/admin/animate.dm:27  var/static/list/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_begin_animation__populate_options__component_options;

    // tg: code/modules/wiremod/components/admin/animate.dm:26
    public override void populate_options()
    {
        atom_or_filter = add_option_port("Target Options", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/admin/animate.dm:34
    public override async DmTask populate_ports()
    {
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
        parallel = add_input_port("Parallel", PORT_TYPE_BOOLEAN, @default: TRUE) as datum_port_input;
        animation_loops = add_input_port("Loops", PORT_TYPE_NUMBER) as datum_port_input;
        stop_all_animations = add_input_port("Stop All Animations", PORT_TYPE_SIGNAL, trigger: nameof(stop_animations)) as datum_port_input;
        animate_event = add_output_port("Perform Animation", PORT_TYPE_INSTANT_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/animate.dm:41
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == atom_or_filter)
        {
            if (filter_target != null)
            {
                remove_input_port(filter_target);
                filter_target = null;
            }

            if (Dm.eq(atom_or_filter.value, COMP_ANIMATE_FILTER))
                filter_target = add_input_port("Filter Name", PORT_TYPE_STRING, order: 0.5f) as datum_port_input;
        }
    }

    // tg: code/modules/wiremod/components/admin/animate.dm:51
    [ShouldNotSleep]
    public virtual void stop_animations(datum_port_input port = null)
    {
        if (!Dm.truthy(target.value))
            return;
        animate(target.value, null);
    }

    // tg: code/modules/wiremod/components/admin/animate.dm:57
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (!Dm.truthy(target.value))
            return null;

        SScircuit_component.queue_instant_run();
        animate_event.set_output(COMPONENT_SIGNAL);
        DmList result = SScircuit_component.execute_instant_run() as DmList;

        if (result == null || !Dm.truthy(result["animation_steps"]))
            return null;

        atom target_atom = target.value as atom;
        if (!isatom(target_atom))
            return null;

        object target_for_animation = target_atom;
        if (Dm.eq(atom_or_filter.value, COMP_ANIMATE_FILTER))
            target_for_animation = target_atom.get_filter(filter_target.value);

        if (!Dm.truthy(target_for_animation))
            return null;

        if (!isatom(target_atom))
            return null;
        target_atom.datum_flags = Dm.bitor(target_atom.datum_flags, DF_VAR_EDITED);

        float extra_flags = NONE;
        if (Dm.truthy(parallel.value))
            extra_flags = Dm.bitor(extra_flags, ANIMATION_PARALLEL);

        log_admin_circuit(Dm.text("[] performed an animation on [].", parent.get_creator(), target_atom));
        DmList first_step = popleft(result["animation_steps"] as DmList) as DmList;
        animate(animation_step_arguments(target_for_animation, first_step, Dm.named("loop", animation_loops.value), Dm.named("flags", Dm.bitor(first_step["flags"], extra_flags))));
        foreach (DmList step in Dm.each_cast<DmList>(result["animation_steps"]))
        {
            animate(animation_step_arguments(null, step, Dm.named("flags", step["flags"])));
            if (Dm.eq(atom_or_filter.value, COMP_ANIMATE_FILTER))
            {
                DmList filter_params = target_atom.filter_data[filter_target.value] as DmList;
                foreach (object param in Dm.each(step["vars"]))
                {
                    if (Dm.truthy(filter_params.Find(param)))
                        filter_params[param] = ((DmList)step["vars"])[param];
                }
            }
        }
        return null;
    }

    // tg passes an animation step's list of vars to animate() as a positional list beside its named time, easing, flags and loop;
    // the shim's animate() takes the vars as named arguments, so the step's vars are spread into them here, in the list's order.
    private static object[] animation_step_arguments(object animated, DmList step, params object[] named)
    {
        var arguments = new List<object>();
        if (animated != null)
            arguments.Add(animated);
        arguments.Add(Dm.named("time", step["time"]));
        foreach (object var_name in Dm.each(step["vars"]))
            arguments.Add(Dm.named(Dm.str(var_name), ((DmList)step["vars"])[var_name]));
        arguments.Add(Dm.named("easing", step["easing"]));
        arguments.AddRange(named);
        return arguments.ToArray();
    }
}

// tg: code/modules/wiremod/components/admin/animate.dm:97
public partial class obj_item_circuit_component_animation_step : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/animate.dm:110
    public override async DmTask populate_ports()
    {
        animation_variables = add_input_port("Variables", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_ANY), order: 1.66f) as datum_port_input;
        animation_time = add_input_port("Time", PORT_TYPE_NUMBER, order: 1.66f) as datum_port_input;
        animation_easing = add_input_port("Easing", PORT_TYPE_NUMBER, order: 1.66f) as datum_port_input;
        animation_flags = add_input_port("Flags", PORT_TYPE_NUMBER, order: 1.66f) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/admin/animate.dm:116
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (return_values == null)
            return null;

        if (!Dm.truthy(return_values["animation_steps"]))
            return_values["animation_steps"] = Dm.list();

        DmList variables = animation_variables.value as DmList;

        if (variables == null)
            return null;

        DmList step = Dm.list(
            Dm.kv("vars", variables.Copy()),
            Dm.kv("time", Dm.num(animation_time.value) * (1 * SECONDS)),
            Dm.kv("easing", animation_easing.value),
            Dm.kv("flags", animation_flags.value)
        );

        Dm.append(return_values["animation_steps"] as DmList, Dm.list(step));
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/admin/animate.dm:27
    internal DmList obj_item_circuit_component_begin_animation__populate_options__component_options = Dm.list(
        "Object",
        "Filter");
}
