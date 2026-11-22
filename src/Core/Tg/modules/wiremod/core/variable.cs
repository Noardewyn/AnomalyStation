// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/variable.dm
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
 * A circuit variable that holds the name, the datatype and the colour of the variable (taken from the datatype).
 *
 * Used in integrated circuits for setter and getter circuit components.
 */
// tg: code/modules/wiremod/core/variable.dm:6
public partial class datum_circuit_variable : datum
{
    // tg: code/modules/wiremod/core/variable.dm:25
    [DmParams("name", "datatype")]
    public override async DmTask New(params object[] __extra)
    {
        object name = Dm.arg(__extra, 0);
        object datatype = Dm.arg(__extra, 1);
        await base.New(__extra);
        this.name = name;
        this.datatype = datatype;

        this.datatype_handler = GLOB.circuit_datatypes[datatype] as datum_circuit_datatype;
        this.listeners = Dm.list();
        this.color = datatype_handler.color;
    }

    /// Sets the value of the circuit component and triggers the appropriate listeners
    // tg: code/modules/wiremod/core/variable.dm:36
    public virtual void set_value(object new_value = null)
    {
        value = new_value;
        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(listeners))
            component.trigger_component();
    }

    // tg: code/modules/wiremod/core/variable.dm:41
    [SignalHandler]
    public virtual void on_listener_qdel(datum listener = null)
    {
        Dm.remove(listeners, listener);
    }

    /// Adds a listener to receive inputs when the variable has a value that is set.
    // tg: code/modules/wiremod/core/variable.dm:46
    public virtual void add_listener(obj_item_circuit_component to_add = null)
    {
        Dm.append(listeners, to_add);
        RegisterSignal(to_add, COMSIG_QDELETING, nameof(on_listener_qdel));
    }

    /// Removes a listener to receive inputs when the variable has a value that is set. Listener will usually clean themselves up
    // tg: code/modules/wiremod/core/variable.dm:51
    public virtual void remove_listener(obj_item_circuit_component to_remove = null)
    {
        UnregisterSignal(to_remove, COMSIG_QDELETING);
        Dm.remove(listeners, to_remove);
    }
}
