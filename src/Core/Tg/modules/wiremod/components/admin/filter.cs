// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/filter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/wiremod/components/admin/filter.dm:1  GLOBAL_LIST_INIT(wiremod_filter_info, list(...))
    public DmList wiremod_filter_info;

    // tg: code/modules/wiremod/components/admin/filter.dm:1  (GLOBAL_MANAGED(wiremod_filter_info, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwiremod_filter_info()
    {
        wiremod_filter_info = Dm.list(
            Dm.kv("alpha", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("icon", PORT_TYPE_STRING),
                Dm.kv("render_source", PORT_TYPE_STRING),
                Dm.kv("flags", PORT_TYPE_NUMBER))),
            Dm.kv("angular_blur", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("size", PORT_TYPE_NUMBER))),
            Dm.kv("displace", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("size", PORT_TYPE_NUMBER),
                Dm.kv("icon", PORT_TYPE_STRING),
                Dm.kv("render_source", PORT_TYPE_NUMBER))),
            Dm.kv("drop_shadow", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("size", PORT_TYPE_NUMBER),
                Dm.kv("offset", PORT_TYPE_NUMBER),
                Dm.kv("color", PORT_TYPE_STRING))),
            Dm.kv("blur", Dm.list(Dm.kv("size", PORT_TYPE_NUMBER))),
            Dm.kv("layer", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("icon", PORT_TYPE_STRING),
                Dm.kv("render_source", PORT_TYPE_STRING),
                Dm.kv("flags", PORT_TYPE_NUMBER),
                Dm.kv("color", PORT_TYPE_STRING),
                Dm.kv("transform", PORT_TYPE_LIST(PORT_TYPE_ANY)),
                Dm.kv("blend_mode", PORT_TYPE_NUMBER))),
            Dm.kv("motion_blur", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER))),
            Dm.kv("outline", Dm.list(
                Dm.kv("size", PORT_TYPE_NUMBER),
                Dm.kv("color", PORT_TYPE_STRING),
                Dm.kv("flags", PORT_TYPE_NUMBER))),
            Dm.kv("radial_blur", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("size", PORT_TYPE_NUMBER))),
            Dm.kv("rays", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("size", PORT_TYPE_NUMBER),
                Dm.kv("color", PORT_TYPE_STRING),
                Dm.kv("offset", PORT_TYPE_NUMBER),
                Dm.kv("density", PORT_TYPE_NUMBER),
                Dm.kv("threshold", PORT_TYPE_NUMBER),
                Dm.kv("factor", PORT_TYPE_NUMBER),
                Dm.kv("flags", PORT_TYPE_NUMBER))),
            Dm.kv("ripple", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("size", PORT_TYPE_NUMBER),
                Dm.kv("repeat", PORT_TYPE_NUMBER),
                Dm.kv("radius", PORT_TYPE_NUMBER),
                Dm.kv("falloff", PORT_TYPE_NUMBER),
                Dm.kv("flags", PORT_TYPE_NUMBER))),
            Dm.kv("wave", Dm.list(
                Dm.kv("x", PORT_TYPE_NUMBER),
                Dm.kv("y", PORT_TYPE_NUMBER),
                Dm.kv("size", PORT_TYPE_NUMBER),
                Dm.kv("offset", PORT_TYPE_NUMBER),
                Dm.kv("flags", PORT_TYPE_NUMBER))));
        Dm.append(ref gvars_datum_init_order, "wiremod_filter_info");
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:84  GLOBAL_LIST_INIT(wiremod_flag_info, list(...))
    public DmList wiremod_flag_info;

    // tg: code/modules/wiremod/components/admin/filter.dm:84  (GLOBAL_MANAGED(wiremod_flag_info, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwiremod_flag_info()
    {
        wiremod_flag_info = Dm.list(
            Dm.kv("Animation Easing", Dm.list(
                Dm.kv("Easing Type", Dm.list(
                    Dm.kv("LINEAR_EASING", LINEAR_EASING),
                    Dm.kv("SINE_EASING", SINE_EASING),
                    Dm.kv("CIRCULAR_EASING", CIRCULAR_EASING),
                    Dm.kv("QUAD_EASING", QUAD_EASING),
                    Dm.kv("CUBIC_EASING", CUBIC_EASING),
                    Dm.kv("BOUNCE_EASING", BOUNCE_EASING),
                    Dm.kv("ELASTIC_EASING", ELASTIC_EASING),
                    Dm.kv("BACK_EASING", BACK_EASING),
                    Dm.kv("JUMP_EASING", JUMP_EASING))),
                Dm.kv("EASE_IN", EASE_IN),
                Dm.kv("EASE_OUT", EASE_OUT))),
            Dm.kv("Animation Flags", Dm.list(
                Dm.kv("ANIMATION_END_NOW", ANIMATION_END_NOW),
                Dm.kv("ANIMATION_LINEAR_TRANSFORM", ANIMATION_LINEAR_TRANSFORM),
                Dm.kv("ANIMATION_PARALLEL", ANIMATION_PARALLEL),
                Dm.kv("ANIMATION_RELATIVE", ANIMATION_RELATIVE))),
            Dm.kv("Alpha Filter Flags", Dm.list(
                Dm.kv("MASK_INVERSE", MASK_INVERSE),
                Dm.kv("MASK_SWAP", MASK_SWAP))),
            Dm.kv("Color Space", Dm.list(Dm.kv("_", Dm.list(
                    Dm.kv("COLORSPACE_RGB", COLORSPACE_RGB),
                    Dm.kv("COLORSPACE_HSV", COLORSPACE_HSV),
                    Dm.kv("COLORSPACE_HSL", COLORSPACE_HSL),
                    Dm.kv("COLORSPACE_HCY", COLORSPACE_HCY))))),
            Dm.kv("Layering Flags", Dm.list(Dm.kv("_", Dm.list(
                    Dm.kv("FILTER_OVERLAY", FILTER_OVERLAY),
                    Dm.kv("FILTER_UNDERLAY", FILTER_UNDERLAY))))),
            Dm.kv("Layering Blend Mode", Dm.list(Dm.kv("_", Dm.list(
                    Dm.kv("BLEND_OVERLAY", BLEND_OVERLAY),
                    Dm.kv("BLEND_ADD", BLEND_ADD),
                    Dm.kv("BLEND_SUBTRACT", BLEND_SUBTRACT),
                    Dm.kv("BLEND_MULTIPLY", BLEND_MULTIPLY),
                    Dm.kv("BLEND_INSET_OVERLAY", BLEND_INSET_OVERLAY))))),
            Dm.kv("Outline Flags", Dm.list(
                Dm.kv("OUTLINE_SHARP", OUTLINE_SHARP),
                Dm.kv("OUTLINE_SQUARE", OUTLINE_SQUARE))),
            Dm.kv("Ray Flags", Dm.list(
                Dm.kv("FILTER_OVERLAY", FILTER_OVERLAY),
                Dm.kv("FILTER_UNDERLAY", FILTER_UNDERLAY))),
            Dm.kv("Ripple Flags", Dm.list(Dm.kv("WAVE_BOUNDED", WAVE_BOUNDED))),
            Dm.kv("Wave Flags", Dm.list(
                Dm.kv("WAVE_BOUNDED", WAVE_BOUNDED),
                Dm.kv("WAVE_SIDEWAYS", WAVE_SIDEWAYS))));
        Dm.append(ref gvars_datum_init_order, "wiremod_flag_info");
    }
}

