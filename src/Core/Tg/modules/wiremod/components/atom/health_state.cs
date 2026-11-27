// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/health_state.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Compare Health State Component
 *
 * Returns true when state matches entity.
 */
// tg: code/modules/wiremod/components/atom/health_state.dm:7
public partial class obj_item_circuit_component_compare_health_state : obj_item_circuit_component_compare
{
    // tg: code/modules/wiremod/components/atom/health_state.dm:21
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/atom/health_state.dm:28  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_compare_health_state__populate_options__component_options;

    // tg: code/modules/wiremod/components/atom/health_state.dm:25
    public override void populate_options()
    {
        input_port = add_input_port("Organism", PORT_TYPE_ATOM) as datum_port_input;

        state_option = add_option_port("Comparison Option", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/atom/health_state.dm:37
    public override object do_comparisons()
    {
        mob_living organism = input_port.value as mob_living;
        turf current_turf = get_location() as turf;
        turf target_location = get_turf(organism);
        if (!(organism is mob_living) || current_turf.z != target_location.z || get_dist(current_turf, target_location) > max_range)
            return FALSE;

        object current_option = state_option.value;
        float state = organism.stat;
        object sw__1 = current_option;
        if (Dm.eq(sw__1, "Alive"))
            return Dm.num(!IS_DEAD_OR_FAKING(organism));
        else if (Dm.eq(sw__1, "Deceased"))
            return Dm.num(IS_DEAD_OR_FAKING(organism));
        else if (Dm.eq(sw__1, "Asleep"))
            return Dm.num(IS_UNCONSCIOUS_AND_ALIVE(organism) && state == STABLE);
        else if (Dm.eq(sw__1, "Unconscious"))
            return Dm.num(IS_UNCONSCIOUS_AND_ALIVE(organism));
        else if (Dm.eq(sw__1, "Critical"))
            return Dm.num(state == SOFT_CRIT || state == HARD_CRIT);
        //Unknown state, something fucked up really bad - just return false
        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/atom/health_state.dm:28
    internal DmList obj_item_circuit_component_compare_health_state__populate_options__component_options = Dm.list(
        "Alive",
        "Asleep",
        "Critical",
        "Unconscious",
        "Deceased");
}
