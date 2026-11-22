// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/multiz.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/power/multiz.dm:2  This bridges powernets betwen Z levels
public partial class obj_structure_cable_multilayer_multiz : obj_structure_cable_multilayer
{
    // tg: code/modules/power/multiz.dm:8
    public override object get_cable_connections(object powernetless_only = null)
    {
        object dot = base.get_cable_connections(powernetless_only);
        turf T = get_turf(this);
        Dm.append(ref dot, Dm.locate<obj_structure_cable_multilayer_multiz>(GET_TURF_BELOW(T)));
        Dm.append(ref dot, Dm.locate<obj_structure_cable_multilayer_multiz>(GET_TURF_ABOVE(T)));
        return dot;
    }

    // tg: code/modules/power/multiz.dm:14
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        turf T = get_turf(this);
        Dm.append(ref dot, span_notice(I18n.T("[] hub UP.", Dm.locate<obj_structure_cable_multilayer_multiz>(GET_TURF_BELOW(T)) != null ? I18n.T("Detected") : I18n.T("Undetected"))));
        Dm.append(ref dot, span_notice(I18n.T("[] hub DOWN.", Dm.locate<obj_structure_cable_multilayer_multiz>(GET_TURF_ABOVE(T)) != null ? I18n.T("Detected") : I18n.T("Undetected"))));
        return dot;
    }
}