// tg: code/modules/wiremod/components/admin/filter.dm:144
public partial class obj_item_circuit_component_filter_helper : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/filter.dm:157
    public override void populate_options()
    {
        filter_type_port = add_option_port("Filter Type", assoc_to_keys(GLOB.wiremod_filter_info) as DmList) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:160
    public override async DmTask populate_ports()
    {
        current_filter_type = filter_type_port.value;
        output_params = add_output_port("Parameters", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_ANY)) as datum_port_output;
        handle_filter_type_changed();
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:165
    public override void pre_input_received(datum_port_input port = null)
    {
        if ((port == filter_type_port) && !Dm.eq(filter_type_port.value, current_filter_type))
        {
            current_filter_type = filter_type_port.value;
            handle_filter_type_changed();
        }
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:170
    public virtual void handle_filter_type_changed()
    {
        foreach (object param_name in Dm.each(filter_params))
            remove_input_port(filter_params[param_name] as datum_port_input);
        filter_params.Cut();
        foreach (object param_name in Dm.each(GLOB.wiremod_filter_info[current_filter_type]))
        {
            filter_params[param_name] = add_input_port(param_name,
                ((DmList)GLOB.wiremod_filter_info[current_filter_type])[param_name],
                @default: ((DmList)((DmList)GLOB.master_filter_info?[current_filter_type])?["defaults"])?[param_name]);
        }
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:179
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList new_params = Dm.list();
        foreach (object param_name in Dm.each(filter_params))
        {
            datum_port_input param_port = filter_params[param_name] as datum_port_input;
            if (param_port.value != null)
                new_params[param_name] = param_port.value;
        }

        output_params.set_value(new_params);
        return null;
    }
}

// tg: code/modules/wiremod/components/admin/filter.dm:188
public partial class obj_item_circuit_component_filter_adder : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/filter.dm:202
    public override void populate_options()
    {
        filter_type_port = add_option_port("Filter Type", assoc_to_keys(GLOB.wiremod_filter_info) as DmList) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:205
    public override async DmTask populate_ports()
    {
        current_filter_type = filter_type_port.value;
        target_port = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
        filter_name = add_input_port("Filter Name", PORT_TYPE_STRING) as datum_port_input;
        filter_priority = add_input_port("Priority", PORT_TYPE_NUMBER) as datum_port_input;
        handle_filter_type_changed();
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:212
    public override void pre_input_received(datum_port_input port = null)
    {
        if ((port == filter_type_port) && !Dm.eq(filter_type_port.value, current_filter_type))
        {
            current_filter_type = filter_type_port.value;
            handle_filter_type_changed();
        }
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:217
    public virtual void handle_filter_type_changed()
    {
        foreach (object param_name in Dm.each(filter_params))
            remove_input_port(filter_params[param_name] as datum_port_input);
        filter_params.Cut();
        foreach (object param_name in Dm.each(GLOB.wiremod_filter_info[current_filter_type]))
        {
            filter_params[param_name] = add_input_port(param_name,
                ((DmList)GLOB.wiremod_filter_info[current_filter_type])[param_name],
                order: 1.5f,
                @default: ((DmList)((DmList)GLOB.master_filter_info?[current_filter_type])?["defaults"])?[param_name]);
        }
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:227
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom target_atom = target_port.value as atom;
        if (target_atom == null)
            return null;

        DmList new_params = Dm.list();
        new_params["type"] = current_filter_type;
        foreach (object param_name in Dm.each(filter_params))
        {
            datum_port_input param_port = filter_params[param_name] as datum_port_input;
            new_params[param_name] = param_port.value;
        }

        target_atom.add_filter(filter_name.value, filter_priority.value, new_params);
        return null;
    }
}

// tg: code/modules/wiremod/components/admin/filter.dm:240
public partial class obj_item_circuit_component_filter_remover : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/filter.dm:249
    public override async DmTask populate_ports()
    {
        target_port = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
        filter_name = add_input_port("Filter Name", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:253
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom target_atom = target_port.value as atom;
        if (target_atom == null)
            return null;

        target_atom.remove_filter(filter_name.value);
        return null;
    }
}

// tg: code/modules/wiremod/components/admin/filter.dm:260
public partial class obj_item_circuit_component_bitflag_helper : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/filter.dm:274
    public override void populate_options()
    {
        bitflag_port = add_option_port("Bitflag Type", assoc_to_keys(GLOB.wiremod_flag_info) as DmList) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:277
    public override async DmTask populate_ports()
    {
        current_bitflag = bitflag_port.value;
        handle_bitflag_type_changed();
        output_bitflag = add_output_port("Output", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:282
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == bitflag_port && !Dm.eq(bitflag_port.value, current_bitflag))
        {
            current_bitflag = bitflag_port.value;
            handle_bitflag_type_changed();
        }
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:287
    public virtual void handle_bitflag_type_changed()
    {
        foreach (object flag_name in Dm.each(flag_ports))
            remove_input_port(flag_ports[flag_name] as datum_port_input);
        flag_ports.Cut();

        DmList flags = GLOB.wiremod_flag_info[current_bitflag] as DmList;
        foreach (object flag in Dm.each(flags))
        {
            if (islist(flags[flag]))
                flag_ports[flag] = add_option_port(Dm.eq(flag, "_") ? current_bitflag : flag, flags[flag] as DmList);
            else
                flag_ports[flag] = add_input_port(flag, PORT_TYPE_NUMBER);
        }
    }

    // tg: code/modules/wiremod/components/admin/filter.dm:299
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        float new_output = 0;
        DmList flags = GLOB.wiremod_flag_info[current_bitflag] as DmList;
        foreach (object flag in Dm.each(flags))
        {
            if (!Dm.truthy(flag_ports[flag]))
                continue;
            else
            {
                datum_port_input flag_port = flag_ports[flag] as datum_port_input;
                if (islist(flags[flag]))
                {
                    DmList flag_options = flags[flag] as DmList;
                    new_output += Dm.num(flag_options[flag_port.value]);
                }
                else if (Dm.truthy(flag_port.value))
                    new_output += Dm.num(flags[flag]);
            }
        }

        output_bitflag.set_output(new_output);
        return null;
    }
}
