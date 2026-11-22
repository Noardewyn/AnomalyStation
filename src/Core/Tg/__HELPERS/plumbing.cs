// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/plumbing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file.

public static partial class GlobalProcs
{
    /**
     * Finds a duct or plumbing machinery located at the destination
     *
     * Arguments
     * * atom/destination - the target loc we are checking for
     * * ducting_layer - the ducting layer to check for. pass -ve value when you are checking for overlapping machines
    */
    // tg: code/__HELPERS/plumbing.dm:8
    public static object ducting_layer_check(atom destination = null, object ducting_layer = null)
    {
        object dot = null;

        float is_machine = FALSE;
        if (Dm.num(ducting_layer) < 0)
        {
            is_machine = TRUE;
            ducting_layer = abs(Dm.num(ducting_layer));
        }

        foreach (atom_movable other in Dm.each<atom_movable>(get_turf(destination)))
        {
            if (other == destination)
                continue;

            //check for overlapping ducts
            obj_machinery_duct pipe = other as obj_machinery_duct;
            if (pipe != null && Dm.truthy(Dm.bitand(pipe.duct_layer, ducting_layer)))
                return pipe;

            //check for overlapping machines. -ve duct layer means we are checking machines on machines overlap which regardless of layer is not allowed
            foreach (datum_component_plumbing plumber in Dm.each_cast<datum_component_plumbing>(other.GetComponents(typeof(datum_component_plumbing))))
            {
                if (Dm.truthy(is_machine) || Dm.truthy(Dm.bitand(plumber.ducting_layer, ducting_layer)))
                    return plumber;
            }
        }
        return dot;
    }
}
