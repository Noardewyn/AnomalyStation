// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/networks/wiremod_composite.dm
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
 * This subsystem is to handle creating and storing
 * composite templates that are used to create composite datatypes
 * for integrated circuits
 *
 * See: https://en.wikipedia.org/wiki/Composite_data_type
 **/
// tg: code/controllers/subsystem/networks/wiremod_composite.dm:8 SUBSYSTEM_DEF(wiremod_composite) (the var block is generated)
public partial class datum_controller_subsystem_wiremod_composite : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(wiremod_composite) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSwiremod_composite)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/networks/wiremod_composite.dm:22
    public override async DmTask New(params object[] __extra)
    {
        if (SSwiremod_composite != this)
        {
            if (SSwiremod_composite is datum_controller_subsystem_wiremod_composite)
            {
                Recover();
                qdel(SSwiremod_composite);
            }
            SSwiremod_composite = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/networks/wiremod_composite.dm:14
    public override void PreInit()
    {
        base.PreInit();
        // This needs to execute before global variables have initialized.
        foreach (Type type in Dm.each<Type>(subtypesof(typeof(datum_circuit_composite_template))))
        {
            if (!Dm.truthy(Dm.initial(type, (datum_circuit_composite_template t) => t.datatype)))
                continue;
            templates[Dm.initial(type, (datum_circuit_composite_template t) => t.datatype)] = Dm.@new(type);
        }
    }

    // tg: code/controllers/subsystem/networks/wiremod_composite.dm:22
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        foreach (object type in Dm.each(templates))
        {
            datum_circuit_composite_template template = templates[type] as datum_circuit_composite_template;
            template.Initialize();
        }
        return SS_INIT_SUCCESS;
    }

    /**
     * Used to produce a composite datatype using another datatype, or
     * to get an already existing composite datatype.
     */
    // tg: code/controllers/subsystem/networks/wiremod_composite.dm:32
    public virtual object composite_datatype(object datatype = null, params object[] __extra)
    {
        datum_circuit_composite_template type = templates[datatype] as datum_circuit_composite_template;
        if (type == null)
            return null;
        return type.generate_composite_type(Dm.list(__extra));
    }

    // tg: code/controllers/subsystem/networks/wiremod_composite.dm:38
    public virtual object get_composite_type(object base_type = null, object datatype = null)
    {
        datum_circuit_composite_template template = templates[base_type] as datum_circuit_composite_template;
        if (template == null)
            return null;
        return template.generated_types[datatype];
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/wiremod_composite.dm:8  SUBSYSTEM_DEF(wiremod_composite): GLOBAL_REAL(SSwiremod_composite, /datum/controller/subsystem/wiremod_composite)
    internal datum_controller_subsystem_wiremod_composite SSwiremod_composite;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_wiremod_composite SSwiremod_composite => ref TgState.Current.SSwiremod_composite;
}
