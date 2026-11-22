// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/format.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/**
 * # Format List Component
 *
 * Formats lists by replacing %n in format string with nth parameter.
 * Alternative to the Concatenate component.
 */
// tg: code/modules/wiremod/components/list/format.dm:7
public partial class obj_item_circuit_component_format : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/format.dm:12  var/static/regex/format_component/list_param_regex
    private static regex_format_component list_param_regex
        => TgState.Current.obj_item_circuit_component_format__list_param_regex ??= Dm.@new<regex_format_component>(@"%([0-9]+)", "g");

    // tg: code/modules/wiremod/components/list/format.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        param_regex = list_param_regex;
        return dot;
    }

    // tg: code/modules/wiremod/components/list/format.dm:31
    public virtual void make_params_port()
    {
        param_list_port = add_input_port("Params", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/list/format.dm:34
    public override async DmTask populate_ports()
    {
        format_port = add_input_port("Format", PORT_TYPE_STRING) as datum_port_input;
        make_params_port();

        output = add_output_port("Output", PORT_TYPE_STRING) as datum_port_output;
    }

    /**
     * Get an item from the list.
     * Return null to indicate invalid index.
     * Arguments:
     * * param_list - The resolved list.
     * * index_string - The raw list index, as a string.
     */
    // tg: code/modules/wiremod/components/list/format.dm:47
    public virtual object get_list_item(DmList param_list = null, object index_string = null)
    {
        float? index = Dm.text2num(index_string);
        if (Dm.lt(index, 1) || Dm.gt(index, length(param_list)))
            return null;

        return param_list[index];
    }

    // tg: code/modules/wiremod/components/list/format.dm:54
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object dot = base.input_received(port, return_values);

        // Inject the parameters.
        param_regex.context = this;
        output.set_output(param_regex.Replace(format_port.value, "/regex/format_component/proc/" + nameof(regex_format_component.process_format_component_param)));
        param_regex.context = null;
        return dot;
    }
}

/**
 * # Format Associative List Component
 *
 * Formats lists by replacing %n in format string with nth parameter.
 * Alternative to the Concatenate component.
 */
// tg: code/modules/wiremod/components/list/format.dm:68
public partial class obj_item_circuit_component_format_assoc : obj_item_circuit_component_format
{
    // tg: code/modules/wiremod/components/list/format.dm:72  var/static/regex/format_component/assoc_param_regex
    private static regex_format_component assoc_param_regex
        => TgState.Current.obj_item_circuit_component_format_assoc__assoc_param_regex ??= Dm.@new<regex_format_component>(@"%([a-zA-Z0-9_]+)", "g");

    // tg: code/modules/wiremod/components/list/format.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        param_regex = assoc_param_regex;
        return dot;
    }

    // tg: code/modules/wiremod/components/list/format.dm:78
    public override object get_list_item(DmList param_list = null, object index_string = null)
    {
        return param_list[index_string];
    }

    // tg: code/modules/wiremod/components/list/format.dm:81
    public override void make_params_port()
    {
        param_list_port = add_input_port("Params", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_ANY)) as datum_port_input;
    }
}

/**
 * # Subtype of regex that holds context to /obj/item/circuit_component/format
 */
// tg: code/modules/wiremod/components/list/format.dm:87
public partial class regex_format_component : regex
{
    /**
     * Replace %n with the actual param, as a string.
     * Arguments:
     * * match - The full %1 regex match. Unused.
     * * index_string - Just the "1" of the %1 format, actually used.
     */
    // tg: code/modules/wiremod/components/list/format.dm:96
    [ShouldNotSleep]
    public virtual object process_format_component_param(object match = null, object index_string = null)
    {
        // The static regex_context var is what you'd expect src to be, but src is actually the regex instance.
        DmList param_list = context.param_list_port.value as DmList;
        if (!islist(param_list))
            return @"[NO LIST]";

        object value = context.get_list_item(param_list, index_string);
        if (value == null)
            return @"[BAD INDEX]";

        // If this is a datum or atom, it's likely wrapped in a weakref.
        if (isweakref(value))
        {
            datum_weakref weak_value = value as datum_weakref;
            value = weak_value.resolve();
        }

        // Working with entities is constrained by range, just as with To String.
        if (isatom(value))
        {
            turf location = context.get_location() as turf;
            turf target_location = get_turf(value as atom);
            if (target_location.z != location.z || get_dist(location, target_location) > context.max_range)
                return @"[OUT OF RANGE]";
        }

        return Dm.text("[]", value);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/list/format.dm:12
    internal regex_format_component obj_item_circuit_component_format__list_param_regex;
    // tg: code/modules/wiremod/components/list/format.dm:72
    internal regex_format_component obj_item_circuit_component_format_assoc__assoc_param_regex;
}
