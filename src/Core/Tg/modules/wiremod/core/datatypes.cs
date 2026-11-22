// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/datatypes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the circuits' datatype base and the table of the datatypes by key. The var block is
// generated (D111).

public partial class datum_controller_global_vars
{
    // An assoc list of all the possible datatypes.
    // tg: code/modules/wiremod/core/datatypes.dm:2  GLOBAL_LIST_INIT_TYPED(circuit_datatypes, /datum/circuit_datatype, generate_circuit_datatypes())
    public DmList circuit_datatypes;

    // tg: code/modules/wiremod/core/datatypes.dm:2  (GLOBAL_MANAGED(circuit_datatypes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcircuit_datatypes()
    {
        circuit_datatypes = generate_circuit_datatypes() as DmList;
        Dm.append(ref gvars_datum_init_order, "circuit_datatypes");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/wiremod/core/datatypes.dm:4
    public static object generate_circuit_datatypes()
    {
        DmList datatypes_by_key = Dm.list();
        foreach (Type type in Dm.each<Type>(subtypesof(typeof(datum_circuit_datatype))))
        {
            if (!Dm.truthy(Dm.initial(type, (datum_circuit_datatype t) => t.datatype)) || Dm.truthy(Dm.initial(type, (datum_circuit_datatype t) => t.@abstract)))
                continue;
            datatypes_by_key[Dm.initial(type, (datum_circuit_datatype t) => t.datatype)] = Dm.@new(type);
        }
        return datatypes_by_key;
    }
}

/**
 * A circuit datatype. Used to determine the datatype of a port and also handle any additional behaviour.
 */
// tg: code/modules/wiremod/core/datatypes.dm:15
public partial class datum_circuit_datatype : datum
{
    /**
     * Returns the value to be set for the port
     *
     * Used for implicit conversions between outputs and inputs (e.g. number -> string)
     * and applying/removing signals on inputs
     */
    // tg: code/modules/wiremod/core/datatypes.dm:40
    public virtual object convert_value(datum_port port = null, object value_to_convert = null, float? force = null)
    {
        return value_to_convert;
    }

    /**
     * Similar to convert value, however it doesn't get called by a port when a value is set. Useful for extensive conversions that may only need to be done for player inputs (e.g. lists)
     */
    // tg: code/modules/wiremod/core/datatypes.dm:46
    public virtual object convert_value_extensive(datum_port port = null, object value_to_convert = null, float? force = null)
    {
        return convert_value(port, value_to_convert, force);
    }

    /**
     * Determines if a datatype is compatible with another port of a different type.
     * Note: This is ALWAYS called on the input port, never on the output port.
     * Inputs need to care about what types they're receiving, output ports don't have to care.
     *
     * Arguments:
     * * datatype_to_check - The datatype to check
     */
    // tg: code/modules/wiremod/core/datatypes.dm:57
    public virtual object can_receive_from_datatype(object datatype_to_check = null)
    {
        return Dm.num(Dm.eq(datatype, datatype_to_check) || Dm.@in(datatype_to_check, can_receive_from));
    }

    /**
     * Called when the datatype is given to a port.
     *
     * Arguments:
     * * gained_port - The gained port.
     */
    // tg: code/modules/wiremod/core/datatypes.dm:66
    public virtual void on_gain(datum_port gained_port = null)
    {
        return;
    }

    /**
     * Called when the datatype is removed from a port.
     *
     * Arguments:
     * * lost_port - The removed port.
     */
    // tg: code/modules/wiremod/core/datatypes.dm:75
    public virtual void on_loss(datum_port lost_port = null)
    {
        return;
    }

    /**
     * Determines if a port is compatible with this datatype.
     * This WILL throw a runtime if it returns false. This is for sanity checking and it should not return false
     * unless under extraordinary circumstances or people fail to write proper code.
     *
     * Arguments:
     * * port - The port to check if it is compatible.
     */
    // tg: code/modules/wiremod/core/datatypes.dm:86
    public virtual object is_compatible(datum_port port = null)
    {
        return TRUE;
    }

    /**
     * The data to send to the UI attached to the port. Received by the type in FUNDAMENTAL_PORT_TYPES
     *
     * Arguments:
     * * port - The port sending the data.
     */
    // tg: code/modules/wiremod/core/datatypes.dm:95
    public virtual object datatype_ui_data(datum_port port = null)
    {
        return null;
    }

    /**
     * When an input is manually set by a player. This is where extra sanitizing can happen. Will still call convert_value()
     *
     * Arguments:
     * * port - The port sending the data.
     * *
     */
    // tg: code/modules/wiremod/core/datatypes.dm:105
    public virtual object handle_manual_input(datum_port_input port = null, mob user = null, object user_input = null)
    {
        return user_input;
    }

    /**
     * Used by composite datatypes. Returns all the datatypes that build this datatype up.
     */
    // tg: code/modules/wiremod/core/datatypes.dm:111
    public virtual object get_datatypes()
    {
        return Dm.list();
    }

    /**
     * Used by composite datatypes. Returns a single datatype from the list if it exists by index.
     */
    // tg: code/modules/wiremod/core/datatypes.dm:117
    public virtual object get_datatype(object index = null)
    {
        return null;
    }
}
